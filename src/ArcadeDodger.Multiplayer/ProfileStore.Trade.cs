namespace ArcadeDodger.Multiplayer;

public sealed partial class ProfileStore
{
    public string TradeItem(
        string name,
        string transaction,
        int price,
        int module = 0,
        WeaponItem? weapon = null,
        ShipItem? gear = null
    )
    {
        var p = Get(name);
        if (p.Transactions?.Contains(transaction) == true)
            return "ok";
        if (price < 0 || p.Credits < price)
            return "insufficient_credits";
        if ((p.Transactions?.Length ?? 0) >= 16384)
            return "transaction_limit";
        if ((module > 0 ? 1 : 0) + (weapon is not null ? 1 : 0) + (gear is not null ? 1 : 0) != 1)
            return "invalid_item";
        if (module > 0)
        {
            if (!ModuleCatalog.Valid(module))
                return "invalid_item";
            p = AddModule(p, module);
        }
        else
        {
            var e = p.Equipment!;
            e = e with
            {
                Revision = checked(e.Revision + 1),
                Weapons = weapon is null ? e.Weapons : [.. e.Weapons, weapon.Copy()],
                Gear = gear is null ? e.Gear : [.. e.Gear, gear],
            };
            if (!ValidEquipment(e))
                return "invalid_item";
            p = e.Project(p);
        }
        return Commit(
            name,
            p with
            {
                Credits = p.Credits - price,
                Transactions = [.. p.Transactions ?? [], transaction],
            }
        );
    }

    public string WorldReward(
        string name,
        string transaction,
        int credits,
        int experience,
        RunCheckpoint run,
        WeaponItem? weapon = null,
        ShipItem? gear = null
    )
    {
        var p = Get(name);
        if (p.Transactions?.Contains(transaction) == true)
            return "ok";
        if (
            credits < 0
            || experience < 0
            || p.Credits > long.MaxValue - credits
            || (p.Transactions?.Length ?? 0) >= 16384
        )
            return "reward_limit";
        if (weapon is not null || gear is not null)
        {
            var equipment = p.Equipment! with
            {
                Revision = checked(p.Equipment!.Revision + 1),
                Weapons = weapon is null
                    ? p.Equipment.Weapons
                    : [.. p.Equipment.Weapons, weapon.Copy()],
                Gear = gear is null ? p.Equipment.Gear : [.. p.Equipment.Gear, gear],
            };
            if (!ValidEquipment(equipment))
                return "invalid_item";
            p = equipment.Project(p);
        }
        return Commit(
            name,
            p with
            {
                Credits = p.Credits + credits,
                Run = run with
                {
                    Experience = (int)
                        Math.Min(Room.MaxExperience, (long)run.Experience + experience),
                },
                Transactions = [.. p.Transactions ?? [], transaction],
            }
        );
    }
}
