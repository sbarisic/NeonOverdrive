using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private string AcceptContract(Player p, Exploration state, SectorActionMessage request)
    {
        var result = "invalid_action";
        var contract = Contracts.FirstOrDefault(c => c.Id == request.Target);
        if (contract is null || contract.Station != p.Docked)
        {
            result = "not_docked";
            return result;
        }
        if (
            state.Contract != 0
            || state.Completed.Contains(contract.Id)
            || state.ContractCooldown.GetValueOrDefault(contract.Id) > Tick
        )
        {
            result = "unavailable";
            return result;
        }
        if (contract.Kind == "suppress" && sources[contract.Target].Destroyed)
        {
            result = "unavailable";
            return result;
        }
        state.Contract = contract.Id;
        state.Progress.Clear();
        state.Carrying = false;
        state.Ready = false;
        result = "ok";
        return result;
    }

    private string ClaimContract(Player p, Exploration state, SectorActionMessage request)
    {
        var result = "invalid_action";
        var reward = Contracts.FirstOrDefault(c => c.Id == state.Contract);
        if (reward is null || p.Docked != reward.Station)
        {
            result = "not_docked";
            return result;
        }
        if (!state.Ready)
        {
            result = "incomplete";
            return result;
        }
        if (p.EconomyPending)
        {
            result = "busy";
            return result;
        }
        p.EconomyPending = true;
        result = "pending";
        state.Receipt = new(request.Sequence, "pending");
        PersistForRun(
            p,
            () => profiles.Credit(p.Name, reward.Credits),
            r =>
            {
                if (r == "ok")
                {
                    if (p.Hull > 0)
                        p.Experience = Math.Min(MaxExperience, p.Experience + reward.Experience);
                    if (reward.Kind == "interdict")
                        state.ContractCooldown[reward.Id] = Tick + 5 * 60 * TickRate;
                    else
                        state.Completed.Add(reward.Id);
                    state.Contract = 0;
                    state.Carrying = false;
                    state.Ready = false;
                    state.Progress.Clear();
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

    private string BeginInteraction(Player p, Exploration state, SectorActionMessage request)
    {
        var result = "invalid_action";
        var target = Location(request.Target);
        var active = Contracts.FirstOrDefault(c => c.Id == state.Contract);
        if (
            p.Docked != 0
            || p.Editing
            || target is null
            || Vector2.Distance(p.Position, target.Position) > 100
            || !VisibleLine(p.Position, target.Position)
        )
        {
            result = "out_of_range";
            return result;
        }
        if (
            active is null
            || !(
                active.Kind == "survey"
                    && target.Kind == "beacon"
                    && !state.Progress.Contains(target.Id)
                || active.Kind == "recover" && target.Id == active.Target && !state.Carrying
            )
        )
        {
            result = "unavailable";
            return result;
        }
        state.InteractionTarget = target.Id;
        state.InteractionStart = Tick;
        result = "ok";
        return result;
    }

    private string CancelInteraction(Player p, Exploration state, SectorActionMessage request)
    {
        var result = "invalid_action";
        state.InteractionTarget = 0;
        result = "ok";
        return result;
    }
}
