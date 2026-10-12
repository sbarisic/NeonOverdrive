namespace ArcadeDodger.Multiplayer;

public sealed record BeamSnapshot(
    int Owner,
    int Segment,
    float X,
    float Y,
    float EndX,
    float EndY,
    int Effects = 0,
    int Weapon = 0,
    int Stage = 0,
    float Power = 1
);

public sealed partial class Room
{
    public const int BeamCapacity = Capacity * 64;
    public const float LaserDps = 4;
    private readonly List<BeamSnapshot> beams = [];

    private void StepBeams()
    {
        beams.Clear();
        foreach (
            var p in players
                .Values.Skip((int)(Tick % Math.Max(1, players.Count)))
                .Concat(players.Values.Take((int)(Tick % Math.Max(1, players.Count))))
        )
        {
            if (
                !p.Connected
                || p.Hull <= 0
                || p.Editing
                || p.Menu
                || Safe(p)
                || p.Held?.Fire != true
            )
                continue;
            var active = Enumerable
                .Range(0, 4)
                .Where(i => Active(p, i) && p.Weapons[i].Emitter == 2)
                .OrderBy(i => (i + (int)Tick) % 4)
                .ToArray();
            if (active.Length == 0)
                continue;
            foreach (var weapon in active)
            {
                var first = beams.Count;
                var quota = 64 / active.Length;
                var ray = CreateModularShot(p, p.Aim, weapon);
                ray.IsBeam = true;
                ray.EmitPlasma = Tick >= p.PlasmaReady[weapon];
                ray.EmitMine = Tick >= p.MineReady[weapon];
                Advance(ray, float.PositiveInfinity, first, quota);
            }
        }
    }
}
