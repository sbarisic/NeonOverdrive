using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private string DockAtStation(Player p, Exploration state, SectorActionMessage request)
    {
        var result = "invalid_action";
        var station = Location(request.Target);
        if (
            station is not null
            && territory.TryGetValue(station.Id, out var control)
            && (!control.Friendly || control.Contested)
        )
            return "station_contested";
        if (station?.Kind != "station" || Vector2.Distance(p.Position, station.Position) > 180)
            result = "out_of_range";
        else
        {
            p.Docked = station.Id;
            state.LastStation = station.Id;
            p.Held = null;
            p.Velocity = Vector2.Zero;
            p.Throttle = 0;
            state.InteractionTarget = 0;
            result = "ok";
        }
        return result;
    }

    private string UndockFromStation(Player p, Exploration state, SectorActionMessage request)
    {
        var result = "invalid_action";
        if (p.Docked == 0)
        {
            result = "not_docked";
            return result;
        }
        p.Position = Location(p.Docked)!.Position + new Vector2(150, 0);
        p.Previous = p.Position;
        p.Docked = 0;
        p.Held = null;
        result = "ok";
        return result;
    }

    private string RepairAtStation(Player p, Exploration state, SectorActionMessage request)
    {
        var result = "invalid_action";
        if (p.Docked == 0)
        {
            result = "not_docked";
            return result;
        }
        var cost = (MaxHullFor(p) - p.Hull + (p.HullFraction > 0 ? 1 : 0)) * 10;
        if (cost == 0 || p.EconomyPending)
        {
            result = cost == 0 ? "already_repaired" : "busy";
            return result;
        }
        p.EconomyPending = true;
        result = "pending";
        state.Receipt = new(request.Sequence, "pending");
        PersistForRun(
            p,
            () => profiles.Debit(p.Name, cost),
            r =>
            {
                if (r == "ok" && p.Hull > 0)
                {
                    p.Hull = MaxHullFor(p);
                    p.HullFraction = 0;
                }
                p.EconomyPending = false;
                if (state.Receipt?.Sequence == request.Sequence)
                    state.Receipt = new(request.Sequence, r);
            }
        );
        if (!profiles.BackgroundWrites)
            result = state.Receipt!.Result;
        return result;
    }
}
