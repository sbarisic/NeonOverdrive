namespace ArcadeDodger.Multiplayer;

public sealed record ModuleDefinition(int Id, string Name, int Price, string Category, string Description,
    string LaserDescription, float Value = 0, float Radius = 0, float Seconds = 0);

public static class ModuleCatalog
{
    public const int Amplifier=5, Accelerator=6, Range=7, Pierce=8, Stabilizer=9, Overcharger=10,
        Twin=11, Prism=12, Bloom=13, Seeker=14, Ricochet=15, Relay=16, Burn=17, Cryo=18,
        Radiation=19, Corrosion=20, Tesla=21, Gravity=22, Blast=23, Fuse=24, Plasma=25,
        Mine=26, Leech=27, Salvage=28;
    public static readonly ModuleDefinition[] Items = [
        new(1,"Kinetic barrel",0,"Emitter","Ballistic round: 1 damage, 650 speed.","",650),
        new(2,"Laser emitter",150,"Emitter","Continuous beam: 4 base DPS.","",4),
        new(3,"Rocket motor",180,"Emitter","Heavy rocket: 2 damage, 85-unit blast.","",430,85),
        new(4,"Plasma core",180,"Emitter","Plasma orb: 2 damage, 45-unit blast.","",520,45),
        new(5,"Amplifier",120,"Damage","Multiply downstream damage by 2. Copies multiply.","Thicker beam from this checkpoint onward.",2),
        new(6,"Accelerator",60,"Damage","Multiply speed by 1.25. Copies multiply.","Multiply range by 1.25.",1.25f),
        new(7,"Range Extender",80,"Damage","Multiply range by 1.30; checkpoints stay fixed.","Same effect. Copies multiply.",1.30f),
        new(8,"Piercing Core",110,"Damage","Pass through two additional hostiles per copy.","Beam continues through two additional hostiles.",2),
        new(9,"Stabilizer",50,"Trajectory","Halve subsequent branch angles per copy.","Does not merge existing branches.",.5f),
        new(10,"Overcharger",140,"Damage","Double shield damage per copy; normal hull damage.","Same effect, applied continuously.",2),
        new(11,"Twin Splitter",100,"Trajectory","Two branches at ±0.22 radians, sharing damage.","Two continuous branches sharing DPS.",2,.22f),
        new(12,"Tri-Prism",160,"Trajectory","Three branches at -0.30, 0, +0.30, sharing damage.","Three continuous branches sharing DPS.",3,.30f),
        new(13,"Scatter Bloom",180,"Trajectory","Six branches across ±0.60 radians; half remaining range.","Six short branches sharing DPS.",6,.60f),
        new(14,"Seeker Head",150,"Trajectory","Steer toward the nearest alien within 600 units.","Bend toward that alien at this checkpoint.",.09f,600),
        new(15,"Ricochet Plate",100,"Trajectory","Add two wall reflections per copy; distance continues.","Reflect the continuous beam at walls.",2),
        new(16,"Chain Relay",190,"Trajectory","Add one jump per copy to an unvisited hostile within 180; half damage per jump.","Continuous contact relay; never recursively triggers payloads.",.5f,180),
        new(17,"Incendiary Coat",80,"Elemental","Burn aliens for 3 seconds at 1 DPS per copy.","Refresh the strongest burn while in contact.",1,0,3),
        new(18,"Cryogenic Lens",80,"Elemental","Multiply alien speed by 0.45 per copy for 2.5 seconds.","Refresh the strongest slow while in contact.",.45f,0,2.5f),
        new(19,"Radiation Jacket",140,"Elemental","Radiate aliens within 100 units at 1 DPS per copy for 4 seconds.","Refresh the strongest radiation while in contact.",1,100,4),
        new(20,"Corrosive Payload",120,"Elemental","Aliens take 25% extra damage per copy for 4 seconds.","Refresh the strongest vulnerability while in contact.",.25f,0,4),
        new(21,"Tesla Coil",150,"Elemental","Each copy adds damage-based charge; 4 charge stuns for 0.3 seconds. One-second retrigger delay.","Charge uses actual beam damage.",4,0,.3f),
        new(22,"Gravity Core",170,"Elemental","Pull aliens within 100 units at 60 units/second per copy.","Pull toward the beam's impact point.",60,100),
        new(23,"Blast Chamber",110,"Damage","Add a 65-unit impact blast at 50% direct damage per copy.","Two blast pulses/second conserve splash DPS.",.5f,65),
        new(24,"Proximity Fuse",90,"Utility","Trigger against a hostile within 45 units and end the projectile.","End the beam at the trigger, applying contact payloads.",0,45),
        new(25,"Plasma Converter",180,"Utility","Convert to plasma; retain damage and remaining stages. Plasma passes through unchanged.","End beam and emit two plasma volleys/second, conserving DPS.",2),
        new(26,"Mine Layer",160,"Utility","Become a terminal mine: arms in 0.5s, lasts 10s, trigger 55, blast 85.","End beam and deposit one mine volley/second, conserving DPS.",1,85,10),
        new(27,"Leech Circuit",200,"Utility","Restore 10% of actual damage as shield per copy; excludes overkill.","Fractional recovery accumulates up to maximum shield.",.1f),
        new(28,"Salvage Beacon",100,"Utility","Mark aliens for 5 seconds: +25% dropped credits, rounded up. Does not stack.","Refresh the mark while in contact.",1.25f,0,5)
    ];
    public static ModuleDefinition Get(int id) => Items[id-1];
    public static bool Valid(int id) => id is >=1 and <=28;
    public static int[] UnlockLevels { get; } = [1,5,10,15];
}

public sealed record WeaponConfig(int Emitter, int[] Attachments, bool Enabled = true)
{
    public WeaponConfig Copy() => this with { Attachments = (int[])Attachments.Clone() };
}
public sealed record RunCheckpoint(int Experience, string[] Skills, int Hull, int Shield, float HullFraction, float ShieldFraction, double[] WeaponCooldown, long[] PlasmaCooldown, long[] MineCooldown, long ShieldDelay, long HullDelay, float VelocityX=0, float VelocityY=0);
public sealed record PilotProfile(long Credits, int[] Owned, WeaponConfig[] Weapons, string RunId = "", RunCheckpoint? Run = null)
{
    public static PilotProfile Starter() { var owned=new int[29]; owned[1]=4; return new(100,owned,
        Enumerable.Range(0,4).Select(i=>new WeaponConfig(1,new int[8],i==0)).ToArray()); }
    public PilotProfile Copy() => this with {Owned=(int[])Owned.Clone(), Weapons=Weapons.Select(w=>w.Copy()).ToArray(),Run=Run is null?null:Run with {Skills=(string[])Run.Skills.Clone(),WeaponCooldown=(double[])Run.WeaponCooldown.Clone(),PlasmaCooldown=(long[])Run.PlasmaCooldown.Clone(),MineCooldown=(long[])Run.MineCooldown.Clone()}};
}
public sealed record WorkshopSnapshot(long Credits, int[] Owned, WeaponConfig[] Weapons, int Sequence=0, string Result="");
