namespace ArcadeDodger.Multiplayer;

public sealed record WeaponItem(string Id, int Emitter, int[] Attachments, int Tier = 0)
{
    public WeaponItem Copy() => this with { Attachments = (int[])Attachments.Clone() };
}

public sealed record ShipItem(string Id, int Kind, int Tier);

public sealed record WeaponBay(string? Weapon, bool Enabled);

public sealed record EquipmentState(
    long Revision,
    WeaponItem[] Weapons,
    ShipItem[] Gear,
    int[] Loose,
    WeaponBay[] Bays,
    string?[] Ship
)
{
    public EquipmentState Copy() =>
        this with
        {
            Weapons = Weapons.Select(w => w.Copy()).ToArray(),
            Gear = [.. Gear],
            Loose = (int[])Loose.Clone(),
            Bays = [.. Bays],
            Ship = [.. Ship],
        };

    public static EquipmentState Migrate(PilotProfile p)
    {
        if (p.Equipment is { } existing)
        {
            if (existing.Loose.Length == ModuleLevels.StockSize)
                return existing;
            var stock = new int[ModuleLevels.StockSize];
            Array.Copy(existing.Loose, stock, existing.Loose.Length);
            return existing with { Loose = stock };
        }
        var loose = (int[])p.Owned.Clone();
        var items = new List<WeaponItem>();
        var bays = new List<WeaponBay>();
        foreach (var w in p.Weapons)
        {
            var id = w.Emitter > 0 ? Guid.NewGuid().ToString("N") : null;
            if (id is not null)
            {
                items.Add(new(id, w.Emitter, [.. w.Attachments]));
                loose[w.Emitter]--;
                foreach (var part in w.Attachments.Where(i => i > 0))
                    loose[part]--;
            }
            bays.Add(new(id, w.Enabled));
        }
        for (var emitter = 1; emitter <= 4; emitter++)
        {
            for (var i = 0; i < loose[emitter]; i++)
                items.Add(new(Guid.NewGuid().ToString("N"), emitter, new int[8]));
            loose[emitter] = 0;
        }
        var expanded = new int[ModuleLevels.StockSize];
        Array.Copy(loose, expanded, loose.Length);
        return new(0, items.ToArray(), [], expanded, bays.ToArray(), new string?[6]);
    }

    // Legacy fields remain bounded simulation projections, never inventory authority.
    public PilotProfile Project(PilotProfile p)
    {
        var owned = new int[29];
        for (var i = 1; i < Loose.Length; i++)
            if (ModuleLevels.IsMod(i))
                owned[ModuleLevels.Kind(i)] = checked(owned[ModuleLevels.Kind(i)] + Loose[i]);
        foreach (var w in Weapons)
        {
            owned[ModuleLevels.Kind(w.Emitter)]++;
            foreach (var part in w.Attachments.Where(i => i > 0))
                owned[ModuleLevels.Kind(part)]++;
        }
        var byId = Weapons.ToDictionary(w => w.Id);
        var configs = Bays.Select(b =>
                b.Weapon is { } id && byId.TryGetValue(id, out var w)
                    ? new WeaponConfig(
                        ModuleLevels.Kind(w.Emitter),
                        [.. w.Attachments],
                        b.Enabled,
                        ModuleLevels.Level(w.Emitter)
                    )
                    : new WeaponConfig(0, new int[8], b.Enabled)
            )
            .ToArray();
        return p with { Owned = owned, Weapons = configs, Equipment = this };
    }
}

public sealed record ShipDefinition(int Id, int Slot, string Name, string Description);

public static class EquipmentCatalog
{
    // Keep categories equally likely even though Engine has a third (cruise) variant.
    public static int GearForRoll(float categoryRoll, float variantRoll)
    {
        var category = Math.Clamp((int)(categoryRoll * 6), 0, 5);
        var variant = Math.Clamp(
            (int)(variantRoll * (category == 1 ? 3 : 2)),
            0,
            category == 1 ? 2 : 1
        );
        return category == 1 && variant == 2 ? 12 : category * 2 + variant;
    }

    public static readonly string[] Slots =
    [
        "Shield",
        "Engine",
        "Radar",
        "Utility",
        "Hull",
        "Reactor",
    ];
    public static readonly ShipDefinition[] Items =
    [
        new(0, 0, "Capacitor", "+1 maximum shield per tier"),
        new(1, 0, "Quick-charge unit", "−0.5s recharge delay per tier; minimum 1s"),
        new(2, 1, "Cruise engine", "+5% maximum speed per tier"),
        new(3, 1, "Vector thrusters", "+10% thrust and steering acceleration per tier"),
        new(4, 2, "Survey array", "+250 exploration radius per tier"),
        new(5, 2, "Combat scanner", "+200 enemy detection radius per tier"),
        new(6, 3, "Collector", "+20 pickup radius per tier"),
        new(7, 3, "Salvage processor", "+10% collected credits per tier"),
        new(8, 4, "Reinforced plating", "+1 maximum hull per tier"),
        new(9, 4, "Reactive armor", "−5% incoming hull damage per tier"),
        new(10, 5, "Weapon reactor", "+5% weapon damage per tier"),
        new(11, 5, "Shield reactor", "+10% shield regeneration rate per tier"),
        new(
            12,
            1,
            "Warp Cruise Drive",
            "Physical cruise: 2 + 0.5 × tier speed; 2s spool; 10s cooldown"
        ),
    ];

    public static int Slot(int kind) => Items[kind].Slot;

    private static readonly int[][] Builds =
    [
        [6, 8, 14, 5, 15, 7, 27, 23],
        [11, 9, 7, 5, 12, 17, 16, 27],
        [14, 6, 23, 5, 7, 15, 28, 24],
        [14, 18, 9, 11, 20, 16, 7, 5],
    ];

    public static WeaponItem Build(int emitter, int tier, int level = 1) =>
        new(
            Guid.NewGuid().ToString("N"),
            ModuleLevels.Key(emitter, level),
            Builds[emitter - 1]
                .Select((m, i) => i < tier * 2 ? ModuleLevels.Key(m, level) : 0)
                .ToArray(),
            tier
        );

    public static int Resale(int module) => module == 0 ? 0 : ModuleCatalog.Get(module).Price / 4;

    public static int Resale(WeaponItem w) => Resale(w.Emitter) + w.Attachments.Sum(Resale);
}

public sealed record WeaponEdit(string Id, int[] Attachments);

public sealed record EquipmentMessage(
    int Sequence,
    string Action,
    long Revision = 0,
    WeaponEdit[]? Edits = null,
    WeaponBay[]? Bays = null,
    string?[]? Ship = null,
    string? Item = null,
    int Module = 0,
    int Quantity = 1,
    int Page = 0
) : SequencedMessage(Sequence);

public sealed record InventoryPage(
    long Revision,
    int Page,
    int Pages,
    WeaponItem[] Weapons,
    ShipItem[] Gear,
    int[] Loose,
    WeaponBay[] Bays,
    string?[] Ship
);
