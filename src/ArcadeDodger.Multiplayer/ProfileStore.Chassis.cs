namespace ArcadeDodger.Multiplayer;

public sealed partial class ProfileStore
{
    public string BuyChassis(string name, int chassis, int station, RunCheckpoint checkpoint)
    {
        if (!ChassisCatalog.Valid(chassis) || chassis == 0)
            return "invalid_chassis";
        var design = ChassisCatalog.Get(chassis);
        if (design.Station != station)
            return "wrong_station";
        var p = Get(name);
        if (p.Chassis == chassis)
            return "already_equipped";
        if (p.Credits < design.Price)
            return "insufficient_credits";
        var equipment = p.Equipment!;
        int Gear(int kind) =>
            equipment
                .Gear.Where(g => g.Kind == kind && equipment.Ship.Contains(g.Id))
                .Sum(g => g.Tier);
        var hull = design.Hull + SkillCatalog.Rank(checkpoint.Skills, "hull") + Gear(8);
        var shield = design.Shield + SkillCatalog.Rank(checkpoint.Skills, "shield") + Gear(0);
        checkpoint = checkpoint with
        {
            Hull = Math.Min(checkpoint.Hull, hull),
            Shield = Math.Min(checkpoint.Shield, shield),
            HullFraction =
                checkpoint.Hull - checkpoint.HullFraction > hull ? 0 : checkpoint.HullFraction,
            ShieldFraction =
                checkpoint.Shield - checkpoint.ShieldFraction > shield
                    ? 0
                    : checkpoint.ShieldFraction,
        };
        return Commit(
            name,
            p with
            {
                Credits = p.Credits - design.Price,
                Chassis = chassis,
                Run = checkpoint,
                Equipment = equipment with { Revision = checked(equipment.Revision + 1) },
            }
        );
    }
}
