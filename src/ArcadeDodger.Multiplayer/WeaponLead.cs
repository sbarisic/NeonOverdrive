using System.Numerics;

namespace ArcadeDodger.Multiplayer;

// Advisory centre-line interception. Stop before stages that branch, steer or terminate
// the projectile rather than presenting a false exact solution for those descendants.
public static class WeaponLead
{
    public static float BeamRange(PlayerSnapshot pilot, int bay)
    {
        var weapon = pilot.Weapons[bay];
        var initial = pilot.WeaponStats?.FirstOrDefault(s => s.Emitter == 2)?.Range ?? 0;
        var range = initial;
        var slots = Math.Min(weapon.Attachments.Length, ModuleCatalog.ModSlots(pilot.Level, bay));
        for (var i = 0; i < slots; i++)
        {
            var checkpoint = initial * (i + 1) * .05f;
            if (checkpoint >= range)
                break;
            var key = weapon.Attachments[i];
            var kind = ModuleLevels.Kind(key);
            if (kind is 11 or 12 or 13 or 14 or 15 or 25 or 26)
                return MathF.Min(range, checkpoint);
            if (kind is 6 or 7)
                range *= ModuleCatalog.Get(key).Value;
        }
        return range;
    }

    public static bool TrySolve(
        PlayerSnapshot pilot,
        int bay,
        Vector2 target,
        Vector2 velocity,
        out Vector2 point,
        out float seconds,
        out bool limited
    )
    {
        point = default;
        seconds = 0;
        limited = false;
        if (pilot.Weapons.IsDefaultOrEmpty || bay < 0 || bay >= pilot.Weapons.Length)
            return false;
        var weapon = pilot.Weapons[bay];
        var stats = pilot.WeaponStats?.FirstOrDefault(s => s.Emitter == weapon.Emitter);
        if (stats is null || stats.Speed <= 0 || stats.Range <= 0 || weapon.Emitter == 2)
            return false;
        var muzzle = ChassisCatalog.Hardpoint(
            pilot.Chassis,
            bay,
            new(pilot.X, pilot.Y),
            pilot.Heading
        );
        var delta = target - muzzle;
        float distance = 0,
            time = 0,
            speed = stats.Speed,
            range = stats.Range;
        var projectileKind = weapon.Emitter;
        var slots = Math.Min(weapon.Attachments.Length, ModuleCatalog.ModSlots(pilot.Level, bay));
        if (slots == 0)
            return ProjectileIntercept.TrySolve(
                muzzle,
                target,
                velocity,
                speed,
                range,
                out point,
                out seconds
            );
        for (var slot = 0; slot <= slots; slot++)
        {
            var gate = slot < slots ? MathF.Min(range, stats.Range * (slot + 1) * .05f) : range;
            var endTime = time + MathF.Max(0, gate - distance) / speed;
            if (
                Segment(delta, velocity, speed, distance - speed * time, time, endTime, out seconds)
            )
            {
                point = target + velocity * seconds;
                return true;
            }
            if (gate >= range || slot == slots)
                return false;
            distance = gate;
            time = endTime;
            var key = weapon.Attachments[slot];
            var kind = ModuleLevels.Kind(key);
            if (kind is 11 or 12 or 13 or 14 or 15 or 24 or 26)
            {
                limited = true;
                return false;
            }
            if (kind == 6)
                speed *= ModuleCatalog.Get(key).Value;
            else if (kind == 7)
                range *= ModuleCatalog.Get(key).Value;
            else if (kind == 25)
            {
                speed *=
                    300f
                    / (
                        projectileKind == 3 ? 240
                        : projectileKind == 4 ? 300
                        : 650
                    );
                projectileKind = 4;
            }
        }
        return false;
    }

    private static bool Segment(
        Vector2 delta,
        Vector2 velocity,
        float speed,
        float offset,
        float from,
        float to,
        out float time
    )
    {
        time = 0;
        double a = velocity.LengthSquared() - speed * speed;
        double b = 2 * (Vector2.Dot(delta, velocity) - speed * offset);
        double c = delta.LengthSquared() - offset * offset;
        if (Math.Abs(a) < 1e-6)
            return Math.Abs(b) > 1e-6 && Accept(-c / b, from, to, speed, offset, out time);
        var discriminant = b * b - 4 * a * c;
        if (discriminant < 0 || !double.IsFinite(discriminant))
            return false;
        var root = Math.Sqrt(discriminant);
        var first = (-b - root) / (2 * a);
        var second = (-b + root) / (2 * a);
        if (first > second)
            (first, second) = (second, first);
        return Accept(first, from, to, speed, offset, out time)
            || Accept(second, from, to, speed, offset, out time);
    }

    private static bool Accept(
        double value,
        float from,
        float to,
        float speed,
        float offset,
        out float time
    )
    {
        time = (float)value;
        return double.IsFinite(value)
            && value >= Math.Max(0, from)
            && value <= to
            && speed * value + offset >= 0;
    }
}
