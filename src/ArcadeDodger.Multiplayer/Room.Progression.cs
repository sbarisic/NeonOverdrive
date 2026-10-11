using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed record EnergySnapshot(int Id, float X, float Y, int Value);

public sealed partial class Room
{
    public const int EnergyCapacity = 128, EnergyLifetimeTicks = 30 * TickRate;
    public const int MaxExperience = 1000000;
    private readonly List<Energy> energy = new();
    private int nextEnergy;

    // Total XP thresholds: 0, 20, 60, 120, 200... No combat advantage until choices land.
    public static int LevelForExperience(int experience)
    {
        var level = 1;
        while (level < 100 && experience >= ExperienceForLevel(level + 1)) level++;
        return level;
    }

    public static int ExperienceForLevel(int level) =>
        10 * Math.Clamp(level, 1, 100) * (Math.Clamp(level, 1, 100) - 1);

    private void DropEnergy(Enemy enemy)
    {
        if (energy.Count >= EnergyCapacity) return; // Bounded load; overflow reward is discarded, never duplicated.
        energy.Add(new Energy(++nextEnergy, enemy.Position, enemy.Gunner ? 15 : 10, Tick + EnergyLifetimeTicks));
    }

    private void StepEnergy()
    {
        energy.RemoveAll(e => e.Expires <= Tick);
        for (var i = energy.Count - 1; i >= 0; i--)
        {
            var drop = energy[i];
            // Swept collection catches dashes. Closest contact wins, then stable player ID.
            var collector = players.Values.Where(p => p.Hull > 0 && p.Connected)
                .Select(p => (Player: p, Contact: Intersection(p.Previous - drop.Position, p.Position - drop.Position, Radius + 12)))
                .Where(p => p.Contact.HasValue).OrderBy(p => p.Contact).ThenBy(p => p.Player.Id)
                .FirstOrDefault().Player;
            if (collector is null) continue;
            collector.Experience = Math.Min(MaxExperience, collector.Experience + drop.Value);
            energy.RemoveAt(i);
        }
    }

    private sealed record Energy(int Id, Vector2 Position, int Value, long Expires);
}
