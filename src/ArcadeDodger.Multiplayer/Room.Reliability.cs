using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private bool TryLocalSpawn(Vector2 desired, float radius, out Vector2 position)
    {
        for (var ring = 0; ring <= 8; ring++)
        for (var i = 0; i < (ring == 0 ? 1 : 24); i++)
        {
            var angle = i * MathF.Tau / 24;
            var at =
                desired
                + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ring * (radius * 2 + 10);
            if (
                at.X < radius
                || at.Y < radius
                || at.X > Width - radius
                || at.Y > Height - radius
                || geometry?.TraceShips(at, at, radius) is not null
                || enemies.Any(e =>
                    e.Hull > 0 && Vector2.Distance(e.Position, at) < radius + EnemyRadius(e) + 5
                )
                || players.Values.Any(p =>
                    p.Hull > 0
                    && Vector2.Distance(p.Position, at)
                        < radius + ChassisCatalog.Get(p.Chassis).Radius + 5
                )
            )
                continue;
            position = at;
            return true;
        }
        position = default;
        return false;
    }

    private void RechargeNpc(Enemy npc)
    {
        if (
            !npc.Allied
            || npc.Hull <= 0
            || Tick - npc.LastHit < 8 * TickRate
            || Tick % TickRate != 0
        )
            return;
        if (npc.Shield < npc.MaxShield || npc.ShieldFraction > 0)
        {
            npc.Shield = Math.Min(npc.MaxShield, npc.Shield + 1);
            if (npc.Shield == npc.MaxShield)
                npc.ShieldFraction = 0;
        }
    }

    private bool EligibleNpcOpponent(Enemy npc, Enemy target, float range = 1000) =>
        target.Hull > 0
        && target.Allied != npc.Allied
        && !Sanctuary(target.Position)
        && Vector2.DistanceSquared(target.Position, npc.Position) < range * range
        && VisibleLine(npc.Position, target.Position);

    private Enemy? ProtectionTarget(Enemy attacker)
    {
        foreach (var pilot in players.Values)
        {
            var job = Living(pilot);
            if (
                job.Job != "protection"
                || job.JobState != "active"
                || !job.Targets.Skip(1).Contains(attacker.Id)
            )
                continue;
            return enemies.FirstOrDefault(e => e.Id == job.Targets[0] && e.Hull > 0);
        }
        return null;
    }

    private bool MissionContact(Enemy e) =>
        e.Allied
        && e.Hull > 0
        && (
            contacts.Any(c => c.Ship == e)
            || civilians.Any(c =>
                c.Ship == e
                && e.Role
                    is NpcRole.Trader
                        or NpcRole.Miner
                        or NpcRole.Scavenger
                        or NpcRole.Mercenary
                        or NpcRole.Courier
            )
        );

    private bool CanTurnIn(Player p) =>
        DeliveryStation(p.Docked)
        || enemies.Any(e =>
            MissionContact(e)
            && Vector2.Distance(p.Position, e.Position) < 180
            && ServiceLine(p.Position, e.Position)
        );

    private bool DeliveryStation(int id) =>
        Location(id)?.Kind == "station"
        && FriendlyStation(id)
        && (!territory.TryGetValue(id, out var control) || !control.Contested);

    private int DeliveryDestination(Player p, int exclude = 0) =>
        Sector!
            .Locations.Where(l => l.Id != exclude && DeliveryStation(l.Id))
            .OrderBy(l => Vector2.DistanceSquared(p.Position, l.Position))
            .ThenBy(l => l.Id)
            .Select(l => l.Id)
            .FirstOrDefault();

    private bool PatrolCivilian(Civilian agent)
    {
        if (agent.Ship.Role is not (NpcRole.Patrol or NpcRole.Police))
            return false;
        if (
            !territory.TryGetValue(agent.Destination, out var site)
            || !site.Friendly
            || site.Contested
        )
        {
            site = territory
                .Values.Where(t => t.Friendly && !t.Contested)
                .OrderBy(t => agent.Visited.GetValueOrDefault(t.Id))
                .ThenBy(t => t.Id)
                .FirstOrDefault();
            if (site is null)
                return false;
            agent.Destination = site.Id;
        }
        var point = Exterior(site.Position + new Vector2(220, 160));
        if (Vector2.Distance(agent.Ship.Position, point) < 80 && agent.Ship.Velocity.Length() < 10)
        {
            agent.Visited[site.Id] = Tick;
            agent.Destination = 0;
        }
        agent.Ship.State = "traveling";
        FlyNpc(agent.Ship, point, 100, 40);
        return true;
    }
}
