using ArcadeDodger.Multiplayer;

namespace ArcadeDodger.Web;

// Presentation-only decoder. Numeric arrays cross the WASM boundary in one copy,
// avoiding reflection and JSON property-name matching for every projectile.
internal sealed class PackedSnapshot(double[] data, string[] text)
{
    private int cursor;

    private double Number()
    {
        if (cursor >= data.Length || !double.IsFinite(data[cursor]))
            throw new FormatException();
        return data[cursor++];
    }

    private int Int() => checked((int)Number());

    private long Long() => checked((long)Number());

    private float Float() => (float)Number();

    private bool Bool() => Int() != 0;

    private string Text()
    {
        var id = Int();
        return id >= 0 && id < text.Length ? text[id] : throw new FormatException();
    }

    private T[] List<T>(int limit, Func<T> read)
    {
        var count = Int();
        if (count < 0 || count > limit)
            throw new FormatException();
        var result = new T[count];
        for (var i = 0; i < count; i++)
            result[i] = read();
        return result;
    }

    public RoomSnapshot Read()
    {
        var version = Int();
        var tick = Long();
        var players = List(
            24,
            () =>
                new PlayerSnapshot(
                    Int(),
                    Text(),
                    Float(),
                    Float(),
                    Float(),
                    Int(),
                    Int(),
                    Int(),
                    Bool(),
                    Bool(),
                    Float(),
                    Int(),
                    Int(),
                    Float(),
                    Float(),
                    Float(),
                    Int(),
                    Int(),
                    MaxShield: Int(),
                    Weapon: Text(),
                    MaxHull: Int(),
                    MoveSpeed: Float(),
                    Menu: Bool(),
                    Editing: Bool(),
                    Docked: Int()
                )
        );
        var shots = List(
            1024,
            () =>
            {
                var id = Int();
                var owner = Int();
                var x = Float();
                var y = Float();
                var dx = Float();
                var dy = Float();
                var born = Long();
                var kind = Int();
                var effects = Int();
                var modular = Bool();
                var parent = Int();
                var bx = Float();
                var by = Float();
                var weapon = Int();
                var stage = Int();
                var power = Float();
                return new ShotSnapshot(
                    id,
                    owner,
                    x,
                    y,
                    dx,
                    dy,
                    born,
                    kind,
                    effects,
                    modular,
                    parent,
                    bx,
                    by,
                    List(16, Int),
                    weapon,
                    stage,
                    power
                );
            }
        );
        string[] kinds = ["rammer", "gunner", "interceptor", "sniper", "tender", "installation"];
        var enemies = List(
            256,
            () =>
            {
                var id = Int();
                var kind = Int();
                return new EnemySnapshot(
                    id,
                    kinds[Math.Clamp(kind, 0, 5)],
                    Float(),
                    Float(),
                    Float(),
                    Int(),
                    Bool(),
                    Int(),
                    Int(),
                    Text(),
                    Int(),
                    Float(),
                    Int()
                );
            }
        );
        var energy = List(128, () => new EnergySnapshot(Int(), Float(), Float(), Int(), Int()));
        var credits = List(128, () => new CreditSnapshot(Int(), Float(), Float(), Int(), Int()));
        var modules = List(
            128,
            () => new ModuleDropSnapshot(Int(), Float(), Float(), Int(), Int())
        );
        var beams = List(
            1536,
            () =>
                new BeamSnapshot(
                    Int(),
                    Int(),
                    Float(),
                    Float(),
                    Float(),
                    Float(),
                    Int(),
                    Int(),
                    Int(),
                    Float()
                )
        );
        var mines = List(
            128,
            () => new MineSnapshot(Int(), Int(), Float(), Float(), Bool(), Float())
        );
        var explosions = List(
            32,
            () => new ExplosionSnapshot(Int(), Float(), Float(), Float(), Long())
        );
        var locations = List(64, () => new LocationState(Int(), Int(), Text(), Int()));
        var friends = List(1024, Text);
        var wallet = Long();
        var combat =
            cursor < data.Length
                ? List(
                    512,
                    () => new CombatEvent(Int(), Int(), Int(), Float(), Float(), Long(), Float())
                )
                : [];
        if (cursor != data.Length)
            throw new FormatException();
        return new(
            version,
            "snapshot",
            tick,
            players,
            shots,
            enemies,
            energy,
            explosions,
            beams,
            Social: new(friends, [], []),
            Workshop: new(wallet, [], []),
            Credits: credits,
            Mines: mines,
            Sector: new(locations),
            ModuleDrops: modules,
            CombatEvents: combat
        );
    }
}
