using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private string SendMapPing(Player p, Exploration state, SectorActionMessage request)
    {
        var result = "invalid_action";
        if (Tick - state.LastPing < 150)
        {
            result = "ping_cooldown";
            return result;
        }
        var destination = Location(request.Target);
        Player? ally = null;
        if (request.Action == "defend")
        {
            ally = players.GetValueOrDefault(request.Target);
            if (
                ally is null
                || ally.Hull <= 0
                || !ally.Connected
                || !friends.Mutual(p.Name, ally.Name)
                || !enemies.Any(e => e.Target == ally.Id && e.Hull > 0)
            )
            {
                result = "no_threatened_ally";
                return result;
            }
        }
        else if (destination is null || !state.Discovered.Contains(destination.Id))
        {
            result = "undiscovered";
            return result;
        }
        var point = ally?.Position ?? destination!.Position;
        pings.Add(
            new(
                ++nextPing,
                p.Id,
                p.Name,
                ally is null ? destination!.Id : 0,
                ally?.Id ?? 0,
                point.X,
                point.Y,
                Tick + 900,
                request.Action == "broadcast",
                ally is null ? 0 : enemies.First(e => e.Target == ally.Id && e.Hull > 0).Home
            )
        );
        if (ally is null)
            foreach (
                var recipient in players.Values.Where(other =>
                    other.Connected
                    && (
                        other.Id == p.Id
                        || request.Action == "broadcast"
                        || friends.Mutual(p.Name, other.Name)
                    )
                )
            )
                ExplorationFor(recipient).Discovered.Add(destination!.Id);
        state.LastPing = Tick;
        result = "ok";
        return result;
    }
}
