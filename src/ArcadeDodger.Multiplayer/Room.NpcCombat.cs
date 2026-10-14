using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private readonly SectorNavigation? navigation,
        spaceNavigation;
    private readonly SectorCollision? spaceGeometry;
    private int npcRouteWork = 2048;

    private Vector2 NpcMuzzle(Enemy npc)
    {
        var point =
            npc.Position
            + ChassisCatalog.Rotate(
                ChassisCatalog.Get(npc.Chassis).Hardpoints[0] * EnemyCatalog.Size(npc.Level),
                npc.Heading
            );
        if (npc.SurfacePlanet != 0 || npc.Type.StartsWith("salvage-"))
            point = npc.Position + new Vector2(MathF.Cos(npc.Aim), MathF.Sin(npc.Aim)) * 20;
        return geometry?.TraceShips(npc.Position, point, 3) is { } hit
            ? Vector2.Lerp(npc.Position, point, MathF.Max(0, hit.T - .001f))
            : point;
    }

    private bool CanHit(Shot shot, Enemy target)
    {
        if (shot.EnforcementTarget != 0)
            return false;
        if (shot.Owner > 0 && players.TryGetValue(shot.Owner, out var pilot))
        {
            if (Racing(pilot))
                return false;
            if (target.Role == NpcRole.Police && Living(pilot).Police == "wanted")
                return true;
        }
        return shot.Owner > 0 || shot.FriendlyNpc ? !target.Allied : target.Allied;
    }

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
        if (npc.YieldPoint is { } yieldPoint)
        {
            if (
                Tick < npc.YieldUntil
                && Vector2.Distance(npc.Position, yieldPoint) > 25
                && geometry?.TraceShips(npc.Position, yieldPoint, EnemyRadius(npc)) is null
            )
                return yieldPoint;
            npc.YieldPoint = null;
            npc.RepathAt = 0;
        }
        goal = Exterior(goal, EnemyRadius(npc) + 100);
        var openSpace =
            geometry is not null
            && geometry.GravityAt(npc.Position) == Vector2.Zero
            && geometry.GravityAt(goal) == Vector2.Zero;
        var routeGeometry = openSpace ? spaceGeometry ?? geometry : geometry;
        var router =
            openSpace && !npc.RouteGravityFallback ? spaceNavigation ?? navigation : navigation;
        if (
            routeGeometry is null
            || routeGeometry.TraceShips(npc.Position, goal, EnemyRadius(npc)) is null
        )
        {
            npc.RouteSearch?.Dispose();
            npc.RouteSearch = null;
            return goal;
        }
        if (Vector2.DistanceSquared(npc.RouteGoal, goal) > 300 * 300)
        {
            npc.RouteSearch?.Dispose();
            npc.RouteSearch = null;
            npc.RepathAt = 0;
            npc.RouteGravityFallback = false;
        }
        if (npc.RouteSearch is null && Tick >= npc.RepathAt)
        {
            npc.RouteSearch = router?.Begin(npc.Position, goal, EnemyRadius(npc));
            npc.RouteGoal = goal;
        }
        if (npc.RouteSearch is { } search && npcRouteWork > 0)
        {
            var before = search.WorkItems;
            search.Advance(Math.Min(128, npcRouteWork));
            npcRouteWork -= search.WorkItems - before;
            if (search.Complete)
            {
                npc.Route = search.Path;
                npc.RouteIndex = 0;
                npc.RouteSearch = null;
                npc.RepathAt = Tick + (npc.Route.Length == 0 ? 30 : 300);
                if (npc.Route.Length == 0 && openSpace && !npc.RouteGravityFallback)
                {
                    // Preference must not make a physically reachable narrow corridor impassable.
                    npc.RouteGravityFallback = true;
                    npc.RepathAt = Tick + 1;
                }
            }
        }
        while (
            npc.RouteIndex < npc.Route.Length
            && Vector2.Distance(npc.Position, npc.Route[npc.RouteIndex]) < 25
        )
            npc.RouteIndex++;
        if (npc.RouteIndex < npc.Route.Length)
        {
            var point = npc.Route[npc.RouteIndex];
            if (geometry?.TraceShips(npc.Position, point, EnemyRadius(npc)) is null)
                return point;
            npc.RepathAt = Math.Min(npc.RepathAt, Tick + 1);
        }
        if (npc.BlockedRoutes > 0 && npc.RouteSearch is null)
        {
            // A clear position can lack a visible coarse-grid connector near a dock.
            // Fly to a nearby clear approach before asking the grid for another route.
            var facing = MathF.Atan2(goal.Y - npc.Position.Y, goal.X - npc.Position.X);
            for (var i = 0; i < 12; i++)
            {
                var angle = facing + (i % 2 == 0 ? 1 : -1) * (i / 2 + 1) * MathF.PI / 6;
                var point = npc.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 240;
                if (
                    geometry?.TraceShips(npc.Position, point, EnemyRadius(npc)) is not null
                    || point.X < EnemyRadius(npc)
                    || point.Y < EnemyRadius(npc)
                    || point.X > Width - EnemyRadius(npc)
                    || point.Y > Height - EnemyRadius(npc)
                    || enemies.Any(e =>
                        e != npc
                        && e.Hull > 0
                        && Vector2.Distance(point, e.Position)
                            < EnemyRadius(e) + EnemyRadius(npc) + 10
                    )
                )
                    continue;
                npc.YieldPoint = point;
                npc.YieldUntil = Tick + 5 * TickRate;
                return point;
            }
        }
        return npc.Position;
    }

    private void InitializeAllies()
    {
        if (Sector is null || !enemiesEnabled)
            return;
        foreach (var location in Sector.Locations.Where(l => l.Kind == "outpost"))
        {
            sources[location.Id] = new() { Location = location, NextLaunch = 600 };
            AddEnemy(
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
        AddEnemy(
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
        if (
            MoveWorldMissionNpc(npc)
            || MoveCivilian(npc)
            || MoveContact(npc)
            || ReturnFromRaid(npc)
        )
            return true;
        // Tenders support their own faction; seeing a relief ship is not attacking it.
        if (npc.Type == "tender")
        {
            npc.NpcTarget = 0;
            return false;
        }
        // Keep the broadcast's designated group in the battle. Ordinary acquisition
        // would let them retarget another outpost or abandon the ship behind cover.
        var assignedShip =
            rescue is { State: "active" } mission
            && mission.Attackers.Contains(npc.Id)
            && mission.Ship.Hull > 0
                ? mission.Ship
                : ProtectionTarget(npc) ?? RaidTarget(npc);
        var valuableCargo =
            npc.Role == NpcRole.Pirate
                ? civilians
                    .Where(c =>
                        c.Ship.Hull > 0
                        && (c.Stock.Count > 0 || c.Cargo != 0)
                        && Vector2.DistanceSquared(c.Ship.Position, npc.Position) < 1800 * 1800
                    )
                    .OrderByDescending(c => c.Stock.Sum(s => s.Price))
                    .ThenBy(c => Vector2.DistanceSquared(c.Ship.Position, npc.Position))
                    .FirstOrDefault()
                    ?.Ship
                : null;
        var opponent =
            assignedShip
            ?? valuableCargo
            ?? enemyGrid
                .Query(npc.Position, npc.Position, 1000)
                .Where(e => EligibleNpcOpponent(npc, e))
                .MinBy(e => Vector2.DistanceSquared(e.Position, npc.Position));
        var previousTarget = npc.NpcTarget;
        npc.NpcTarget = opponent?.Id ?? 0;
        if (previousTarget != npc.NpcTarget && npc.NpcTarget != 0)
            npc.FireAt = Math.Max(npc.FireAt, Tick + (npc.Type == "sniper" ? 30 : 15));
        if (npc.Type == "relief" && npc.Patrol > 0)
            opponent = null;
        if (
            !npc.Allied
            && npc.SurfacePlanet == 0
            && opponent is null
            && !npc.Type.StartsWith("salvage-")
            && npc.Type != "repair-bot"
        )
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
        else if (npc.Type is not ("salvage-core" or "salvage-turret" or "repair-bot"))
        {
            Vector2 goal;
            if (npc.Type == "relief" && rescue is not { State: "active" })
            {
                // Shipping traffic keeps its route while defending itself. Stopping at
                // gunner range lets a small skirmish pin the ship before a rescue can form.
                goal =
                    npc.Patrol == 0
                        ? Location(npc.Home)!.Position + new Vector2(650, 0)
                        : Location(npc.Home == 22 ? 12 : 11)!.Position + new Vector2(200, 0);
                if (
                    Vector2.Distance(npc.Position, Exterior(goal)) < 120
                    && npc.Velocity.LengthSquared() < 100
                )
                {
                    npc.Patrol++;
                    if (npc.Patrol >= 2)
                        npc.State = "arrived";
                }
            }
            else if (target.HasValue)
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
                if (
                    Vector2.Distance(npc.Position, Exterior(goal)) < 120
                    && npc.Velocity.LengthSquared() < 100
                )
                {
                    npc.Patrol++;
                    if (npc.Patrol >= 2)
                        npc.State = "arrived";
                }
            }
            else
                goal = npc.Position;
            FlyNpc(npc, goal, EnemyCatalog.Get(npc.Type).Speed, target.HasValue ? 0 : 35);
        }
        if (
            target is { } aimAt
            && npc.Type is not ("rammer" or "tender" or "salvage-core" or "repair-bot")
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
                    Position = NpcMuzzle(npc),
                    BirthPosition = NpcMuzzle(npc),
                    Range = npc.Type == "sniper" ? 2160 : 840,
                    Velocity = direction * (npc.Type == "sniper" ? 650 : 300) / TickRate,
                    Damage = EnemyCatalog.Damage(npc.Level) * (npc.Type == "sniper" ? 2 : 1),
                    Born = Tick,
                    Expires = long.MaxValue / 2,
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
