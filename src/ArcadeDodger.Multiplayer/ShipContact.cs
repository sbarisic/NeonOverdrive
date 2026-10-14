using System.Numerics;

namespace ArcadeDodger.Multiplayer;

/// <summary>Convex hull contact shared by authoritative motion and local prediction.
/// Swept SAT uses relative displacement, never the broad-phase circle as a hit.</summary>
public static class ShipContact
{
    private static readonly Vector2[][] Hulls = ChassisCatalog
        .Items.Select(c => Convex(c.Panels.SelectMany(p => p.Vertices)))
        .ToArray();

    public static Vector2[] Hull(int chassis, float angle, float scale = 1) =>
        Hulls[Math.Clamp(chassis, 0, Hulls.Length - 1)]
            .Select(p => ChassisCatalog.Rotate(p * scale, angle))
            .ToArray();

    public static Vector2[] Disc(float radius) =>
        Enumerable
            .Range(0, 12)
            .Select(i =>
                new Vector2(MathF.Cos(i * MathF.Tau / 12), MathF.Sin(i * MathF.Tau / 12)) * radius
            )
            .ToArray();

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private static Vector2[] Convex(IEnumerable<Vector2> vertices)
    {
        var p = vertices.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
        var hull = new List<Vector2>();
        foreach (var point in p)
        {
            while (hull.Count >= 2 && Cross(hull[^1] - hull[^2], point - hull[^1]) <= 0)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(point);
        }
        var lower = hull.Count;
        for (var i = p.Length - 2; i >= 0; i--)
        {
            while (hull.Count > lower && Cross(hull[^1] - hull[^2], p[i] - hull[^1]) <= 0)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(p[i]);
        }
        hull.RemoveAt(hull.Count - 1);
        return hull.ToArray();
    }

    private static (float Min, float Max) Project(Vector2[] hull, Vector2 axis)
    {
        var min = float.MaxValue;
        var max = float.MinValue;
        foreach (var p in hull)
        {
            var dot = Vector2.Dot(p, axis);
            min = Math.Min(min, dot);
            max = Math.Max(max, dot);
        }
        return (min, max);
    }

    public readonly record struct Contact(float Time, Vector2 Normal, float Depth);

    public static Contact? Sweep(
        Vector2[] a,
        Vector2 fromA,
        Vector2 toA,
        Vector2[] b,
        Vector2 fromB,
        Vector2 toB
    )
    {
        var start = fromB - fromA;
        var movement = (toB - fromB) - (toA - fromA);
        float enter = 0,
            exit = 1,
            depth = float.MaxValue;
        Vector2 normal = Vector2.UnitX,
            overlapNormal = Vector2.UnitX;
        bool overlap = true;
        foreach (var polygon in new[] { a, b })
            for (var i = 0; i < polygon.Length; i++)
            {
                var edge = polygon[(i + 1) % polygon.Length] - polygon[i];
                if (edge.LengthSquared() < .00001f)
                    continue;
                var axis = Vector2.Normalize(new Vector2(-edge.Y, edge.X));
                var pa = Project(a, axis);
                var pb = Project(b, axis);
                var offset = Vector2.Dot(start, axis);
                pb = (pb.Min + offset, pb.Max + offset);
                var left = pa.Max - pb.Min;
                var right = pb.Max - pa.Min;
                if (left < 0 || right < 0)
                    overlap = false;
                var d = Math.Min(left, right);
                if (d < depth)
                {
                    depth = d;
                    overlapNormal = left < right ? axis : -axis;
                }
                var speed = Vector2.Dot(movement, axis);
                if (MathF.Abs(speed) < .000001f)
                {
                    if (left < 0 || right < 0)
                        return null;
                    continue;
                }
                var first = (pa.Min - pb.Max) / speed;
                var last = (pa.Max - pb.Min) / speed;
                if (first > last)
                    (first, last) = (last, first);
                if (first > enter)
                {
                    enter = first;
                    normal = speed < 0 ? axis : -axis;
                }
                exit = Math.Min(exit, last);
                if (enter > exit)
                    return null;
            }
        if (overlap)
            return new(0, overlapNormal, Math.Max(0, depth));
        if (enter < 0 || enter > 1 || exit < 0)
            return null;
        return new(enter, normal, 0);
    }

    public static bool Resolve(
        Vector2[] hullA,
        Vector2 startA,
        ref Vector2 positionA,
        ref Vector2 velocityA,
        float inverseMassA,
        Vector2[] hullB,
        Vector2 startB,
        ref Vector2 positionB,
        ref Vector2 velocityB,
        float inverseMassB
    )
    {
        var sum = inverseMassA + inverseMassB;
        if (sum <= 0)
            return false;
        var hit = Sweep(hullA, startA, positionA, hullB, startB, positionB);
        if (hit is not { } c)
            return false;
        var relative = (positionB - startB) - (positionA - startA);
        var closing = Math.Min(0, Vector2.Dot(relative, c.Normal));
        var correction = c.Depth - closing * (1 - c.Time);
        if (correction <= .00001f)
            return false;
        var move = c.Normal * (correction + .001f) / sum;
        positionA -= move * inverseMassA;
        positionB += move * inverseMassB;
        var closingVelocity = Vector2.Dot(velocityB - velocityA, c.Normal);
        if (closingVelocity < 0)
        {
            var impulse = c.Normal * closingVelocity / sum;
            velocityA += impulse * inverseMassA;
            velocityB -= impulse * inverseMassB;
        }
        return true;
    }
}
