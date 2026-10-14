namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private string PurchaseChassis(Player p, Exploration state, SectorActionMessage request)
    {
        if (p.Docked == 0)
            return "not_docked";
        if (p.EconomyPending)
            return "busy";
        var station = p.Docked;
        var checkpoint = Checkpoint(p);
        p.EconomyPending = true;
        state.Receipt = new(request.Sequence, "pending");
        PersistForRun(
            p,
            () => profiles.BuyChassis(p.Name, request.Target, station, checkpoint),
            result =>
            {
                p.EconomyPending = false;
                if (result == "ok" && p.Hull > 0)
                {
                    LoadEquipment(p, profiles.Get(p.Name));
                    p.Position = ClearSpawn(
                        p.Position,
                        ChassisCatalog.Get(p.Chassis).Radius,
                        ignorePilot: p.Id
                    );
                    p.Previous = p.Position;
                }
                state.Receipt = new(request.Sequence, result);
            }
        );
        return profiles.BackgroundWrites ? "pending" : state.Receipt.Result;
    }
}
