namespace ArcadeDodger.Multiplayer;

public sealed record EnemyDefinition(
    string Id,
    string Name,
    int MinimumLevel,
    float Radius,
    int Hull,
    float Speed,
    string Description
);

public static class EnemyCatalog
{
    public static readonly EnemyDefinition[] Items =
    [
        new(
            "contact",
            "Independent Trader",
            1,
            24,
            120,
            100,
            "Hail for conversation, trade or work."
        ),
        new("salvage-core", "Salvage Hub", 1, 32, 120, 0, "Shared service outpost."),
        new("salvage-turret", "Defense Turret", 1, 24, 60, 0, "Defends a salvage outpost."),
        new("repair-bot", "Repair Drone Bay", 1, 24, 60, 0, "Repairs nearby allies."),
        new("turret", "Surface Turret", 1, 24, 6, 0, "Fixed outward surface defense."),
        new("rover", "Surface Rover", 1, 20, 3, 100, "Patrols the planetary rim."),
        new("relief", "Relief Cruiser", 1, 32, 240, 100, "Friendly relief vessel."),
        new("rammer", "Rammer", 1, 20, 3, 145, "Closes in for a collision attack."),
        new("gunner", "Gunner", 1, 20, 3, 100, "Keeps distance and fires single rounds."),
        new(
            "interceptor",
            "Interceptor",
            2,
            17,
            3,
            210,
            "Flanking two-shot attack runs followed by a retreat."
        ),
        new(
            "sniper",
            "Siege Sniper",
            3,
            23,
            4,
            75,
            "A one-second warning precedes a powerful long-range shot."
        ),
        new(
            "tender",
            "Repair Tender",
            2,
            25,
            5,
            80,
            "Repairs another nearby ship through an unobstructed support link."
        ),
    ];

    public static EnemyDefinition Get(string id) =>
        Items.FirstOrDefault(e => e.Id == id) ?? Items[0];

    public static float Size(int level) =>
        level switch
        {
            1 => .65f,
            2 => .8f,
            3 => .95f,
            _ => 1.1f,
        };

    public static float Health(int level) =>
        level switch
        {
            1 => .6f,
            2 => 1,
            3 => 1.5f,
            _ => 2,
        };

    public static float Damage(int level) => Math.Clamp(level, 1, 4) * .25f;

    public static int Region(int home) =>
        home switch
        {
            20 => 1,
            21 => 2,
            22 or 30 => 3,
            23 or 31 => 4,
            _ => 1,
        };
}
