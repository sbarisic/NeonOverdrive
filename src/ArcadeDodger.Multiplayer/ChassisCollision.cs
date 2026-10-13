using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public static class ChassisCollision
{
    /// <summary>Sweep against the actual panel union, including rounded projectile thickness.</summary>
    public static float? Trace(int chassis, Vector2 start, Vector2 end, float heading, float radius)
    {
        if (SectorCollision.Circle(start, end, ChassisCatalog.Get(chassis).Radius + radius) is null)
            return null;
        start = ChassisCatalog.Rotate(start, -heading);
        end = ChassisCatalog.Rotate(end, -heading);
        float? nearest = null;
        foreach (var panel in ChassisCatalog.Get(chassis).Panels)
        {
            var vertices = panel.Vertices;
            var positive = false;
            var negative = false;
            for (var i = 0; i < vertices.Length; i++)
            {
                var a = vertices[i];
                var b = vertices[(i + 1) % vertices.Length];
                var edge = b - a;
                var cross = edge.X * (start.Y - a.Y) - edge.Y * (start.X - a.X);
                positive |= cross > .00001f;
                negative |= cross < -.00001f;
                void Hit(float? t)
                {
                    if (t is >= 0 and <= 1 && (nearest is null || t < nearest))
                        nearest = t;
                }
                Hit(SectorCollision.Circle(start - a, end - a, radius));
                var length = edge.Length();
                if (length < .001f)
                    continue;
                var tangent = edge / length;
                var normal = new Vector2(-tangent.Y, tangent.X);
                var distance = Vector2.Dot(start - a, normal);
                var velocity = Vector2.Dot(end - start, normal);
                var along = Vector2.Dot(start - a, tangent);
                if (MathF.Abs(distance) <= radius && along >= 0 && along <= length)
                    return 0;
                if (MathF.Abs(velocity) < .000001f)
                    continue;
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var t = (sign * radius - distance) / velocity;
                    var at = Vector2.Dot(Vector2.Lerp(start, end, t) - a, tangent);
                    if (at >= 0 && at <= length)
                        Hit(t);
                }
            }
            if (!positive || !negative)
                return 0;
        }
        return nearest;
    }
}
