using System.Numerics;

namespace ArcadeDodger.Multiplayer;

// Static geometry uses a spatial grid shared by simulation and client prediction.
public sealed class SectorCollision
{
    private readonly SectorDefinition sector;
    private readonly Dictionary<(int, int), List<SectorObstacle>> cells = [];

    public SectorCollision(SectorDefinition sector)
    {
        this.sector = sector;
        foreach (var o in sector.Obstacles)
            for (var x = (int)((o.X - o.Radius) / 256); x <= (int)((o.X + o.Radius) / 256); x++)
            for (var y = (int)((o.Y - o.Radius) / 256); y <= (int)((o.Y + o.Radius) / 256); y++)
            {
                if (!cells.TryGetValue((x, y), out var cell))
                    cells[(x, y)] = cell = [];
                cell.Add(o);
            }
    }

    public IEnumerable<SectorObstacle> Query(Vector2 a, Vector2 b, float padding = 0)
    {
        var seen = System.Buffers.ArrayPool<int>.Shared.Rent(sector.Obstacles.Length + 1);
        Array.Clear(seen, 0, sector.Obstacles.Length + 1);
        try
        {
            for (
                var x = (int)MathF.Floor((MathF.Min(a.X, b.X) - padding) / 256);
                x <= (int)MathF.Floor((MathF.Max(a.X, b.X) + padding) / 256);
                x++
            )
            for (
                var y = (int)MathF.Floor((MathF.Min(a.Y, b.Y) - padding) / 256);
                y <= (int)MathF.Floor((MathF.Max(a.Y, b.Y) + padding) / 256);
                y++
            )
                if (cells.TryGetValue((x, y), out var cell))
                    foreach (var o in cell)
                        if (seen[o.Id] == 0)
                        {
                            seen[o.Id] = 1;
                            yield return o;
                        }
        }
        finally
        {
            System.Buffers.ArrayPool<int>.Shared.Return(seen);
        }
    }

    public (float T, Vector2 Normal)? Trace(Vector2 a, Vector2 b, float radius = 0)
    {
        (float T, Vector2 Normal)? hit = null;
        foreach (var o in Query(a, b, radius))
            if (
                Circle(a - o.Position, b - o.Position, o.Radius + radius) is { } t
                && (hit is null || t < hit.Value.T)
            )
            {
                var normal = Vector2.Lerp(a, b, t) - o.Position;
                hit = (
                    t,
                    normal.LengthSquared() > .0001f ? Vector2.Normalize(normal) : Vector2.UnitX
                );
            }
        return hit;
    }

    public (float T, Vector2 Normal)? TraceShips(Vector2 from, Vector2 to, float radius)
    {
        var hit = Trace(from, to, radius);
        foreach (var l in sector.Locations.Where(l => l.Kind == "base"))
            if (
                Circle(from - l.Position, to - l.Position, l.Radius + radius) is { } t
                && (hit is null || t < hit.Value.T)
            )
            {
                var normal = Vector2.Lerp(from, to, t) - l.Position;
                hit = (
                    t,
                    normal.LengthSquared() > .001f ? Vector2.Normalize(normal) : Vector2.UnitX
                );
            }
        return hit;
    }

    public Vector2 Move(Vector2 from, Vector2 to, float radius)
    {
        var velocity = Vector2.Zero;
        return Move(from, to, radius, ref velocity);
    }

    public Vector2 Move(Vector2 from, Vector2 to, float radius, ref Vector2 velocity)
    {
        for (var i = 0; i < 4; i++)
        {
            var hit = TraceShips(from, to, radius);
            if (hit is null)
                break;
            velocity -= hit.Value.Normal * MathF.Min(0, Vector2.Dot(velocity, hit.Value.Normal));
            var at =
                Vector2.Lerp(from, to, MathF.Max(0, hit.Value.T - .0001f))
                + hit.Value.Normal * .02f;
            var left = to - at;
            left -= hit.Value.Normal * MathF.Min(0, Vector2.Dot(left, hit.Value.Normal));
            from = at;
            to = at + left;
        }
        var bounded = Vector2.Clamp(
            to,
            new(radius),
            new(sector.Width - radius, sector.Height - radius)
        );
        if (bounded.X != to.X)
            velocity.X = 0;
        if (bounded.Y != to.Y)
            velocity.Y = 0;
        return bounded;
    }

    public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var d = b - a;
        return Vector2.Distance(
            p,
            a
                + d
                    * (
                        d.LengthSquared() < .001f
                            ? 0
                            : Math.Clamp(Vector2.Dot(p - a, d) / d.LengthSquared(), 0, 1)
                    )
        );
    }

    public static float? Circle(Vector2 start, Vector2 end, float radius)
    {
        var c = start.LengthSquared() - radius * radius;
        if (c <= 0)
            return 0;
        var d = end - start;
        var a = d.LengthSquared();
        if (a < .000001f)
            return null;
        var b = 2 * Vector2.Dot(start, d);
        var disc = b * b - 4 * a * c;
        if (disc < 0)
            return null;
        var t = (-b - MathF.Sqrt(disc)) / (2 * a);
        return t is >= 0 and <= 1 ? t : null;
    }
}
