using System.Collections.Immutable;
using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private void RecordContribution(Enemy enemy, Shot? shot, float amount)
    {
        if (
            amount <= 0
            || shot is null
            || !players.TryGetValue(shot.Owner, out var attacker)
            || attacker.Hull <= 0
        )
            return;
        var old = enemy.Contributors.GetValueOrDefault(attacker.Id);
        enemy.Contributors[attacker.Id] = (
            attacker.RunId,
            (old.Run == attacker.RunId ? old.Damage : 0) + amount
        );
        if (!enemy.Installation || enemy.Contributors[attacker.Id].Damage < 1)
            return;
        var state = ExplorationFor(attacker);
        if (
            Contracts.FirstOrDefault(c => c.Id == state.Contract) is { Kind: "suppress" } c
            && c.Target == enemy.Home
        )
            state.Progress.Add(enemy.Home);
    }

    private void DestroyInstallation(Enemy enemy)
    {
        if (!enemy.Installation)
            return;
        sources[enemy.Home].Destroyed = true;
        sources[enemy.Home].Warning = 0;
        foreach (var state in explorers.Values)
            if (
                Contracts.FirstOrDefault(c => c.Id == state.Contract) is { Kind: "suppress" } c
                && c.Target == enemy.Home
            )
            {
                if (state.Progress.Contains(enemy.Home))
                    state.Ready = true;
                else
                {
                    state.Contract = 0;
                    state.Progress.Clear();
                    state.Ready = false;
                }
            }
    }
}
