using System.Collections.Immutable;
using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    public RoomSnapshot Snapshot() =>
        new(
            ClientProtocol.Version,
            "snapshot",
            Tick,
            players
                .Values.Select(p => new PlayerSnapshot(
                    p.Id,
                    p.Name,
                    p.Position.X,
                    p.Position.Y,
                    p.Aim,
                    p.Hull,
                    p.Shield,
                    p.Ack,
                    p.Editing || Safe(p) || Tick < p.ProtectedUntil,
                    p.Connected,
                    p.Velocity.X,
                    p.Experience,
                    LevelForExperience(p.Experience),
                    p.Velocity.Y,
                    AccelerationFor(p) * (p.Cruise ? 2 : 1),
                    SteeringFor(p) * (p.Cruise ? 2 : 1),
                    p.Life,
                    SkillPointsFor(p),
                    p.Skills.Order().ToImmutableArray(),
                    MaxShieldFor(p),
                    WeaponFor(p),
                    MaxHullFor(p),
                    MoveSpeedFor(p) * CruiseMultiplier(p) * (Living(p).Tow == 0 ? 1 : .6f),
                    p.Menu || RaceCountdown(p),
                    p.Editing,
                    p.Weapons,
                    p.EquipSequence,
                    p.EquipAccepted,
                    Enumerable.Range(0, 4).Select(i => Unlocked(p, i)).ToArray(),
                    Enumerable.Range(1, 4).Select(e => Stats(p, e)).ToArray(),
                    p.Docked,
                    p.RunId,
                    Detection(p),
                    Survey(p),
                    p.Heading,
                    p.Throttle,
                    profiles.ReplayCountFor(p.Name) + 1,
                    p.Chassis,
                    TurnRateFor(p),
                    Enumerable
                        .Range(0, 4)
                        .Aggregate(0, (mask, bay) => mask | (Active(p, bay) ? 1 << bay : 0)),
                    p.Cruise ? "active"
                        : p.CruiseAt > 0 ? "spooling"
                        : Tick < p.CruiseReady ? "cooldown"
                        : "ready",
                    (int)Math.Max(0, (p.CruiseAt > 0 ? p.CruiseAt : p.CruiseReady) - Tick),
                    p.Connected
                        && p.Hull > 0
                        && !p.Menu
                        && !p.Editing
                        && p.Docked == 0
                        && !RaceCountdown(p)
                        && p.Held?.Brake == true,
                    CruiseMultiplier(p),
                    1 / (1 + .1f * Gear(p, 3)),
                    p.Connected
                        && p.Hull > 0
                        && !p.Menu
                        && !p.Editing
                        && p.Docked == 0
                        && !RaceCountdown(p)
                        && p.Held is { FullThrottle: true, Brake: false }
                ))
                .ToArray(),
            shots
                .Select(s => new ShotSnapshot(
                    s.Id,
                    s.Owner,
                    s.Position.X,
                    s.Position.Y,
                    s.Velocity.X,
                    s.Velocity.Y,
                    s.Born,
                    s.Kind,
                    Effects(s),
                    s.Program is not null,
                    s.Parent,
                    s.BirthPosition.X,
                    s.BirthPosition.Y,
                    s.Ancestors.Length > 0 ? s.Ancestors : null,
                    s.Weapon,
                    s.Cursor,
                    s.VisualPower
                ))
                .ToArray(),
            enemies
                .Select(e => new EnemySnapshot(
                    e.Id,
                    e.Installation ? "installation" : e.Type,
                    e.Position.X,
                    e.Position.Y,
                    e.Aim,
                    e.Hull,
                    e.Gunner
                        && e.State == "engage"
                        && e.FireAt > Tick
                        && e.FireAt - Tick <= (e.Type == "sniper" ? 30 : 15),
                    (Tick < e.BurnUntil ? 1 : 0)
                        | (Tick < e.RadiationUntil ? 2 : 0)
                        | (Tick < e.SlowUntil ? 4 : 0)
                        | (Tick < e.CorrosionUntil ? 8 : 0)
                        | (Tick < e.StunUntil ? 16 : 0)
                        | (Tick < e.MarkUntil ? 32 : 0),
                    e.Home,
                    e.State,
                    e.Level,
                    EnemyRadius(e),
                    e.MaxHull,
                    e.Faction.ToString().ToLowerInvariant(),
                    e.Shield,
                    e.MaxShield,
                    e.SurfacePlanet,
                    e.Chassis,
                    e.Role.ToString().ToLowerInvariant(),
                    e.SurfacePlanet != 0 ? e.Aim : e.Heading,
                    e.Velocity.X,
                    e.Velocity.Y,
                    e.Throttle
                ))
                .ToArray(),
            energy
                .Select(e => new EnergySnapshot(e.Id, e.Position.X, e.Position.Y, e.Value, e.Owner))
                .ToArray(),
            explosions.ToArray(),
            beams.ToArray(),
            Credits: credits
                .Select(c => new CreditSnapshot(c.Id, c.Position.X, c.Position.Y, c.Value, c.Owner))
                .ToArray(),
            Mines: mines
                .Select(m => new MineSnapshot(
                    m.Id,
                    m.Payload.Owner,
                    m.Payload.Position.X,
                    m.Payload.Position.Y,
                    Tick >= m.ArmedAt,
                    m.Payload.VisualPower
                ))
                .ToArray(),
            ModuleDrops: moduleDrops
                .Select(m => new ModuleDropSnapshot(
                    m.Id,
                    m.Position.X,
                    m.Position.Y,
                    m.Module,
                    m.Owner,
                    m.Weapon is not null ? "weapon"
                        : m.Gear is not null ? "gear"
                        : "module",
                    m.Weapon?.Tier ?? m.Gear?.Tier ?? 0,
                    m.Gear?.Kind ?? -1
                ))
                .ToArray(),
            CombatEvents: combatEvents.Where(e => Tick - e.Born <= 8).ToArray()
        );
}
