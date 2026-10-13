using System.Numerics;

namespace ArcadeDodger.Multiplayer;

/// <summary>Bounded, lazy visibility grid. Every edge is swept against the shared geometry.</summary>
public sealed class SectorNavigation(SectorDefinition sector, SectorCollision geometry)
{
    private const int Cell = 160;
    private readonly Dictionary<(int, int), bool> clear = [];

    private Vector2 Point((int X, int Y) cell) =>
        new(cell.X * Cell + Cell / 2, cell.Y * Cell + Cell / 2);

    private bool Clear((int X, int Y) cell)
    {
        if (
            cell.X < 0
            || cell.Y < 0
            || cell.X >= sector.Width / Cell
            || cell.Y >= sector.Height / Cell
        )
            return false;
        if (!clear.TryGetValue(cell, out var value))
            clear[cell] = value = geometry.TraceShips(Point(cell), Point(cell), 40) is null;
        return value;
    }

    public Vector2[] Route(Vector2 start, Vector2 finish, float radius = 32)
    {
        if (geometry.TraceShips(finish, finish, radius) is not null)
        {
            var target = finish;
            var facing = MathF.Atan2(start.Y - target.Y, start.X - target.X);
            var found = false;
            for (var distance = 50; distance <= 1600 && !found; distance += 50)
            for (var i = 0; i < 32 && !found; i++)
            {
                var angle = facing + i * MathF.Tau / 32;
                var candidate = target + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
                if (geometry.TraceShips(candidate, candidate, radius) is not null)
                    continue;
                finish = candidate;
                found = true;
            }
            if (!found)
                return [];
        }
        if (geometry.TraceShips(start, finish, radius) is null)
            return [finish];
        (int, int)? Connect(Vector2 p)
        {
            var x = (int)p.X / Cell;
            var y = (int)p.Y / Cell;
            for (var ring = 0; ring < 5; ring++)
            for (var dx = -ring; dx <= ring; dx++)
            for (var dy = -ring; dy <= ring; dy++)
            {
                var c = (x + dx, y + dy);
                if (Clear(c) && geometry.TraceShips(p, Point(c), radius) is null)
                    return c;
            }
            return null;
        }
        if (Connect(start) is not { } first || Connect(finish) is not { } last)
            return [];
        var frontier = new PriorityQueue<(int, int), float>();
        var cost = new Dictionary<(int, int), float> { [first] = 0 };
        var parent = new Dictionary<(int, int), (int, int)>();
        frontier.Enqueue(first, 0);
        var visited = new HashSet<(int, int)>();
        while (frontier.TryDequeue(out var current, out _) && visited.Count < 16000)
        {
            if (!visited.Add(current))
                continue;
            if (current == last)
            {
                var path = new List<Vector2> { finish, Point(last) };
                while (parent.TryGetValue(current, out current))
                    path.Add(Point(current));
                path.Reverse();
                return path.ToArray();
            }
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;
                var next = (current.Item1 + dx, current.Item2 + dy);
                if (
                    !Clear(next)
                    || visited.Contains(next)
                    || geometry.TraceShips(Point(current), Point(next), radius) is not null
                )
                    continue;
                var nextCost = cost[current] + Vector2.Distance(Point(current), Point(next));
                if (cost.TryGetValue(next, out var old) && old <= nextCost)
                    continue;
                cost[next] = nextCost;
                parent[next] = current;
                frontier.Enqueue(next, nextCost + Vector2.Distance(Point(next), Point(last)));
            }
        }
        return [];
    }
}
