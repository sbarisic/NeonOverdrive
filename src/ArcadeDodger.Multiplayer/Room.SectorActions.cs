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
        var result =
            p.Hull <= 0
                ? "ship_destroyed"
                : request.Action switch
                {
                    "ping" or "broadcast" or "defend" => SendMapPing(p, state, request),
                    "dock" => DockAtStation(p, state, request),
                    "undock" => UndockFromStation(p, state, request),
                    "repair" => RepairAtStation(p, state, request),
                    "accept" => AcceptContract(p, state, request),
                    "claim" => ClaimContract(p, state, request),
                    "interact" => BeginInteraction(p, state, request),
                    "cancel" => CancelInteraction(p, state, request),
                    _ => "invalid_action",
                };
        state.Receipt = new(request.Sequence, result);
        return result == "ok";
    }
}
