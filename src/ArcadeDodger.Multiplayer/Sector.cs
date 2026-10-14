using System.Numerics;
using System.Text.Json.Serialization;

namespace ArcadeDodger.Multiplayer;

public sealed record SectorLocation(
    int Id,
    string Kind,
    string Name,
    float X,
    float Y,
    float Radius,
    string Biome = "",
    int Parent = 0,
    bool Solid = false,
    string Faction = "neutral",
    bool Surface = false
)
{
    [JsonIgnore]
    public Vector2 Position => new(X, Y);
}

public sealed record SectorObstacle(int Id, float X, float Y, float Radius)
{
    [JsonIgnore]
    public Vector2 Position => new(X, Y);
}

public sealed record SectorDefinition(
    int Seed,
    float Width,
    float Height,
    SectorLocation[] Locations,
    SectorObstacle[] Obstacles,
    float GravityRange = 400,
    float GravityAcceleration = 45
)
{
    public const float DefaultWidth = 24000,
        DefaultHeight = 16000;

    public static SectorDefinition Generate(int seed)
    {
        var rng = new Random(seed);
        var locations = new List<SectorLocation>();
        var obstacles = new List<SectorObstacle>();
        // A jittered route skeleton guarantees reachable destinations and reproducible geography.
        Vector2 J(float x, float y) =>
            new(2 * x + rng.Next(-160, 161), 2 * y + rng.Next(-160, 161));
        void Add(
            int id,
            string kind,
            string name,
            Vector2 p,
            float radius,
            string biome = "",
            int parent = 0,
            bool solid = false,
            string faction = "neutral",
            bool surface = false
        ) =>
            locations.Add(
                new(id, kind, name, p.X, p.Y, radius, biome, parent, solid, faction, surface)
            );
        var planets = new[] { J(2400, 2100), J(5600, 1900), J(9000, 3300), J(6900, 6100) };
        var names = new[] { "Haven", "Cinder", "Zephyr", "The Sundering" };
        var biomes = new[] { "inhabited", "volcanic", "gas", "shattered" };
        for (var i = 0; i < 4; i++)
            Add(
                1 + i,
                "planet",
                names[i],
                planets[i],
                i == 2 ? 760 : 1160,
                biomes[i],
                solid: i != 2
            );
        Add(10, "station", "Arrival Port", new(2400, 3600), 90);
        Add(11, "station", "Engineering Station", new(8400, 8400), 90);
        Add(12, "station", "Frontier Outpost", new(18000, 12000), 90);
        for (var i = 0; i < 4; i++)
            Add(
                20 + i,
                "base",
                i == 3 ? "The Sundering Raid Factory" : names[i] + " Hostile Array",
                planets[i] + Vector2.Normalize(new Vector2(650, 350)) * (i == 2 ? 1050 : 1247),
                85,
                "",
                i + 1,
                faction: "hostile",
                surface: i != 2
            );
        for (var i = 0; i < 4; i++)
            if (i != 2)
                Add(
                    80 + i,
                    "outpost",
                    names[i] + " Allied Outpost",
                    planets[i] - Vector2.Normalize(new Vector2(650, 350)) * 1247,
                    85,
                    parent: i + 1,
                    faction: "friendly",
                    surface: true
                );
        Add(30, "corridor", "North Arrival Corridor", new(15000, 180), 110);
        Add(31, "corridor", "East Arrival Corridor", new(23820, 10000), 110);
        for (var i = 0; i < 6; i++)
            Add(
                40 + i,
                "wreck",
                "Salvage Wreck " + (i + 1),
                Vector2.Lerp(planets[i % 4], planets[(i + 1) % 4], .4f)
                    + new Vector2(0, 400 + i * 50),
                45
            );
        for (var i = 0; i < 3; i++)
            Add(
                50 + i,
                "beacon",
                "Orbital Survey " + (i + 1),
                planets[i + 1] + Vector2.Normalize(new Vector2(-500, -400)) * 1650,
                24
            );
        Add(
            60,
            "thermal",
            "Cinder Thermal Vent",
            planets[1] + Vector2.Normalize(new Vector2(-250, 200)) * 1650,
            170
        );
        var clusters = new[]
        {
            new Vector2(3000, 3200),
            new(4300, 900),
            new(6300, 3000),
            new(10200, 2200),
            new(10200, 4700),
            new(7400, 7000),
            new(5600, 6600),
            new(5800, 5300),
        };
        clusters = clusters.Select(p => p * 2).ToArray();
        // Keep all routes between interactable destinations clear, not only the nearest route.
        var nodes = locations
            .Where(l => l.Kind is "station" or "base" or "wreck" or "beacon" or "corridor")
            .ToArray();
        for (var c = 0; c < clusters.Length; c++)
        {
            Add(70 + c, "cluster", "Asteroid Field " + (c + 1), clusters[c], 330);
            var accepted = 0;
            for (var n = 0; n < 240 && accepted < (c >= 5 ? 12 : 7); n++)
            {
                var angle = (float)rng.NextDouble() * MathF.Tau;
                var p =
                    clusters[c]
                    + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * rng.Next(70, 370);
                var radius = rng.Next(28, 64);
                if (
                    locations.Any(l =>
                        l.Kind != "cluster"
                        && Vector2.Distance(p, l.Position)
                            < l.Radius + radius + (l.Solid ? 400 : 130)
                    )
                )
                    continue;
                if (
                    nodes.Any(a =>
                        nodes.Any(b =>
                            a.Id < b.Id
                            && SectorCollision.DistanceToSegment(p, a.Position, b.Position)
                                < radius + 55
                        )
                    )
                )
                    continue;
                if (obstacles.Any(o => Vector2.Distance(o.Position, p) < o.Radius + radius + 15))
                    continue;
                obstacles.Add(new(obstacles.Count + 1, p.X, p.Y, radius));
                accepted++;
            }
        }
        var geometryDefinition = new SectorDefinition(
            seed,
            DefaultWidth,
            DefaultHeight,
            locations.ToArray(),
            obstacles.ToArray()
        );
        var collision = new SectorCollision(geometryDefinition);
        var routes = new SectorNavigation(geometryDefinition, collision);
        var arrival = locations.Single(l => l.Id == 10).Position + new Vector2(140, 0);
        for (var c = 0; c < clusters.Length; c++)
        {
            var chosen = new List<Vector2>();
            for (var n = 0; chosen.Count < 3 && n < 300; n++)
            {
                var angle = n * 2.399963f;
                var at =
                    clusters[c] + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (90 + n * 3);
                if (
                    collision.TraceShips(at, at, 35) is not null
                    || chosen.Any(p => Vector2.Distance(p, at) < 100)
                    || routes.Route(arrival, at, Room.Radius).Length == 0
                )
                    continue;
                chosen.Add(at);
                var level = locations
                    .Where(l => l.Kind == "planet")
                    .MinBy(l => Vector2.DistanceSquared(l.Position, at))!
                    .Id;
                Add(
                    100 + c * 3 + chosen.Count - 1,
                    "cache",
                    $"Field {c + 1} Cache {chosen.Count}",
                    at,
                    16,
                    parent: level
                );
            }
            if (chosen.Count != 3)
                throw new InvalidOperationException("Unable to place asteroid caches");
        }
        return new(seed, DefaultWidth, DefaultHeight, locations.ToArray(), obstacles.ToArray());
    }
}
