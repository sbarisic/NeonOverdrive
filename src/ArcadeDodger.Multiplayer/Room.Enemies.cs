using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed record EnemySnapshot(
    int Id,
    string Kind,
    float X,
    float Y,
    float Aim,
    int Hull,
    bool Telegraph,
    int Effects = 0,
    int Home = 0,
    string State = "patrol",
    int Level = 1,
    float Radius = 20,
    int MaxHull = 3,
    string Faction = "hostile",
    int Shield = 0,
    int MaxShield = 0,
    int SurfacePlanet = 0,
    int Chassis = 0,
    string Role = "combat",
    float Heading = 0,
    float VelocityX = 0,
    float VelocityY = 0,
    float Throttle = 0
)
{
    public string Activity =>
        State switch
        {
            "engage" or "fighting" => "fighting",
            "return" or "fleeing" => "fleeing",
            "arrived" or "docking" or "contact" => "docking",
            "guard" or "working" => "working",
            "repairing" => "repairing",
            _ => "traveling",
        };
}

public sealed partial class Room
{
    public const int EnemyCapacity = 192;

    private static float EnemyRadius(Enemy e) =>
        e.Installation ? 85
        : e.SurfacePlanet != 0 || e.Type.StartsWith("salvage-") || e.Type == "repair-bot"
            ? EnemyCatalog.Get(e.Type).Radius * EnemyCatalog.Size(e.Level)
        : ChassisCatalog.Get(e.Chassis).Radius * EnemyCatalog.Size(e.Level);

    private readonly List<Enemy> enemies = [];
    private int nextEnemy;

    private void StepEnemies()
    {
        npcRouteWork = 2048;
        StepSources();
        var alive = players
            .Values.Where(p => p.Hull > 0 && p.Connected && !p.Editing && !Safe(p))
            .ToArray();
        foreach (var enemy in enemies)
        {
            enemy.Previous = enemy.Position;
            enemy.FlightGoal = null;
            RechargeNpc(enemy);
            if (enemy.Installation || enemy.Hull <= 0 || Tick < enemy.StunUntil)
                continue;
            if (StepNpcCombat(enemy))
                continue;
            var home = Location(enemy.Home)?.Position ?? enemy.Position;
            var route = home;
            if (Location(enemy.Home)?.Kind == "corridor")
                route = Location(enemy.Home == 30 ? 22 : 23)!.Position;
            var points = PatrolRoute(enemy.Home);
            bool InTerritory(Vector2 pos) =>
                Enumerable
                    .Range(0, 4)
                    .Any(i =>
                        SectorCollision.DistanceToSegment(pos, points[i], points[(i + 1) % 4])
                        <= 1200
                    );
            var target = playerGrid
                .Query(enemy.Position, enemy.Position, 700)
                .Where(p => alive.Contains(p))
                .Where(p =>
                    Vector2.DistanceSquared(p.Position, enemy.Position) <= 700 * 700
                    && InTerritory(p.Position)
                    && VisibleLine(enemy.Position, p.Position)
                )
                .OrderBy(p => Vector2.DistanceSquared(p.Position, enemy.Position))
                .FirstOrDefault();
            if (
                enemy.Target > 0
                && alive.FirstOrDefault(p => p.Id == enemy.Target) is { } tracked
                && InTerritory(tracked.Position)
                && Vector2.Distance(tracked.Position, enemy.Position) < 1200
                && VisibleLine(enemy.Position, tracked.Position)
            )
                target = tracked;
            var launching = Tick < enemy.LaunchUntil;
            if (launching)
                target = null;
            Vector2 goal;
            if (launching)
            {
                enemy.Target = 0;
                enemy.State = "launch";
                goal = points[enemy.Patrol % 4];
            }
            else if (target is not null)
            {
                if (enemy.Target != target.Id)
                    enemy.FireAt = Math.Max(
                        enemy.FireAt,
                        Tick + (enemy.Type == "sniper" ? 30 : 15)
                    );
                enemy.Target = target.Id;
                enemy.State = "engage";
                goal = target.Position;
            }
            else if (Tick < enemy.InvestigateUntil && InTerritory(enemy.Investigate))
            {
                enemy.Target = 0;
                enemy.State = "investigate";
                goal = enemy.Investigate;
            }
            else
            {
                enemy.Target = 0;
                goal = points[enemy.Patrol % 4];
                enemy.State = Vector2.Distance(enemy.Position, goal) > 700 ? "return" : "patrol";
                if (Vector2.Distance(enemy.Position, goal) < 60)
                    enemy.Patrol = (enemy.Patrol + 1) % 4;
            }
            var delta = goal - enemy.Position;
            var distance = delta.Length();
            var direction = distance > .001f ? delta / distance : Vector2.UnitX;
            enemy.Aim = MathF.Atan2(direction.Y, direction.X);
            var outer = enemy.Type == "sniper" ? 580 : 320;
            var inner = enemy.Type == "sniper" ? 460 : 220;
            var movement =
                target is not null && enemy.Gunner
                    ? (
                        distance > outer ? 1
                        : distance < inner ? -1
                        : 0
                    )
                    : 1;
            if (enemy.Type == "interceptor" && target is not null)
            {
                if (Tick < enemy.RetreatUntil)
                    direction = -direction;
                else
                    direction = Vector2.Normalize(
                        direction + new Vector2(-direction.Y, direction.X) * .65f
                    );
                movement = 1;
            }
            if (enemy.Type == "tender")
            {
                var ally = enemyGrid
                    .Query(enemy.Position, enemy.Position, 250)
                    .Where(e =>
                        e != enemy
                        && e.Allied == enemy.Allied
                        && !e.Installation
                        && e.Hull > 0
                        && e.Hull - e.DamageFraction < e.MaxHull
                        && Vector2.DistanceSquared(e.Position, enemy.Position) <= 250 * 250
                        && VisibleLine(enemy.Position, e.Position)
                    )
                    .OrderBy(e => e.Hull - e.DamageFraction)
                    .FirstOrDefault();
                enemy.SupportTarget = ally?.Id ?? 0;
                if (ally is not null)
                {
                    ally.DamageFraction -= .5f / TickRate;
                    while (ally.DamageFraction < 0 && ally.Hull < ally.MaxHull)
                    {
                        ally.Hull++;
                        ally.DamageFraction += 1;
                    }
                    if (ally.Hull >= ally.MaxHull)
                        ally.DamageFraction = Math.Max(0, ally.DamageFraction);
                    movement = 0;
                }
            }
            var flightGoal =
                movement == 0 ? enemy.Position
                : movement < 0 || enemy.Type == "interceptor" && target is not null
                    ? enemy.Position + direction * (movement < 0 ? -400 : 400)
                : goal;
            FlyNpc(enemy, flightGoal, EnemyCatalog.Get(enemy.Type).Speed);
            if (target is null)
                continue;
            if (
                enemy.Gunner
                && enemy.Type != "tender"
                && distance < (enemy.Type == "sniper" ? 850 : 650)
                && Tick >= enemy.FireAt
                && Tick >= enemy.RetreatUntil
                && VisibleLine(enemy.Position, target.Position)
            )
            {
                var aim = Vector2.Normalize(target.Position - enemy.Position);
                if (shots.Count < ProjectileLimit)
                    shots.Add(
                        new()
                        {
                            Id = ++nextShot,
                            Owner = -enemy.Id,
                            Position = NpcMuzzle(enemy),
                            BirthPosition = NpcMuzzle(enemy),
                            Range = enemy.Type == "sniper" ? 2160 : 840,
                            Velocity = aim * ((enemy.Type == "sniper" ? 650 : 300f) / TickRate),
                            Damage =
                                EnemyCatalog.Damage(enemy.Level) * (enemy.Type == "sniper" ? 2 : 1),
                            Born = Tick,
                            Expires = long.MaxValue / 2,
                        }
                    );
                if (enemy.Type == "interceptor" && enemy.Burst == 0)
                {
                    enemy.Burst = 1;
                    enemy.FireAt = Tick + 6;
                }
                else
                {
                    enemy.Burst = 0;
                    enemy.FireAt = Tick + (enemy.Type == "sniper" ? 120 : 75);
                    if (enemy.Type == "interceptor")
                        enemy.RetreatUntil = Tick + 45;
                }
            }
        }
    }

    private sealed class Enemy
    {
        public bool Allied;
        public bool Neutral;
        public NpcFaction Faction
        {
            get =>
                Neutral ? NpcFaction.Neutral
                : Allied ? NpcFaction.Friendly
                : NpcFaction.Hostile;
            set
            {
                Neutral = value == NpcFaction.Neutral;
                Allied = value != NpcFaction.Hostile;
            }
        }
        public NpcRole Role = NpcRole.Combat;
        public int ChassisOverride = -1;
        public float MarkValue = 1.25f;
        public int Chassis =>
            ChassisOverride >= 0
                ? ChassisOverride
                : Type switch
                {
                    "interceptor" => 1,
                    "gunner" => 2,
                    "contact" => 3,
                    "tender" => 4,
                    "sniper" => 5,
                    "relief" => 6,
                    _ => 0,
                };
        public int SurfacePlanet,
            NpcTarget,
            Shield,
            MaxShield;
        public float ShieldFraction,
            SurfaceAngle;
        public int SurfaceDirection = 1;
        public long LastHit,
            RepathAt;
        public int CourierDestination = 12;
        public long CourierDepartAt;
        public Vector2 RouteGoal;
        public Vector2 ProgressPosition;
        public long ProgressAt;
        public int BlockedRoutes;
        public Vector2? YieldPoint;
        public long YieldUntil;
        public bool RouteGravityFallback;
        public Vector2[] Route = [];
        public SectorNavigation.Search? RouteSearch;
        public int RouteIndex;
        private string? type;
        public string Type
        {
            get => type ?? (Gunner ? "gunner" : "rammer");
            set => type = value;
        }
        public int Level = 1,
            MaxHull = 3,
            Burst,
            SupportTarget;
        public long RetreatUntil;
        public readonly Dictionary<int, (string Run, float Damage)> Contributors = [];
        public int Id,
            Hull = 3,
            Home,
            Target,
            Patrol;
        public bool Gunner,
            Installation;
        public string State = "guard";
        public Vector2 Position,
            Previous,
            Investigate;
        public float Aim,
            Heading,
            Throttle;
        public Vector2 Velocity;
        public Vector2? FlightGoal;
        public float FlightSpeed,
            FlightStop;

        public long LaunchUntil,
            InvestigateUntil,
            FireAt,
            BurnUntil,
            RadiationUntil,
            SlowUntil;
        public Shot? BurnSource,
            RadiationSource;
        public float DamageFraction,
            Charge,
            BurnPower,
            RadPower,
            Corrosion,
            SlowFactor = 1;
        public long CorrosionUntil,
            StunUntil,
            StunReady,
            MarkUntil;
    }
}
