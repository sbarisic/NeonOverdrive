using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private readonly SectorNavigation? navigation;

    private static bool CanHit(Shot shot, Enemy target) =>
        shot.Owner > 0 || shot.FriendlyNpc ? !target.Allied : target.Allied;

    private Vector2 Exterior(Vector2 goal, float clearance = 80)
    {
        if (Sector is null)
            return goal;
        foreach (var planet in Sector.Locations.Where(l => l.Solid))
        {
            var offset = goal - planet.Position;
            if (offset.Length() < planet.Radius + clearance)
                goal =
                    planet.Position
                    + (offset.LengthSquared() < 1 ? Vector2.UnitX : Vector2.Normalize(offset))
                        * (planet.Radius + clearance);
        }
        return Vector2.Clamp(goal, new(clearance), new(Width - clearance, Height - clearance));
    }

    private Vector2 Navigate(Enemy npc, Vector2 goal)
    {
        goal = Exterior(goal, EnemyRadius(npc) + 100);
        if (geometry is null || geometry.TraceShips(npc.Position, goal, EnemyRadius(npc)) is null)
            return goal;
        if (Tick >= npc.RepathAt || Vector2.DistanceSquared(npc.RouteGoal, goal) > 300 * 300)
        {
            npc.Route = navigation?.Route(npc.Position, goal, EnemyRadius(npc)) ?? [];
            npc.RouteIndex = 0;
            npc.RouteGoal = goal;
            npc.RepathAt = Tick + 300;
        }
        while (
            npc.RouteIndex < npc.Route.Length
            && Vector2.Distance(npc.Position, npc.Route[npc.RouteIndex]) < 25
        )
            npc.RouteIndex++;
        return npc.RouteIndex < npc.Route.Length ? npc.Route[npc.RouteIndex] : npc.Position;
    }

    private void InitializeAllies()
    {
        if (Sector is null || !enemiesEnabled)
            return;
        foreach (var location in Sector.Locations.Where(l => l.Kind == "outpost"))
        {
            sources[location.Id] = new() { Location = location, NextLaunch = 600 };
            enemies.Add(
                new()
                {
                    Id = ++nextEnemy,
                    Installation = true,
                    Allied = true,
                    Home = location.Id,
                    Position = location.Position,
                    Previous = location.Position,
                    Hull = 120,
                    MaxHull = 120,
                    Level = location.Parent,
                }
            );
        }
        foreach (var location in Sector.Locations.Where(l => l.Surface))
            for (var index = 0; index < 4; index++)
                LaunchSurface(location, index < 2, index);
    }

    private void LaunchSurface(SectorLocation home, bool turret, int index)
    {
        if (enemies.Count >= EnemyLimit || Location(home.Parent) is not { } planet)
            return;
        var level = planet.Id;
        var hull = (int)MathF.Ceiling(EnemyCatalog.Health(level) * (turret ? 6 : 3));
        var angle = MathF.Atan2(home.Y - planet.Y, home.X - planet.X) + (index - 1.5f) * .18f;
        var position =
            planet.Position
            + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (planet.Radius + 34);
        enemies.Add(
            new()
            {
                Id = ++nextEnemy,
                Type = turret ? "turret" : "rover",
                Gunner = true,
                Home = home.Id,
                Level = level,
                Hull = hull,
                MaxHull = hull,
                Allied = home.Faction == "friendly",
                Shield = home.Faction == "friendly" ? hull : 0,
                MaxShield = home.Faction == "friendly" ? hull : 0,
                SurfacePlanet = planet.Id,
                SurfaceAngle = angle,
                SurfaceDirection = index % 2 == 0 ? 1 : -1,
                Position = position,
                Previous = position,
                FireAt = Tick + 60,
            }
        );
    }

    private bool StepNpcCombat(Enemy npc)
    {
        // Tenders support their own faction; seeing a relief ship is not attacking it.
        if (npc.Type == "tender")
        {
            npc.NpcTarget = 0;
            return false;
        }
        if (
            npc.Allied
            && (npc.Shield < npc.MaxShield || npc.ShieldFraction > 0)
            && Tick - npc.LastHit >= 8 * TickRate
            && Tick % TickRate == 0
        )
        {
            npc.Shield = Math.Min(npc.MaxShield, npc.Shield + 1);
            if (npc.Shield == npc.MaxShield)
                npc.ShieldFraction = 0;
        }
        // Keep the broadcast's designated group in the battle. Ordinary acquisition
        // would let them retarget another outpost or abandon the ship behind cover.
        var assignedShip =
            rescue is { State: "active" } mission
            && mission.Attackers.Contains(npc.Id)
            && mission.Ship.Hull > 0
                ? mission.Ship
                : null;
        var opponent =
            assignedShip
            ?? enemyGrid
                .Query(npc.Position, npc.Position, 1000)
                .Where(e =>
                    e.Hull > 0
                    && e.Allied != npc.Allied
                    && !Sanctuary(e.Position)
                    && Vector2.DistanceSquared(e.Position, npc.Position) < 1000 * 1000
                    && VisibleLine(npc.Position, e.Position)
                )
                .MinBy(e => Vector2.DistanceSquared(e.Position, npc.Position));
        var previousTarget = npc.NpcTarget;
        npc.NpcTarget = opponent?.Id ?? 0;
        if (previousTarget != npc.NpcTarget && npc.NpcTarget != 0)
            npc.FireAt = Math.Max(npc.FireAt, Tick + (npc.Type == "sniper" ? 30 : 15));
        if (npc.Type == "relief" && npc.Patrol > 0)
            opponent = null;
        if (!npc.Allied && npc.SurfacePlanet == 0 && opponent is null)
            return false;
        npc.Target = 0;
        Player? pilot = null;
        if (!npc.Allied && opponent is null)
            pilot = players
                .Values.Where(p =>
                    p.Hull > 0
                    && p.Connected
                    && !p.Editing
                    && !Safe(p)
                    && Vector2.DistanceSquared(p.Position, npc.Position) < 700 * 700
                    && VisibleLine(npc.Position, p.Position)
                )
                .MinBy(p => Vector2.DistanceSquared(p.Position, npc.Position));
        var target = opponent?.Position ?? pilot?.Position;
        npc.State = target.HasValue ? "engage" : "patrol";
        if (npc.SurfacePlanet != 0 && Location(npc.SurfacePlanet) is { } planet)
        {
            if (npc.Type == "rover" && !target.HasValue)
            {
                npc.SurfaceAngle +=
                    100f
                    / TickRate
                    / (planet.Radius + 34)
                    * npc.SurfaceDirection
                    * (Tick < npc.SlowUntil ? npc.SlowFactor : 1);
                var next =
                    planet.Position
                    + new Vector2(MathF.Cos(npc.SurfaceAngle), MathF.Sin(npc.SurfaceAngle))
                        * (planet.Radius + 34);
                // Reverse at outposts instead of walking through structures.
                if (geometry?.TraceShips(npc.Position, next, EnemyRadius(npc)) is null)
                    npc.Position = next;
                else
                {
                    npc.SurfaceDirection *= -1;
                    npc.SurfaceAngle = MathF.Atan2(
                        npc.Position.Y - planet.Y,
                        npc.Position.X - planet.X
                    );
                }
            }
            npc.Aim = MathF.Atan2(npc.Position.Y - planet.Y, npc.Position.X - planet.X);
        }
        else
        {
            Vector2 goal;
            if (target.HasValue)
            {
                var offset = target.Value - npc.Position;
                var distance = offset.Length();
                var direction = distance > .001f ? offset / distance : Vector2.UnitX;
                var outer =
                    npc.Type == "sniper" ? 580
                    : npc.Type == "rammer" ? 0
                    : 320;
                var inner =
                    npc.Type == "sniper" ? 460
                    : npc.Type == "rammer" ? 0
                    : 220;
                goal =
                    distance > outer ? target.Value
                    : distance < inner ? npc.Position - direction * 100
                    : npc.Position;
                if (npc.Type == "interceptor")
                    goal =
                        npc.Position
                        + (
                            Tick < npc.RetreatUntil
                                ? -direction
                                : Vector2.Normalize(
                                    direction + new Vector2(-direction.Y, direction.X) * .65f
                                )
                        ) * 200;
            }
            else if (npc.Type == "relief" && !target.HasValue)
            {
                goal =
                    npc.Patrol == 0
                        ? Location(npc.Home)!.Position + new Vector2(650, 0)
                        : Location(npc.Home == 22 ? 12 : 11)!.Position;
                if (Vector2.Distance(npc.Position, Exterior(goal)) < 120)
                {
                    npc.Patrol++;
                    if (npc.Patrol >= 2)
                        npc.State = "arrived";
                }
            }
            else
                goal = npc.Position;
            var delta = Navigate(npc, goal) - npc.Position;
            if (delta.LengthSquared() > 1)
            {
                npc.Aim = MathF.Atan2(delta.Y, delta.X);
                var next = MoveShip(
                    npc.Position,
                    npc.Position
                        + Vector2.Normalize(delta)
                            * EnemyCatalog.Get(npc.Type).Speed
                            / TickRate
                            * (Tick < npc.SlowUntil ? npc.SlowFactor : 1),
                    EnemyRadius(npc)
                );
                if (npc.Allied || !Sanctuary(next))
                    npc.Position = next;
            }
        }
        if (
            npc.Type == "rammer"
            && opponent is not null
            && Tick >= npc.FireAt
            && Intersection(
                npc.Previous - opponent.Previous,
                npc.Position - opponent.Position,
                EnemyRadius(npc) + EnemyRadius(opponent)
            ).HasValue
        )
        {
            Hit(new Shot { Owner = -npc.Id }, null, opponent, EnemyCatalog.Damage(npc.Level));
            npc.FireAt = Tick + 30;
        }
        if (
            target is { } aimAt
            && npc.Type is not ("rammer" or "tender")
            && Vector2.Distance(npc.Position, aimAt) < (npc.Type == "sniper" ? 850 : 650)
            && VisibleLine(npc.Position, aimAt)
            && Tick >= npc.FireAt
            && Tick >= npc.RetreatUntil
            && shots.Count < ProjectileLimit
        )
        {
            var direction = Vector2.Normalize(aimAt - npc.Position);
            npc.Aim = MathF.Atan2(direction.Y, direction.X);
            shots.Add(
                new()
                {
                    Id = ++nextShot,
                    Owner = -npc.Id,
                    FriendlyNpc = npc.Allied,
                    Position = npc.Position,
                    BirthPosition = npc.Position,
                    Velocity = direction * (npc.Type == "sniper" ? 720 : 280) / TickRate,
                    Damage = EnemyCatalog.Damage(npc.Level) * (npc.Type == "sniper" ? 2 : 1),
                    Born = Tick,
                    Expires = Tick + 90,
                }
            );
            if (npc.Type == "interceptor" && npc.Burst == 0)
            {
                npc.Burst = 1;
                npc.FireAt = Tick + 6;
            }
            else
            {
                npc.Burst = 0;
                npc.FireAt = Tick + (npc.Type == "sniper" ? 120 : 75);
                if (npc.Type == "interceptor")
                    npc.RetreatUntil = Tick + 45;
            }
        }
        return true;
    }
}
