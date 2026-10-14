using System.Numerics;
using ArcadeDodger.Multiplayer;
using Raylib_cs;

namespace ArcadeDodger.Web;

internal sealed partial class BrowserArenaWorld
{
    private void Observe(RoomSnapshot frame, int ownId, Vector2 origin, float time, bool low)
    {
        bool initial = tick < 0;
        // Suppress stale transition bursts after a connection stall / room switch.
        bool transitions = !initial && frame.Tick - tick <= Room.TickRate;
        shotTransitions = transitions; // Keep initial/stall suppression for all samples of this tick.
        if (transitions)
        {
            foreach (var p in frame.Players)
            {
                if (!players.TryGetValue(p.Id, out var old))
                    continue;
                Vector2 position = new(p.X, p.Y);
                Color color = PlayerColor(p);
                if (p.Hull > 0)
                {
                    if (p.Hull < old.Hull || p.Shield < old.Shield)
                        Add(
                            position,
                            Vector2.Zero,
                            p.Shield < old.Shield ? Shield : color,
                            time,
                            .22f,
                            EffectKind.Hit,
                            p.Id,
                            origin
                        );
                    float distance = Vector2.DistanceSquared(position, new(old.X, old.Y));
                    // No trails spanning teleports/respawns; low FX has no trails at all.
                    if (!low && old.Hull > 0 && distance > 4 && distance < 160 * 160)
                        Add(
                            new(old.X, old.Y),
                            position - new Vector2(old.X, old.Y),
                            color,
                            time,
                            .20f,
                            EffectKind.Trail,
                            p.Id,
                            origin
                        );
                }
            }
            var own = frame.Players.FirstOrDefault(p => p.Id == ownId);
            if (
                own is not null
                && players.TryGetValue(ownId, out var previousPilot)
                && (own.Docked != previousPilot.Docked || own.Experience > previousPilot.Experience)
            )
                Add(
                    new(own.X, own.Y),
                    Vector2.Zero,
                    Cyan,
                    time,
                    .5f,
                    EffectKind.Hit,
                    ownId,
                    origin
                );
            foreach (var e in frame.Enemies ?? [])
            {
                if (enemies.TryGetValue(e.Id, out var old) && e.Hull < old.Hull)
                {
                    Add(
                        new(e.X, e.Y),
                        Vector2.Zero,
                        EnemyColor(e),
                        time,
                        .22f,
                        EffectKind.Hit,
                        e.Id,
                        origin
                    );
                    if (
                        !low
                        && e.Hull > 0
                        && (enemyHitFlashes.ContainsKey(e.Id) || enemyHitFlashes.Count < 256)
                    )
                        enemyHitFlashes[e.Id] = time + EnemyHitFlashDuration;
                }
            }
        }
        players.Clear();
        foreach (var p in frame.Players.Take(Room.Capacity))
            players[p.Id] = p;
        enemies.Clear();
        foreach (var e in (frame.Enemies ?? []).Take(256))
            enemies[e.Id] = e;
        tick = frame.Tick; // Damage/death transitions remain tick-deduplicated.
    }

    private void ObserveShots(RoomSnapshot frame, int ownId, Vector2 origin, float time, bool low)
    {
        // Playback birth-gates shots: new IDs can appear without a new authoritative tick.
        // Remember each sampled ID even when FX are suppressed, so it cannot flash later.
        if (shotTransitions)
        {
            foreach (var s in frame.Shots.Take(1024))
            {
                if (shots.Contains(s.Id) || low || s.Parent > 0)
                    continue;
                Vector2 muzzle = new(s.BirthX, s.BirthY);
                Add(
                    muzzle,
                    Direction(new(s.Dx, s.Dy)),
                    ProjectileColor(s),
                    time,
                    .12f,
                    EffectKind.Muzzle,
                    s.Id,
                    origin
                );
            }
        }
        shots.Clear();
        foreach (var s in frame.Shots.Take(1024))
            shots.Add(s.Id);
    }

    private void Add(
        Vector2 p,
        Vector2 d,
        Color color,
        float time,
        float duration,
        EffectKind kind,
        int seed,
        Vector2 origin
    )
    {
        if (effects.Count >= EffectCapacity || !Visible(p, origin, 100))
            return;
        effects.Add(new(p, d, color, time, duration, kind, seed));
    }

    private void Reset()
    {
        tick = -1;
        lastCombatEvent = 0;
        shotTransitions = false;
        effects.Clear();
        fragments.Clear();
        enemyHitFlashes.Clear();
        players.Clear();
        enemies.Clear();
        shots.Clear();
    }
}
