using System.Collections.Immutable;
using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    public RoomSnapshot ForViewer(RoomSnapshot frame, int id)
    {
        if (Sector is null || !players.TryGetValue(id, out var p))
            return frame;
        var interest = MathF.Max(1400, Detection(p) + 200);
        bool Near(float x, float y) =>
            Vector2.DistanceSquared(p.Position, new(x, y)) <= interest * interest;
        return frame with
        {
            Players = frame
                .Players.Where(other =>
                    other.Id == id
                    || Vector2.DistanceSquared(p.Position, new(other.X, other.Y))
                        <= Detection(p) * Detection(p)
                )
                .Select(other =>
                    other.Id == id
                        ? other
                        : other with
                        {
                            Weapons = System
                                .Collections
                                .Immutable
                                .ImmutableArray<WeaponConfig>
                                .Empty,
                            WeaponStats = null,
                            UnlockedWeapons = null,
                            Skills = System.Collections.Immutable.ImmutableArray<string>.Empty,
                        }
                )
                .ToArray(),
            Enemies = frame
                .Enemies?.Where(e =>
                    Vector2.DistanceSquared(p.Position, new(e.X, e.Y))
                    <= Detection(p) * Detection(p)
                )
                .ToArray(),
            Shots = frame.Shots.Where(e => e.Owner == id || Near(e.X, e.Y)).ToArray(),
            Beams = frame
                .Beams?.Where(e =>
                    e.Owner == id
                    || SectorCollision.DistanceToSegment(
                        p.Position,
                        new(e.X, e.Y),
                        new(e.EndX, e.EndY)
                    ) < interest
                )
                .ToArray(),
            ModuleDrops = frame.ModuleDrops?.Where(e => e.Owner == id && Near(e.X, e.Y)).ToArray(),
            Energy = frame
                .Energy?.Where(e => (e.Owner == 0 || e.Owner == id) && Near(e.X, e.Y))
                .ToArray(),
            Credits = frame
                .Credits?.Where(e => (e.Owner == 0 || e.Owner == id) && Near(e.X, e.Y))
                .ToArray(),
            Mines = frame.Mines?.Where(e => Near(e.X, e.Y)).ToArray(),
            CombatEvents = frame.CombatEvents?.Where(e => Near(e.X, e.Y)).ToArray(),
            Explosions = frame.Explosions?.Where(e => Near(e.X, e.Y)).ToArray(),
        };
    }

    public SectorState? SectorSnapshot(int player)
    {
        if (Sector is null)
            return null;
        SectorSessionSnapshot? pilot = null;
        if (players.TryGetValue(player, out var p))
        {
            var s = ExplorationFor(p);
            pilot = new(
                p.Docked,
                s.LastStation,
                s.Discovered.Order().ToArray(),
                s.Cells.Order().ToArray(),
                s.Contract,
                s.Progress.Order().ToArray(),
                s.Carrying,
                s.Ready,
                s.Completed.Order().ToArray(),
                s.InteractionTarget == 0 ? 0 : Math.Clamp((Tick - s.InteractionStart) / 90f, 0, 1),
                s.Receipt,
                (MaxHullFor(p) - p.Hull + (p.HullFraction > 0 ? 1 : 0)) * 10,
                pings
                    .Where(g =>
                        g.Expires > Tick
                        && (g.Sender == p.Id || g.Broadcast || friends.Mutual(p.Name, g.Name))
                    )
                    .ToArray(),
                Tick < p.LootNoticeUntil ? p.LootNotice : "",
                s.ContractCooldown.Where(c => c.Value > Tick).Select(c => c.Key).ToArray(),
                enemies
                    .Where(e =>
                        e.Installation
                        && e.Contributors.TryGetValue(p.Id, out var contribution)
                        && contribution.Run == p.RunId
                        && contribution.Damage >= 1
                    )
                    .Select(e => e.Home)
                    .ToArray(),
                InteractionTarget: s.InteractionTarget,
                Receipts: s.Receipts.Values.Concat(s.Rejected is null ? [] : new[] { s.Rejected })
                    .OrderBy(r => r.Cursor)
                    .ToArray()
            );
        }
        return new(
            sources
                .Values.Select(s => new LocationState(
                    s.Location.Id,
                    s.Location.Kind == "corridor"
                        ? 0
                        : enemies
                            .FirstOrDefault(e => e.Installation && e.Home == s.Location.Id)
                            ?.Hull
                            ?? 0,
                    s.Destroyed ? "destroyed"
                        : s.Warning > 0 ? "launching"
                        : "active",
                    s.Warning > 0 ? (int)Math.Max(0, s.NextLaunch - Tick) : 0
                ))
                .ToArray(),
            pilot,
            RescueState(),
            players.TryGetValue(player, out var viewer) ? ServicesFor(viewer) : null,
            viewer is not null ? LivingState(viewer) : null
        );
    }
}
