using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private void StepPolice()
    {
        foreach (var p in players.Values.Where(p => p.Hull > 0 && p.Connected))
        {
            var state = Living(p);
            var police = civilians
                .Where(c => c.Ship.Role == NpcRole.Police && c.Ship.Hull > 0)
                .Select(c => c.Ship)
                .FirstOrDefault(e =>
                    Vector2.Distance(e.Position, p.Position) < 350
                    && ServiceLine(e.Position, p.Position)
                );
            if (police is null)
            {
                if (state.Police is "scanning" or "decision")
                {
                    state.Police = "wanted";
                    state.LastPoliceSeen = Tick;
                }
                state.ScanAt = 0;
                if (state.Police == "wanted" && Tick - state.LastPoliceSeen >= 60 * TickRate)
                    state.Police = "";
                continue;
            }
            if (state.Police == "wanted")
            {
                state.LastPoliceSeen = Tick;
                if (
                    !Safe(p)
                    && !p.Editing
                    && Tick >= police.FireAt
                    && shots.Count < ProjectileLimit
                )
                {
                    var delta = p.Position - police.Position;
                    if (delta.LengthSquared() > 1)
                        shots.Add(
                            new()
                            {
                                Id = ++nextShot,
                                Owner = -police.Id,
                                EnforcementTarget = p.Id,
                                Position = police.Position,
                                BirthPosition = police.Position,
                                Velocity = Vector2.Normalize(delta) * 300 / TickRate,
                                Born = Tick,
                                Expires = long.MaxValue / 2,
                                Range = 900,
                                Damage = .5f,
                            }
                        );
                    police.FireAt = Tick + 75;
                }
                continue;
            }
            if (state.IllegalCargo == 0)
                continue;
            if (state.ScanAt == 0)
            {
                state.ScanAt = Tick;
                state.Police = "scanning";
                Broadcast(
                    "police",
                    "Patrol",
                    $"{p.Name}, hold position for cargo inspection. You may surrender contraband."
                );
            }
            if (Tick - state.ScanAt >= 3 * TickRate)
                state.Police = "decision";
        }
    }

    private string PoliceAction(Player p, bool pay)
    {
        var state = Living(p);
        if (state.IllegalCargo == 0)
            return "no_contraband";
        if (state.Police.Length == 0)
            return "no_inspection";
        if (!pay && state.Police is "decision" or "wanted")
            return "fine_required";
        if (p.EconomyPending)
            return "busy";
        var cost = pay ? (int)Math.Min(25, profiles.Get(p.Name).Credits) : 0;
        void Confiscate(string result)
        {
            p.EconomyPending = false;
            if (result == "ok")
            {
                state.IllegalCargo = 0;
                state.JobState = "failed";
                state.ScanAt = 0;
                state.Police = "";
            }
        }
        if (cost == 0)
        {
            Confiscate("ok");
            return "ok";
        }
        p.EconomyPending = true;
        var sequence = p.LastReceived;
        PersistForRun(
            p,
            () => profiles.Debit(p.Name, cost),
            result =>
            {
                Confiscate(result);
                ExplorationFor(p).Receipt = new(sequence, result);
            }
        );
        return profiles.BackgroundWrites ? "pending" : ExplorationFor(p).Receipt!.Result;
    }

    private string RefusePolice(Player p)
    {
        var state = Living(p);
        if (state.Police is not ("scanning" or "decision"))
            return "no_inspection";
        state.Police = "wanted";
        state.LastPoliceSeen = Tick;
        state.ScanAt = 0;
        Broadcast("police", "Patrol", $"{p.Name} refused inspection. Pursuit authorized.");
        return "ok";
    }
}
