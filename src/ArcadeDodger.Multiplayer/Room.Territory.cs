using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private sealed class ControlSite
    {
        public int Id;
        public Vector2 Position;
        public bool Friendly,
            Contested;
        public int Progress;
    }

    private readonly Dictionary<int, ControlSite> territory = [];
    private float FriendlyControl =>
        territory.Count == 0
            ? .5f
            : territory.Values.Count(t => t.Friendly) / (float)territory.Count;

    private bool FriendlyStation(int id) =>
        !territory.TryGetValue(id, out var site) || site.Friendly;

    private void InitializeTerritory()
    {
        if (Sector is null)
            return;
        var near = salvageSites
            .Values.OrderBy(s =>
                Vector2.DistanceSquared(s.Location.Position, Location(10)!.Position)
            )
            .Take(2)
            .Select(s => s.Location.Id)
            .ToHashSet();
        foreach (var location in Sector.Locations.Where(l => l.Id is 11 or 12 || l.Kind == "wreck"))
        {
            var friendly = location.Kind == "station" || near.Contains(location.Id);
            territory[location.Id] = new()
            {
                Id = location.Id,
                Position = location.Position,
                Friendly = friendly,
            };
            if (enemiesEnabled && location.Kind == "station")
                for (var i = 0; i < 2 && enemies.Count < EnemyLimit; i++)
                {
                    var at = location.Position + new Vector2(i == 0 ? -240 : 240, 130);
                    enemies.Add(
                        new()
                        {
                            Id = ++nextEnemy,
                            Type = "salvage-turret",
                            Allied = true,
                            Home = location.Id,
                            Position = at,
                            Previous = at,
                            Hull = 60,
                            MaxHull = 60,
                            Level = location.Id - 9,
                            Gunner = true,
                        }
                    );
                }
            if (!friendly && enemiesEnabled && salvageSites.TryGetValue(location.Id, out var site))
                for (var slot = 0; slot < 3 && enemies.Count < EnemyLimit; slot++)
                {
                    var hull = (int)
                        MathF.Ceiling((slot == 0 ? 120 : 60) * EnemyCatalog.Health(site.Level));
                    var unit = new Enemy
                    {
                        Id = ++nextEnemy,
                        Type = slot == 0 ? "salvage-core" : "salvage-turret",
                        Allied = false,
                        Home = location.Id,
                        Position = site.Positions[slot],
                        Previous = site.Positions[slot],
                        Hull = hull,
                        MaxHull = hull,
                        Level = site.Level,
                        Gunner = slot > 0,
                    };
                    site.Units[slot] = unit;
                    enemies.Add(unit);
                }
        }
    }

    private ControlSite? TerritoryAt(Vector2 position) =>
        territory.Values.MinBy(t => Vector2.DistanceSquared(position, t.Position));

    private void StepTerritory()
    {
        foreach (var site in territory.Values)
        {
            var nearby = enemies
                .Where(e =>
                    e.Hull > 0
                    && e.SurfacePlanet == 0
                    && !e.Installation
                    && e.Role is not (NpcRole.Trader or NpcRole.Miner or NpcRole.Scavenger)
                    && Vector2.DistanceSquared(e.Position, site.Position) <= 350 * 350
                )
                .ToArray();
            var friendly =
                nearby.Any(e => e.Allied)
                || players.Values.Any(p =>
                    p.Hull > 0
                    && p.Connected
                    && p.Docked == 0
                    && !p.Editing
                    && !Racing(p)
                    && Vector2.DistanceSquared(p.Position, site.Position) <= 350 * 350
                );
            var hostile = nearby.Any(e => !e.Allied);
            site.Contested =
                friendly && hostile || site.Friendly && hostile || !site.Friendly && friendly;
            var defended =
                salvageSites.TryGetValue(site.Id, out var salvage)
                && salvage.Units.Any(e => e is { Hull: > 0 } && e.Allied == site.Friendly);
            defended |= enemies.Any(e =>
                e.Hull > 0
                && e.Home == site.Id
                && e.Type == "salvage-turret"
                && e.Allied == site.Friendly
            );
            var occupied =
                site.Id is 11 or 12 && players.Values.Any(p => p.Docked == site.Id && p.Hull > 0);
            var attacking = site.Friendly ? hostile && !friendly : friendly && !hostile;
            site.Progress = attacking && !defended && !occupied ? site.Progress + 1 : 0;
            if (site.Progress >= 30 * TickRate)
            {
                site.Friendly = !site.Friendly;
                site.Progress = 0;
                Broadcast(
                    "sector",
                    "Territory control",
                    $"{Location(site.Id)!.Name} captured by {(site.Friendly ? "allied" : "hostile")} forces."
                );
            }
        }
        foreach (
            var npc in enemies.Where(e =>
                e.Hull > 0
                && !e.Installation
                && e.SurfacePlanet == 0
                && !e.Type.StartsWith("salvage-")
                && e.Type != "repair-bot"
                && Tick - e.LastHit >= 10 * TickRate
            )
        )
            if (
                TerritoryAt(npc.Position) is { Contested: false } site
                && site.Friendly == npc.Allied
            )
                HealHull(
                    ref npc.Hull,
                    ref npc.DamageFraction,
                    npc.MaxHull,
                    MathF.Min(.5f, npc.MaxHull * .01f) / TickRate
                );
        foreach (
            var p in players.Values.Where(p => p.Hull > 0 && Tick - p.LastDamage >= 10 * TickRate)
        )
            if (TerritoryAt(p.Position) is { Friendly: true, Contested: false })
                HealHull(
                    ref p.Hull,
                    ref p.HullFraction,
                    MaxHullFor(p),
                    MathF.Min(.5f, MaxHullFor(p) * .01f) / TickRate
                );
        if (enemiesEnabled && Tick % (60 * TickRate) == 0 && enemies.Count < EnemyLimit)
        {
            var quota = 2 + (int)MathF.Ceiling(MathF.Max(0, .5f - FriendlyControl) * 12);
            if (enemies.Count(e => e.Allied && e.Role == NpcRole.Patrol && e.Hull > 0) < quota)
            {
                var level = 1 + (int)(MathF.Max(0, .5f - FriendlyControl) * 8);
                var at = Location(10)!.Position + new Vector2(300, 100);
                var hull = (int)MathF.Ceiling(12 * EnemyCatalog.Health(level));
                enemies.Add(
                    new()
                    {
                        Id = ++nextEnemy,
                        Type = "gunner",
                        Role = NpcRole.Patrol,
                        Allied = true,
                        ChassisOverride = nextEnemy % 7,
                        Hull = hull,
                        MaxHull = hull,
                        Shield = hull,
                        MaxShield = hull,
                        Home = 10,
                        Position = at,
                        Previous = at,
                        Level = level,
                    }
                );
            }
        }
    }
}
