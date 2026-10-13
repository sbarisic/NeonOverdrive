using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private float CruiseMultiplier(Player p) => p.Cruise ? 2 + .5f * Gear(p, 12) : 1;

    private static float TurnRateFor(Player p) =>
        ChassisCatalog.Get(p.Chassis).TurnRadians * (p.Cruise ? .5f : 1);

    private void CancelCruise(Player p)
    {
        if (!p.Cruise && p.CruiseAt == 0)
            return;
        p.Cruise = false;
        p.CruiseAt = 0;
        p.CruiseReady = Tick + 10 * TickRate;
    }

    private string ToggleCruise(Player p)
    {
        if (p.Cruise || p.CruiseAt > 0)
        {
            CancelCruise(p);
            return "ok";
        }
        if (Gear(p, 12) == 0)
            return "cruise_drive_required";
        if (Living(p).Tow != 0)
            return "stop_towing";
        if (p.Menu || p.Editing || p.Docked != 0 || !p.Connected)
            return "close_menu";
        if (Tick < p.CruiseReady || p.LastDamage >= 0 && Tick - p.LastDamage < 5 * TickRate)
            return "cruise_not_ready";
        p.CruiseAt = Tick + 2 * TickRate;
        return "ok";
    }

    private void StepCruise(Player p, bool frozen)
    {
        if (
            frozen
            || p.Held is null
            || !p.Connected
            || Gear(p, 12) == 0
            || p.LastDamage >= 0 && Tick - p.LastDamage < 5 * TickRate
        )
        {
            CancelCruise(p);
            return;
        }
        if (p.CruiseAt > 0 && Tick >= p.CruiseAt)
        {
            p.Cruise = true;
            p.CruiseAt = 0;
        }
        if (
            p.Cruise
            && geometry?.TraceShips(
                p.Position,
                p.Position + p.Velocity / TickRate,
                ChassisCatalog.Get(p.Chassis).Radius
            )
                is not null
        )
            CancelCruise(p);
    }
}
