using System.Collections.Immutable;
using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private readonly List<(float Time, Player? Pilot, Enemy? Alien)> contactBuffer = [];

    private List<(float Time, Player? Pilot, Enemy? Alien)> Contacts(
        Shot s,
        Vector2 start,
        Vector2 end,
        float startTime = 0,
        float endTime = 1
    )
    {
        var result = contactBuffer;
        result.Clear();
        foreach (var p in playerGrid.Query(start, end, 140))
        {
            if (
                p.Id == s.Owner
                || Friendly(s, p)
                || p.Hull <= 0
                || p.Editing
                || Safe(p)
                || Tick < p.ProtectedUntil
                || s.HitPlayers.Contains(p.Id)
            )
                continue;
            var from = start - Vector2.Lerp(p.Previous, p.Position, startTime);
            var to = end - Vector2.Lerp(p.Previous, p.Position, endTime);
            var t = s.Proximity
                ? Intersection(from, to, s.FuseRadius)
                : ChassisCollision.Trace(p.Chassis, from, to, p.Heading, ProjectileRadius(s));
            if (t.HasValue)
                result.Add((t.Value, p, null));
        }
        foreach (var e in enemyGrid.Query(start, end, 140))
        {
            if (
                !CanHit(s, e)
                || e.Hull <= 0
                || Sanctuary(e.Position)
                || s.HitEnemies.Contains(e.Id)
            )
                continue;
            var from = start - Vector2.Lerp(e.Previous, e.Position, startTime);
            var to = end - Vector2.Lerp(e.Previous, e.Position, endTime);
            var mobile = !e.Installation && e.SurfacePlanet == 0 && !e.Type.StartsWith("salvage-");
            var scale = EnemyCatalog.Size(e.Level);
            var t =
                !s.Proximity && mobile
                    ? ChassisCollision.Trace(
                        e.Chassis,
                        from / scale,
                        to / scale,
                        e.Heading,
                        ProjectileRadius(s) / scale
                    )
                    : Intersection(
                        from,
                        to,
                        s.Proximity
                            ? (e.Installation ? 85 : 0) + s.FuseRadius
                            : EnemyRadius(e) + ProjectileRadius(s)
                    );
            if (t.HasValue)
                result.Add((t.Value, null, e));
        }
        result.Sort(
            static (a, b) =>
            {
                var time = a.Time.CompareTo(b.Time);
                return time != 0
                    ? time
                    : (a.Pilot?.Id ?? a.Alien!.Id).CompareTo(b.Pilot?.Id ?? b.Alien!.Id);
            }
        );
        return result;
    }

    private void Guide(Shot s)
    {
        if (!s.Homing)
            return;
        var target = enemyGrid
            .Query(s.Position, s.Position, s.HomingRadius)
            .Where(e =>
                e.Hull > 0
                && CanHit(s, e)
                && !Sanctuary(e.Position)
                && VisibleLine(s.Position, e.Position)
                && !s.HitEnemies.Contains(e.Id)
            )
            .OrderBy(e => Vector2.DistanceSquared(e.Position, s.Position))
            .FirstOrDefault();
        if (target is null)
            return;
        var delta = target.Position - s.Position;
        if (delta.LengthSquared() < 1 || delta.LengthSquared() > s.HomingRadius * s.HomingRadius)
            return;
        var speed = s.Velocity.Length();
        var direction = s.IsBeam
            ? Vector2.Normalize(delta)
            : Vector2.Lerp(Vector2.Normalize(s.Velocity), Vector2.Normalize(delta), s.HomingRate);
        if (direction.LengthSquared() > .00001f)
            s.Velocity = Vector2.Normalize(direction) * speed;
    }

    private bool gravityMovedShips;

    private void Pull(Shot s, Vector2 at, float seconds)
    {
        if (s.Gravity == 0)
            return;
        foreach (
            var e in enemyGrid
                .Query(at, at, s.GravityRadius)
                .Where(e =>
                    e.Hull > 0
                    && CanHit(s, e)
                    && e.SurfacePlanet == 0
                    && !e.Installation
                    && !Stationary(e)
                    && !Sanctuary(e.Position)
                    && VisibleLine(at, e.Position)
                )
                .ToArray()
        )
        {
            var delta = at - e.Position;
            var distance = delta.Length();
            if (distance > .001f && distance < s.GravityRadius)
            {
                if (!gravityMovedShips)
                    BeginShipContacts();
                gravityMovedShips = true;
                var previous = e.Position;
                e.Position = MoveShip(
                    e.Position,
                    e.Position
                        + delta
                            / distance
                            * MathF.Min(distance, Definition(22).Value * s.Gravity * seconds),
                    EnemyRadius(e)
                );
                if (seconds > 0)
                {
                    var pulled = (e.Position - previous) / seconds;
                    if (pulled.LengthSquared() > .0001f)
                    {
                        var normal = Vector2.Normalize(pulled);
                        e.Velocity +=
                            normal
                            * MathF.Max(0, pulled.Length() - Vector2.Dot(e.Velocity, normal));
                    }
                }
                enemyGrid.Move(e, e.Position);
            }
        }
    }
}
