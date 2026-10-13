namespace ArcadeDodger.Multiplayer;

/// <summary>Stable component keys encode a catalog kind and one of four levels.</summary>
public static class ModuleLevels
{
    public const int StockSize = 128;

    public static int Kind(int component) => component % 32;

    public static int Level(int component) => component / 32 + 1;

    public static int Key(int kind, int level) => kind + 32 * (level - 1);

    public static bool Valid(int component) =>
        component > 0 && component < StockSize && Kind(component) is >= 1 and <= 28;

    public static bool IsMod(int component) => Valid(component) && Kind(component) >= 5;

    public static bool IsEmitter(int component) => Valid(component) && Kind(component) <= 4;

    public static float Bonus(int level) => 1 + .25f * (level - 1);

    public static int Roll(int enemyLevel, float roll) =>
        enemyLevel > 4
            ? 4
            : Math.Clamp(
                enemyLevel
                    - (
                        roll < .6f ? 0
                        : roll < .9f ? 1
                        : 2
                    ),
                1,
                4
            );

    public static ModuleDefinition Resolve(int component) => Cache.Items[component];

    public static ModuleDefinition[] Catalog => Cache.Items.Values.OrderBy(d => d.Id).ToArray();

    private static class Cache
    {
        internal static readonly Dictionary<int, ModuleDefinition> Items = Build();

        private static Dictionary<int, ModuleDefinition> Build()
        {
            var result = new Dictionary<int, ModuleDefinition>();
            foreach (var d in ModuleCatalog.Items)
                for (var level = 1; level <= 4; level++)
                {
                    var extra = level - 1;
                    var multiplier = Bonus(level);
                    var value = d.Id switch
                    {
                        5 or 6 or 7 or 10 or 28 => 1 + (d.Value - 1) * multiplier,
                        8 or 15 => d.Value + extra,
                        9 or 18 => MathF.Max(.1f, 1 - (1 - d.Value) * multiplier),
                        14 => d.Value * (1 + .5f * extra),
                        17 or 19 or 20 or 22 or 23 or 27 => d.Value * multiplier,
                        _ => d.Value,
                    };
                    var radius = d.Id switch
                    {
                        14 => d.Radius + 100 * extra,
                        16 or 19 or 22 or 23 or 24 or 26 => d.Radius * multiplier,
                        _ => d.Radius,
                    };
                    var key = Key(d.Id, level);
                    result[key] = d with
                    {
                        Id = key,
                        BaseId = d.Id,
                        Level = level,
                        Name =
                            d.Name
                            + (level > 1 ? $" {new[] { "I", "II", "III", "IV" }[level - 1]}" : ""),
                        Price = d.Price * level,
                        Value = value,
                        Radius = radius,
                        Description =
                            level == 1 ? d.Description : Describe(d, level, value, radius),
                        LaserDescription =
                            level == 1 ? d.LaserDescription : Describe(d, level, value, radius),
                    };
                }
            return result;
        }
    }

    private static string Describe(ModuleDefinition d, int level, float value, float radius)
    {
        var effect = d.Id switch
        {
            <= 4 => $"Base damage x{1 + .15f * (level - 1):0.##}; original range and cadence.",
            5 => $"Downstream damage x{value:0.##}.",
            6 => $"Projectile speed x{value:0.##}; lasers gain the same range multiplier.",
            7 => $"Range x{value:0.##}; checkpoint distances stay fixed.",
            8 => $"Pass through {value:0} additional contacts.",
            9 => $"Subsequent branch angles x{value:0.###}.",
            10 => $"Shield damage x{value:0.##}; hull damage unchanged.",
            11 or 12 or 13 =>
                $"{value:0} branches sharing damage; downstream range +{10 * (level - 1)}%."
                    + (d.Id == 13 ? " Bloom then halves remaining range." : ""),
            14 => $"Steering {value:0.###}; acquisition radius {radius:0} units.",
            15 => $"{value:0} additional cover reflections.",
            16 => $"Chain to a target within {radius:0} units; {value * 100:0}% damage retained.",
            17 or 19 => $"{value:0.##} damage per second for {d.Seconds:0.#} seconds."
                + (d.Id == 19 ? $" Radiation radius {radius:0}." : ""),
            18 => $"Enemy movement x{value:0.###} for {d.Seconds:0.#} seconds.",
            20 => $"Incoming enemy damage +{value * 100:0.##}% for {d.Seconds:0.#} seconds.",
            21 =>
                $"Charge gain x{Bonus(level):0.##}; four charge stuns for 0.3s, one-second retrigger limit.",
            22 => $"Pull at {value:0.##} units/s within {radius:0} units.",
            23 => $"Impact blast: {value * 100:0.##}% damage within {radius:0} units.",
            24 => $"Proximity trigger {radius:0} units.",
            25 =>
                $"Convert to plasma, preserving damage; conversion splash radius x{Bonus(level):0.##}.",
            26 => $"Terminal mine; blast radius {radius:0} units. Arms in 0.5s; expires after 10s.",
            27 => $"Recover {value * 100:0.##}% of actual damage as shields.",
            28 => $"Marked credit drops x{value:0.###} for five seconds; does not stack.",
            _ => d.Description,
        };
        return $"Level {level}. {effect}";
    }
}
