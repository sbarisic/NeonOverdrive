using System.Diagnostics;
using System.Numerics;

namespace ArcadeDodger.Multiplayer;

/// <summary>Lazy, radius-specific visibility grids shared by NPC and waypoint routing.</summary>
public sealed class SectorNavigation(SectorDefinition sector, SectorCollision geometry)
{
    private const int Cell = 160;
    private readonly int columns = (int)MathF.Ceiling(sector.Width / Cell);
    private readonly int rows = (int)MathF.Ceiling(sector.Height / Cell);
    private readonly Dictionary<float, Grid> grids = [];

    private sealed class Grid(int count)
    {
        public readonly sbyte[] Clear = new sbyte[count];
        public readonly byte[] Tested = new byte[count];
        public readonly byte[] Open = new byte[count];
    }

    private Vector2 Point(int cell) =>
        new(cell % columns * Cell + Cell / 2, cell / columns * Cell + Cell / 2);

    private bool InBounds(Vector2 p, float radius) =>
        p.X >= radius
        && p.Y >= radius
        && p.X <= sector.Width - radius
        && p.Y <= sector.Height - radius;

    public bool CanTravel(Vector2 from, Vector2 to, float radius) =>
        InBounds(from, radius)
        && InBounds(to, radius)
        && geometry.TraceShips(from, to, radius) is null;

    private bool Clear(int cell, float radius, Grid grid)
    {
        if (grid.Clear[cell] == 0)
            grid.Clear[cell] = (sbyte)(CanTravel(Point(cell), Point(cell), radius) ? 1 : -1);
        return grid.Clear[cell] > 0;
    }

    private bool Edge(int from, int to, int direction, float radius, Grid grid)
    {
        var bit = (byte)(1 << direction);
        if ((grid.Tested[from] & bit) == 0)
        {
            grid.Tested[from] |= bit;
            if (Clear(to, radius, grid) && CanTravel(Point(from), Point(to), radius))
                grid.Open[from] |= bit;
        }
        return (grid.Open[from] & bit) != 0;
    }

    public Search Begin(Vector2 start, Vector2 finish, float radius = 32)
    {
        if (
            !float.IsFinite(radius)
            || radius < 0
            || !float.IsFinite(start.X)
            || !float.IsFinite(start.Y)
            || !float.IsFinite(finish.X)
            || !float.IsFinite(finish.Y)
        )
            throw new ArgumentOutOfRangeException(
                nameof(radius),
                "Route coordinates and radius must be finite."
            );
        if (!grids.TryGetValue(radius, out var grid))
        {
            // Normal callers use a small catalog of chassis radii. Bound retained memory even for arbitrary callers.
            if (grids.Count >= 16)
                grids.Clear();
            grids[radius] = grid = new(columns * rows);
        }
        var search = new Search();
        search.Work = Find(start, finish, radius, grid, search).GetEnumerator();
        return search;
    }

    public Vector2[] Route(Vector2 start, Vector2 finish, float radius = 32)
    {
        using var search = Begin(start, finish, radius);
        while (!search.Complete)
            search.Advance(1024);
        return search.Path;
    }

    /// <summary>Each work item performs at most one small search step. Browser callers also supply a time budget.</summary>
    public sealed class Search : IDisposable
    {
        internal IEnumerator<bool>? Work;
        public bool Complete { get; private set; }
        public Vector2[] Path { get; internal set; } = [];
        public int WorkItems { get; private set; }

        public void Advance(int maxWorkItems, double milliseconds = double.PositiveInfinity)
        {
            var started = Stopwatch.GetTimestamp();
            for (var i = 0; i < maxWorkItems && !Complete; i++)
            {
                if (Work?.MoveNext() != true)
                {
                    Complete = true;
                    Dispose();
                    break;
                }
                WorkItems++;
                if (Stopwatch.GetElapsedTime(started).TotalMilliseconds >= milliseconds)
                    break;
            }
        }

        public void Dispose()
        {
            Work?.Dispose();
            Work = null;
        }
    }

    private IEnumerable<bool> Find(
        Vector2 start,
        Vector2 finish,
        float radius,
        Grid grid,
        Search search
    )
    {
        if (!CanTravel(start, start, radius))
            yield break;
        finish = Vector2.Clamp(
            finish,
            new(radius),
            new(sector.Width - radius, sector.Height - radius)
        );
        if (!CanTravel(finish, finish, radius))
        {
            var target = finish;
            var facing = MathF.Atan2(start.Y - target.Y, start.X - target.X);
            var found = false;
            for (var distance = 50; distance <= 1600 && !found; distance += 50)
            for (var i = 0; i < 32 && !found; i++)
            {
                var angle = facing + i * MathF.Tau / 32;
                var candidate = target + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
                if (CanTravel(candidate, candidate, radius))
                {
                    finish = candidate;
                    found = true;
                }
                yield return true;
            }
            if (!found)
                yield break;
        }
        yield return true;
        if (CanTravel(start, finish, radius))
        {
            search.Path = [finish];
            yield break;
        }
        yield return true;
        var ends = new int[] { -1, -1 };
        for (var endpoint = 0; endpoint < 2; endpoint++)
        {
            var p = endpoint == 0 ? start : finish;
            var x = (int)p.X / Cell;
            var y = (int)p.Y / Cell;
            for (var ring = 0; ring < 5 && ends[endpoint] < 0; ring++)
            for (var dx = -ring; dx <= ring && ends[endpoint] < 0; dx++)
            for (var dy = -ring; dy <= ring && ends[endpoint] < 0; dy++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring)
                    continue;
                var cx = x + dx;
                var cy = y + dy;
                if (cx < 0 || cy < 0 || cx >= columns || cy >= rows)
                    continue;
                var c = cy * columns + cx;
                if (Clear(c, radius, grid) && CanTravel(p, Point(c), radius))
                    ends[endpoint] = c;
                yield return true;
            }
            if (ends[endpoint] < 0)
                yield break;
        }
        var first = ends[0];
        var last = ends[1];
        float Heuristic(int cell)
        {
            var dx = Math.Abs(cell % columns - last % columns);
            var dy = Math.Abs(cell / columns - last / columns);
            return Cell * (Math.Max(dx, dy) + .41421356f * Math.Min(dx, dy));
        }
        // Octile distance matches the eight-neighbour grid. Prefer progress toward the goal
        // when costs tie instead of flooding a wide band of equally good open-space cells.
        var frontier = new PriorityQueue<int, (float Cost, float Remaining)>();
        var cost = new Dictionary<int, float> { [first] = 0 };
        var parent = new Dictionary<int, int>();
        var visited = new HashSet<int>();
        frontier.Enqueue(first, (Heuristic(first), Heuristic(first)));
        while (frontier.TryDequeue(out var current, out _))
        {
            yield return true;
            if (!visited.Add(current))
                continue;
            if (current == last)
            {
                var path = new List<Vector2> { finish, Point(last) };
                while (parent.TryGetValue(current, out current))
                    path.Add(Point(current));
                path.Reverse();
                // String-pull a short lookahead, testing the actual sweep rather than cutting corners.
                var smooth = new List<Vector2>();
                var previous = start;
                for (var i = 0; i < path.Count; i++)
                {
                    var next = i;
                    for (var j = Math.Min(i + 12, path.Count - 1); j > i; j--)
                    {
                        var clear = CanTravel(previous, path[j], radius);
                        yield return true;
                        if (clear)
                        {
                            next = j;
                            break;
                        }
                    }
                    previous = path[next];
                    smooth.Add(previous);
                    i = next;
                }
                search.Path = smooth.ToArray();
                yield break;
            }
            var direction = 0;
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;
                var edge = direction++;
                var x = current % columns + dx;
                var y = current / columns + dy;
                if (x < 0 || y < 0 || x >= columns || y >= rows)
                    continue;
                var next = y * columns + x;
                if (visited.Contains(next))
                    continue;
                var open = Edge(current, next, edge, radius, grid);
                yield return true;
                if (!open)
                    continue;
                var nextCost = cost[current] + (dx == 0 || dy == 0 ? Cell : Cell * 1.41421356f);
                if (cost.TryGetValue(next, out var old) && old <= nextCost)
                    continue;
                cost[next] = nextCost;
                parent[next] = current;
                var remaining = Heuristic(next);
                frontier.Enqueue(next, (nextCost + remaining, remaining));
            }
        }
    }
}
