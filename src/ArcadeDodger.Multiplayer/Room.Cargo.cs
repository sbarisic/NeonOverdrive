using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private void AddCargo(string kind, Vector2 at, int credits, long life = 900 * TickRate)
    {
        if (worldCargo.Count >= 64)
            return;
        var point = Exterior(at, 120);
        if (geometry?.TraceShips(point, point, 28) is not null)
            return;
        var id = ++nextWorldId;
        var tier = Math.Clamp(
            Sector!
                .Locations.Where(l => l.Kind == "planet")
                .MinBy(l => Vector2.DistanceSquared(l.Position, point))!
                .Id,
            1,
            4
        );
        worldCargo.Add(
            new()
            {
                Id = id,
                Kind = kind,
                Position = point,
                Credits = credits,
                Reward = kind is "derelict" or "anomaly"
                    ? EquipmentCatalog.Build(
                        1 + id % 4,
                        kind == "anomaly" ? 4 : tier,
                        kind == "anomaly" ? 4 : tier
                    )
                    : null,
                Expires = life == 0 ? long.MaxValue : Tick + life,
            }
        );
    }

    private string TowCargo(Player p, int target)
    {
        var state = Living(p);
        if (target == 0)
        {
            ReleaseTow(p);
            return "ok";
        }
        if (state.Tow != 0)
            return "already_towing";
        var cargo = worldCargo.FirstOrDefault(c => c.Id == target);
        if (
            cargo is null
            || cargo.Pending
            || cargo.Kind is "derelict" or "anomaly"
            || cargo.Owner != 0
        )
            return "tow_unavailable";
        if (
            Vector2.Distance(p.Position, cargo.Position) > 180
            || !ServiceLine(p.Position, cargo.Position)
        )
            return "out_of_range";
        cargo.Owner = p.Id;
        state.Tow = cargo.Id;
        CancelCruise(p);
        return "ok";
    }

    private void ReleaseTow(Player p)
    {
        var state = Living(p);
        foreach (var cargo in worldCargo.Where(c => c.Owner == p.Id))
            cargo.Owner = 0;
        state.Tow = 0;
    }

    private void DeliverCargo(Player p, WorldCargo cargo)
    {
        if (p.EconomyPending || cargo.Pending)
            return;
        cargo.Pending = p.EconomyPending = true;
        var checkpoint = Checkpoint(p);
        var committed = false;
        PersistForRun(
            p,
            () =>
            {
                var result = profiles.WorldReward(
                    p.Name,
                    $"cargo:{Sector!.Seed}:{cargo.Id}",
                    cargo.Credits,
                    0,
                    checkpoint
                );
                committed = result == "ok";
                return result;
            },
            result =>
            {
                cargo.Pending = p.EconomyPending = false;
                if (committed)
                    worldCargo.Remove(cargo);
                if (result == "ok")
                {
                    worldCargo.Remove(cargo);
                    Living(p).Tow = 0;
                    if (Living(p).Job == "recovery" && Living(p).Targets.Contains(cargo.Id))
                        Living(p).JobState = "ready";
                    p.LootNotice = $"Cargo delivered: {cargo.Credits} credits";
                    p.LootNoticeUntil = Tick + 150;
                }
            }
        );
    }
}
