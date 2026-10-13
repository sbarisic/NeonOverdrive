using System.Collections.Immutable;
using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    public bool SectorAction(int id, SectorActionMessage request)
    {
        if (
            !players.TryGetValue(id, out var p)
            || !p.Connected
            || request.Sequence <= p.LastReceived
            || Sector is null
        )
            return false;
        p.LastReceived = request.Sequence;
        var state = ExplorationFor(p);
        if (Racing(p) && request.Action is not ("race_stop" or "cruise"))
        {
            state.Receipt = new(request.Sequence, "finish_race_first");
            return false;
        }
        var result =
            p.Hull <= 0
                ? "ship_destroyed"
                : request.Action switch
                {
                    "ping" or "broadcast" or "defend" => SendMapPing(p, state, request),
                    "dock" => DockAtStation(p, state, request),
                    "undock" => UndockFromStation(p, state, request),
                    "repair" => RepairAtStation(p, state, request),
                    "buy_chassis" => PurchaseChassis(p, state, request),
                    "cruise" => ToggleCruise(p),
                    "radio" or "radio_trade" or "radio_close" => RadioAction(
                        p,
                        request.Action,
                        request.Target
                    ),
                    "convoy_buy" => BuyConvoy(p, request.Target),
                    "tow" => TowCargo(p, request.Target),
                    "race_join" => JoinRace(p, request.Target),
                    "race_stop" => StopRace(p),
                    "race_retry" => RetryRaceSave(p),
                    "mission_bounty"
                    or "mission_interception"
                    or "mission_smuggling"
                    or "mission_recovery"
                    or "mission_exploration"
                    or "mission_protection" => AcceptWorldJob(p, request.Action[8..]),
                    "mission_claim" => ClaimWorldJob(p),
                    "mission_abandon" => AbandonWorldJob(p),
                    "mission_scan" => ScanAnomaly(p),
                    "police_surrender" or "police_pay" => PoliceAction(
                        p,
                        request.Action == "police_pay"
                    ),
                    "police_refuse" => RefusePolice(p),
                    "wreck_reactor" or "wreck_cargo" or "wreck_navigation" or "wreck_abandon" =>
                        ExploreDerelict(p, request.Target, request.Action[6..]),
                    "accept" => AcceptContract(p, state, request),
                    "claim" => ClaimContract(p, state, request),
                    "interact" => BeginInteraction(p, state, request),
                    "cancel" => CancelInteraction(p, state, request),
                    "hail" or "goodbye" or "job_accept" or "job_claim" => ContactAction(
                        p,
                        state,
                        request
                    ),
                    "activate" or "turret_one" or "turret_two" or "repair_bot" => BuildOutpost(
                        p,
                        state,
                        request
                    ),
                    _ => "invalid_action",
                };
        state.Receipt = new(request.Sequence, result);
        return result == "ok";
    }
}
