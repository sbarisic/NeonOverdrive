namespace ArcadeDodger.Multiplayer;

public sealed record SkillDefinition(
    string Id,
    string Name,
    string Description,
    string Branch,
    string Family,
    int Tier,
    int MinLevel,
    string? Prerequisite,
    string Icon,
    string Effect,
    float Value,
    int Cost = 1,
    string? Mastery = null
);

public sealed record WeaponStats(
    int Emitter,
    float Damage,
    float SplashMultiplier,
    float RadiusMultiplier,
    float Range,
    float Speed,
    float Interval,
    float ShieldMultiplier,
    int Pierce,
    float StatusPower
);

public sealed record SkillReceipt(int Sequence, string Skill, string Result);

public sealed record CreditReward(int Sequence, int Amount, bool Lucky);

public static class SkillCatalog
{
    public static readonly SkillDefinition[] Items = Build();
    private static readonly Dictionary<string, SkillDefinition> ById = Items.ToDictionary(s =>
        s.Id
    );

    public static SkillDefinition? Get(string id) => ById.GetValueOrDefault(id);

    public static int Rank(IEnumerable<string> skills, string family)
    {
        var rank = 0;
        foreach (var id in skills)
            if (Get(id) is { } skill && skill.Family == family)
                rank = Math.Max(rank, skill.Tier);
        return rank;
    }

    public static float Value(IEnumerable<string> skills, string family, float fallback = 0)
    {
        var rank = -1;
        var value = fallback;
        foreach (var id in skills)
            if (Get(id) is { } skill && skill.Family == family && skill.Tier > rank)
            {
                rank = skill.Tier;
                value = skill.Value;
            }
        return value;
    }

    public static string Migrate(string id) =>
        id switch
        {
            "phase.1" => "thruster.1",
            "longphase.1" => "thruster.2",
            "blink.1" => "vector.1",
            "blink.2" => "vector.2",
            "comet.1" => "cruise.1",
            "nova.1" => "impact.1",
            "salvage.1" => "fieldrepair.1",
            _ => id,
        };

    private static SkillDefinition[] Build()
    {
        var list = new List<SkillDefinition>();
        string[] roman = ["", "I", "II", "III", "IV", "V", "VI"];
        void Series(
            string family,
            string name,
            string branch,
            string effect,
            float[] values,
            Func<float, string> describe,
            string? parent = null
        )
        {
            var first = parent is null
                ? 2
                : Math.Max(2, list.Single(s => s.Id == parent).MinLevel + 2);
            for (var i = 0; i < values.Length; i++)
                list.Add(
                    new(
                        $"{family}.{i + 1}",
                        name + (values.Length > 1 ? " " + roman[i + 1] : ""),
                        describe(values[i]),
                        branch,
                        family,
                        i + 1,
                        first + i * 2,
                        i == 0 ? parent : $"{family}.{i}",
                        family,
                        effect,
                        values[i]
                    )
                );
        }
        Series(
            "shield",
            "Shield Capacity",
            "Defense",
            "shield",
            [1, 2, 3, 4, 5, 6],
            v => $"+{v} maximum shields in total. This tier adds and restores one shield."
        );
        Series(
            "recharge",
            "Quick Recharge",
            "Defense",
            "recharge",
            [4, 3],
            v => $"Shields begin regenerating after {v} damage-free seconds.",
            "shield.2"
        );
        Series(
            "flux",
            "Flux Recovery",
            "Defense",
            "flux",
            [1.5f, 2],
            v => $"Regenerate {v} shield points per second.",
            "recharge.2"
        );
        Series(
            "siphon",
            "Energy Siphon",
            "Defense",
            "siphon",
            [1],
            v => "Each collected XP pickup restores one shield.",
            "shield.3"
        );
        Series(
            "hull",
            "Hull Plating",
            "Defense",
            "hull",
            [1, 2, 3],
            v => $"+{v} maximum hull in total. This tier adds and restores one hull."
        );
        Series(
            "repair",
            "Living Metal",
            "Defense",
            "repair",
            [10, 5],
            v => $"After 10 damage-free seconds, regenerate one hull every {v} seconds.",
            "hull.2"
        );
        Series(
            "strength",
            "Weapon Strength",
            "Strength",
            "damage",
            [.15f, .30f, .45f, .60f, .75f],
            v => $"+{v * 100:0}% weapon damage, including splash, mines and damaging statuses."
        );
        Series(
            "dynamics",
            "Projectile Dynamics",
            "Strength",
            "speed",
            [.15f, .30f],
            v => $"+{v * 100:0}% projectile speed; lasers gain that much range instead.",
            "strength.1"
        );
        Series(
            "range",
            "Range Engineering",
            "Strength",
            "range",
            [.15f, .30f],
            v => $"+{v * 100:0}% initial weapon range, including checkpoint distances.",
            "strength.2"
        );
        Series(
            "rate",
            "Fire Rate",
            "Fire Rate",
            "rate",
            [.1f, .2f, .3f, .4f, .5f],
            v => $"+{v * 100:0}% firing frequency; continuous lasers gain equivalent DPS."
        );
        string[] masteries = ["kinetic", "laser", "rocket", "plasma"];
        string[][] descriptions =
        [
            [
                "+15% direct kinetic damage.",
                "+25% kinetic projectile speed.",
                "Kinetic shots penetrate one additional hostile.",
                "+25% additional direct kinetic damage (40% total).",
            ],
            [
                "+20% laser range.",
                "+15% laser DPS.",
                "+25% laser damage against shields.",
                "+25% additional laser DPS (40% total).",
            ],
            [
                "+20% rocket explosion radius.",
                "+25% rocket speed.",
                "+15% direct and splash rocket damage.",
                "+25% additional rocket splash damage (40% splash total).",
            ],
            [
                "+20% plasma speed.",
                "+20% plasma explosion radius.",
                "+15% direct and splash plasma damage.",
                "+25% additional direct and splash plasma damage (40% total).",
            ],
        ];
        string[][] effects =
        [
            ["direct", "speed", "pierce", "direct"],
            ["range", "direct", "shield", "direct"],
            ["radius", "speed", "direct", "splash"],
            ["speed", "radius", "direct", "direct"],
        ];
        float[][] values =
        [
            [.15f, .25f, 1, .25f],
            [.2f, .15f, .25f, .25f],
            [.2f, .25f, .15f, .25f],
            [.2f, .2f, .15f, .25f],
        ];
        int[] levels = [2, 5, 10, 15];
        for (var m = 0; m < 4; m++)
        for (var i = 0; i < 4; i++)
        {
            var key = masteries[m];
            list.Add(
                new(
                    $"{key}.{i + 1}",
                    $"{char.ToUpper(key[0]) + key[1..]} Mastery {roman[i + 1]}",
                    descriptions[m][i],
                    "Masteries",
                    key,
                    i + 1,
                    levels[i],
                    i == 0 ? null : $"{key}.{i}",
                    key,
                    effects[m][i],
                    values[m][i],
                    Mastery: key
                )
            );
        }
        Series(
            "thruster",
            "Thruster Tuning",
            "Mobility",
            "acceleration",
            [.25f, .5f],
            v => $"+{v * 100:0}% normal acceleration."
        );
        Series(
            "vector",
            "Vector Control",
            "Mobility",
            "steering",
            [.25f, .5f],
            v => $"+{v * 100:0}% counter-steering acceleration.",
            "thruster.1"
        );
        Series(
            "slipstream",
            "Slipstream",
            "Mobility",
            "movement",
            [.1f, .2f],
            v => $"+{v * 100:0}% movement speed."
        );
        Series(
            "cruise",
            "Cruise Engines",
            "Mobility",
            "movement",
            [.1f],
            v => "+10% maximum speed, additive with Slipstream.",
            "slipstream.2"
        );
        Series(
            "impact",
            "Impact Dampeners",
            "Mobility",
            "ramdefense",
            [.5f],
            v => "Take 50% less enemy ramming damage.",
            "thruster.2"
        );
        Series(
            "fortune",
            "Fortune",
            "Fortune",
            "credits",
            [.25f, .5f, 1],
            v => $"+{v * 100:0}% collected credits. Combines with Luck."
        );
        Series(
            "luck",
            "Luck",
            "Fortune",
            "luck",
            [.1f, .2f, .3f],
            v => $"{v * 100:0}% chance to double a collected credit payout."
        );
        Series(
            "collector",
            "Collector Field",
            "Collection",
            "radius",
            [30, 60],
            v => $"+{v} units of XP and credit collection radius."
        );
        Series(
            "harvest",
            "Astral Harvest",
            "Collection",
            "xp",
            [.25f, .5f],
            v => $"+{v * 100:0}% collected XP, rounded down.",
            "collector.1"
        );
        Series(
            "fieldrepair",
            "Field Repairs",
            "Collection",
            "pickuprepair",
            [.25f],
            v => "Each XP pickup restores 0.25 hull, up to maximum hull.",
            "collector.2"
        );
        Series(
            "luckyfind",
            "Lucky Find",
            "Fortune",
            "luck",
            [.35f],
            v => "Raise the chance of a doubled credit payout to 35%.",
            "luck.3"
        );
        return list.ToArray();
    }
}
