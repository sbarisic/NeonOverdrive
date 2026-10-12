using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    public const int ModuleCount = 29,
        WeaponSlots = 8;

    private Shot CreateModularShot(Player p, float aim, int weapon = 0)
    {
        var w = p.Weapons[weapon];
        var kind = w.Emitter;
        var stats = Stats(p, kind);
        var range = stats.Range;
        var speed = stats.Speed;
        return new Shot
        {
            Id = ++nextShot,
            Owner = p.Id,
            OwnerName = p.Name,
            Weapon = weapon,
            Position = p.Position,
            BirthPosition = p.Position,
            Born = Tick,
            Expires =
                Tick
                + Math.Max(
                    180,
                    (int)
                        MathF.Ceiling(
                            range
                                * MathF.Pow(1.3f, w.Attachments.Count(id => id == 7))
                                / MathF.Min(speed, 520)
                                * TickRate
                        ) + 30
                ),
            Kind = kind,
            Program = (int[])w.Attachments.Clone(),
            Range = range,
            InitialRange = range,
            Velocity = new Vector2(MathF.Cos(aim), MathF.Sin(aim)) * speed / TickRate,
            SpeedMultiplier =
                speed
                / (
                    kind == 3 ? 430
                    : kind == 4 ? 520
                    : kind == 1 ? 1300
                    : 650
                ),
            Damage = stats.Damage,
            VisualPower =
                stats.Damage
                / (
                    kind == 2 ? 4
                    : kind >= 3 ? 2
                    : 1
                ),
            Blast =
                (
                    kind == 3 ? 85
                    : kind == 4 ? 45
                    : 0
                ) * stats.RadiusMultiplier,
            RadiusMultiplier = stats.RadiusMultiplier,
            SplashMultiplier = stats.SplashMultiplier,
            ShieldMultiplier = stats.ShieldMultiplier,
            Pierce = stats.Pierce,
            StatusPower = stats.StatusPower,
        };
    }

    private static float Gate(Shot s) =>
        s.Program is not null && s.Cursor < 8
            ? s.InitialRange * (s.Cursor + 1) * .05f
            : float.PositiveInfinity;

    private static int Effects(Shot s) =>
        (s.Burns > 0 ? 1 : 0)
        | (s.Rads > 0 ? 2 : 0)
        | (s.Freezes > 0 ? 4 : 0)
        | (s.Corrodes > 0 ? 8 : 0)
        | (s.Teslas > 0 ? 16 : 0);

    private void StepProjectiles()
    {
        var count = shots.Count;
        for (var index = 0; index < count && index < shots.Count; index++)
        {
            var s = shots[index];
            if (s.Expires <= Tick)
                continue;
            Guide(s);
            Advance(s, 1, 0, int.MaxValue);
        }
    }

    // Budget is the remaining fraction of this tick for moving projectiles; beams trace immediately.
    private static float ProjectileRadius(Shot s) => s.Owner > 0 && s.Kind == 1 ? 1.5f : 3;

    private void Advance(Shot s, float budget, int firstSegment, int segmentLimit)
    {
        for (var guard = 0; guard < 96 && budget > .00001f && s.Expires > Tick; guard++)
        {
            if (s.IsBeam && beams.Count - firstSegment >= segmentLimit)
                return;
            while (s.Program is not null && s.Cursor < 8 && s.Program[s.Cursor] == 0)
                s.Cursor++;
            if (s.Program is not null && s.Distance >= s.Range - .001f)
            {
                if (!s.IsBeam)
                    Explode(s, s.Position, null, null);
                s.Expires = Tick;
                return;
            }
            if (s.Distance + .001f >= Gate(s))
            {
                var module = s.Program![s.Cursor++];
                if (ApplyStage(s, module, budget, firstSegment, segmentLimit))
                    return;
                continue;
            }
            var speed = s.Velocity.Length();
            if (speed < .00001f)
            {
                foreach (var c in Contacts(s, s.Position, s.Position))
                {
                    Contact(s, c.Pilot, c.Alien, s.Position);
                    if (s.Pierce-- <= 0)
                    {
                        s.Expires = Tick;
                        break;
                    }
                }
                return;
            }
            var direction = s.Velocity / speed;
            var coverRadius = s.IsBeam ? 0 : ProjectileRadius(s);
            var wallX =
                direction.X > .00001f ? (Width - coverRadius - s.Position.X) / direction.X
                : direction.X < -.00001f ? (coverRadius - s.Position.X) / direction.X
                : float.PositiveInfinity;
            var wallY =
                direction.Y > .00001f ? (Height - coverRadius - s.Position.Y) / direction.Y
                : direction.Y < -.00001f ? (coverRadius - s.Position.Y) / direction.Y
                : float.PositiveInfinity;
            var wall = MathF.Max(0, MathF.Min(wallX, wallY));
            var length = MathF.Min(
                s.IsBeam ? float.PositiveInfinity : budget * speed,
                MathF.Min(
                    wall,
                    MathF.Min(
                        Gate(s) - s.Distance,
                        s.Program is null ? float.PositiveInfinity : s.Range - s.Distance
                    )
                )
            );
            length = MathF.Max(0, length);
            var start = s.Position;
            var end = start + direction * length;
            var cover = geometry?.Trace(start, end, coverRadius);
            float? sanctuary = null;
            if (Sector is not null)
                foreach (var station in Sector.Locations.Where(l => l.Kind == "station"))
                    if (
                        SectorCollision.Circle(
                            start - station.Position,
                            end - station.Position,
                            300
                        )
                            is { } t
                        && (sanctuary is null || t < sanctuary)
                    )
                        sanctuary = t;
            var boundary = cover?.T ?? 2;
            var absorbed = sanctuary.HasValue && sanctuary <= boundary;
            if (absorbed)
                boundary = sanctuary!.Value;
            if (boundary <= 1)
            {
                end = Vector2.Lerp(start, end, boundary);
                length *= boundary;
            }
            var contacts = Contacts(
                s,
                start,
                end,
                s.IsBeam ? 1 : 1 - budget,
                s.IsBeam ? 1 : MathF.Min(1, 1 - budget + length / speed)
            );
            var stopped = false;
            foreach (var c in contacts)
            {
                if (c.Pilot is { Hull: <= 0 } || c.Alien is { Hull: <= 0 })
                    continue;
                var at = Vector2.Lerp(start, end, c.Time);
                Contact(s, c.Pilot, c.Alien, at);
                if (c.Pilot is not null)
                    s.HitPlayers.Add(c.Pilot.Id);
                if (c.Alien is not null)
                    s.HitEnemies.Add(c.Alien.Id);
                if (s.Proximity || s.Pierce-- <= 0)
                {
                    end = at;
                    stopped = true;
                    break;
                }
            }
            var travelled = Vector2.Distance(start, end);
            if (s.IsBeam)
                beams.Add(
                    new(
                        s.Owner,
                        beams.Count - firstSegment,
                        start.X,
                        start.Y,
                        end.X,
                        end.Y,
                        Effects(s),
                        s.Weapon,
                        s.Cursor,
                        s.VisualPower
                    )
                );
            else
                Pull(s, end, travelled / speed / TickRate);
            s.Position = end;
            s.Distance += travelled;
            if (!s.IsBeam)
                budget -= travelled / speed;
            if (stopped)
            {
                s.Impact = true;
                if (s.IsBeam)
                    Pull(s, end, 1f / TickRate);
                s.Expires = Tick;
                return;
            }
            if (boundary <= 1)
            {
                if (absorbed)
                {
                    s.Expires = Tick;
                    return;
                }
                if (s.Bounces-- <= 0)
                {
                    s.Impact = true;
                    if (!s.IsBeam)
                        Explode(s, end, null, null);
                    s.Expires = Tick;
                    return;
                }
                s.Velocity = Vector2.Reflect(s.Velocity, cover!.Value.Normal);
                s.Position += cover.Value.Normal * .03f;
                continue;
            }
            if (length >= wall - .001f)
            {
                if (s.Bounces-- <= 0)
                {
                    s.Impact = true;
                    if (!s.IsBeam)
                        Explode(s, end, null, null);
                    s.Expires = Tick;
                    return;
                }
                if (wallX <= wall + .001f)
                    s.Velocity.X = -s.Velocity.X;
                if (wallY <= wall + .001f)
                    s.Velocity.Y = -s.Velocity.Y;
            }
            if (s.IsBeam && s.Distance >= s.Range - .001f)
                return;
            if (length < .00001f && wall > .001f)
                return;
        }
    }
}

public sealed record ExplosionSnapshot(int Id, float X, float Y, float Radius, long Born);
