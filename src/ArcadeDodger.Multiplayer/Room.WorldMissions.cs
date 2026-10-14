using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private string AcceptWorldJob(Player p, string kind)
    {
        var state = Living(p);
        if (state.Job.Length > 0 || ExplorationFor(p).Job.Length > 0)
            return "assignment_active";
        if (Tick < Math.Max(state.JobCooldown, ExplorationFor(p).JobCooldown))
            return "assignment_cooldown";
        if (p.Docked == 0 && state.Conversation == 0)
            return "contact_required";
        if (
            p.Docked == 0
            && !enemies.Any(e =>
                e.Id == state.Conversation
                && e.Allied
                && e.Hull > 0
                && e.Role != NpcRole.Police
                && Vector2.Distance(e.Position, p.Position) <= Detection(p)
            )
        )
            return "contact_unavailable";
        var reserved = livingPilots
            .Values.Where(s => s.JobState == "active")
            .SelectMany(s => s.Targets)
            .ToHashSet();
        int[] targets = [];
        if (kind is "bounty" or "interception")
        {
            var target = enemies
                .Where(e =>
                    !e.Allied
                    && !e.Installation
                    && e.SurfacePlanet == 0
                    && e.Type
                        is not ("salvage-core" or "salvage-turret" or "repair-bot" or "turret")
                    && e.Hull > 0
                    && !reserved.Contains(e.Id)
                    && (kind != "interception" || e.Role == NpcRole.Courier && e.State != "arrived")
                )
                .OrderByDescending(e => e.Level)
                .ThenBy(e => Vector2.DistanceSquared(p.Position, e.Position))
                .FirstOrDefault();
            if (target is null)
                return "no_eligible_target";
            targets = [target.Id];
            state.JobRegion = Math.Clamp(target.Level, 1, 4);
        }
        else if (kind == "protection")
        {
            var target = civilians.FirstOrDefault(c =>
                c.Ship.Hull > 0
                && !reserved.Contains(c.Ship.Id)
                && c.Ship.Role is NpcRole.Trader or NpcRole.Miner
                && enemies.Any(e =>
                    !e.Allied
                    && !e.Installation
                    && e.Hull > 0
                    && e.NpcTarget == c.Ship.Id
                    && Vector2.Distance(e.Position, c.Ship.Position) < 900
                )
            );
            if (target is null)
                return "no_eligible_target";
            targets =
            [
                target.Ship.Id,
                .. enemies
                    .Where(e =>
                        !e.Allied
                        && e.Hull > 0
                        && !e.Installation
                        && e.NpcTarget == target.Ship.Id
                        && Vector2.Distance(e.Position, target.Ship.Position) < 900
                    )
                    .Select(e => e.Id)
                    .Take(8),
            ];
        }
        else if (kind == "recovery")
        {
            var cargo = worldCargo.FirstOrDefault(c =>
                c.Kind is "container" or "disabled-ship"
                && c.Owner == 0
                && !c.Pending
                && !reserved.Contains(c.Id)
            );
            if (cargo is null)
                return "no_eligible_target";
            targets = [cargo.Id];
        }
        else if (kind == "exploration")
        {
            var anomaly = worldCargo.FirstOrDefault(c =>
                c.Kind == "anomaly" && !reserved.Contains(c.Id)
            );
            if (anomaly is null)
                return "no_eligible_target";
            targets = [anomaly.Id];
            state.Reward = anomaly.Reward;
        }
        else if (kind == "smuggling")
        {
            var destination = DeliveryDestination(p, p.Docked);
            if (destination == 0)
                return "no_delivery_station";
            targets = [destination];
            state.IllegalCargo = 1;
        }
        else
            return "invalid_assignment";
        var objectivePosition =
            kind is "exploration" or "recovery" ? worldCargo.First(c => c.Id == targets[0]).Position
            : kind == "smuggling" ? Location(targets[0])!.Position
            : enemies.First(e => e.Id == targets[0]).Position;
        state.JobRegion = Math.Clamp(
            Sector!
                .Locations.Where(l => l.Kind == "planet")
                .MinBy(l => Vector2.DistanceSquared(l.Position, objectivePosition))!
                .Id,
            1,
            4
        );
        state.Job = kind;
        state.JobState = "active";
        state.Targets = targets;
        state.Progress.Clear();
        state.LastKnownAttackers.Clear();
        Broadcast(
            "mission",
            "Assignment dispatch",
            $"{p.Name}: {kind} assignment accepted. Follow your objective."
        );
        return "ok";
    }

    private string AbandonWorldJob(Player p)
    {
        var state = Living(p);
        if (state.RewardPending || p.EconomyPending)
            return "busy";
        if (state.IllegalCargo > 0 && state.Police.Length > 0)
            return "resolve_inspection_first";
        var legacy = ExplorationFor(p);
        legacy.Job = "";
        legacy.JobTargets = [];
        legacy.JobProgress.Clear();
        state.Job = state.JobState = "";
        state.Targets = [];
        state.IllegalCargo = 0;
        state.Reward = null;
        state.Progress.Clear();
        state.LastKnownAttackers.Clear();
        return "ok";
    }

    private string ClaimWorldJob(Player p)
    {
        var state = Living(p);
        if (state.JobState != "ready")
            return "assignment_incomplete";
        if (state.IllegalCargo > 0 && state.Police.Length > 0)
            return "resolve_inspection_first";
        if (!CanTurnIn(p))
            return "contact_required";
        if (p.EconomyPending || state.RewardPending)
            return "busy";
        var credits = (state.Job is "exploration" or "recovery" ? 40 : 60) * state.JobRegion;
        var xp = (state.Job is "exploration" or "recovery" ? 30 : 45) * state.JobRegion;
        var key =
            $"job:{Sector!.Seed}:{state.Job}:{string.Join(',', state.Targets)}:{state.JobCooldown}";
        var checkpoint = Checkpoint(p);
        p.EconomyPending = state.RewardPending = true;
        var sequence = p.LastReceived;
        var reward = state.Reward;
        PersistForRun(
            p,
            () => profiles.WorldReward(p.Name, key, credits, xp, checkpoint, reward),
            result =>
            {
                p.EconomyPending = state.RewardPending = false;
                if (result == "ok")
                {
                    p.Experience = Math.Min(Room.MaxExperience, p.Experience + xp);
                    state.Reward = null;
                    state.Job = "";
                    state.JobState = "";
                    state.Targets = [];
                    state.IllegalCargo = 0;
                    state.JobCooldown = Tick + 300 * TickRate;
                }
                ExplorationFor(p).Receipt = new(sequence, result);
            }
        );
        return profiles.BackgroundWrites ? "pending" : ExplorationFor(p).Receipt!.Result;
    }

    private void WorldEnemyDefeated(Enemy e)
    {
        foreach (var p in players.Values)
        {
            var state = Living(p);
            if (state.JobState != "active")
                continue;
            if (state.Job == "protection" && state.Targets[0] == e.Id)
            {
                state.JobState = "failed";
                continue;
            }
            if (!state.Targets.Contains(e.Id))
                continue;
            if (
                !Racing(p)
                && e.Contributors.TryGetValue(p.Id, out var c)
                && c.Run == p.RunId
                && c.Damage >= 1
            )
                state.Progress.Add(e.Id);
            if (state.Job is "bounty" or "interception")
                state.JobState = state.Progress.Contains(e.Id) ? "ready" : "failed";
        }
        if (civilians.FirstOrDefault(c => c.Ship == e) is { } lost)
        {
            // Reserved purchases may already be committing. Only unsold, unreserved goods become salvage.
            var value = lost.Stock.Where(s => !lost.Reserved.Contains(s.Id)).Sum(s => s.Price / 4);
            lost.Stock.RemoveAll(s => !lost.Reserved.Contains(s.Id));
            lost.CargoRevision++;
            AddCargo("disabled-ship", e.Position + new Vector2(70, 0), 60 + value);
        }
    }

    private void StepWorldJobs()
    {
        foreach (var p in players.Values)
        {
            var state = Living(p);
            if (state.Job == "smuggling" && state.JobState is "active" or "waiting")
            {
                if (!DeliveryStation(state.Targets[0]))
                {
                    var destination = DeliveryDestination(p);
                    if (destination != 0)
                    {
                        state.Targets = [destination];
                        state.JobState = "active";
                        Broadcast(
                            "mission",
                            p.Name,
                            "Delivery rerouted to "
                                + Location(destination)!.Name
                                + ". Original reward retained."
                        );
                    }
                    else
                        state.JobState = "waiting";
                }
                else if (state.JobState == "waiting")
                    state.JobState = "active";
            }
            if (state.JobState != "active")
                continue;
            if (
                !Racing(p)
                && state.Job == "smuggling"
                && state.IllegalCargo > 0
                && p.Docked == state.Targets[0]
            )
                state.JobState = "ready";
            if (
                state.Job is "bounty" or "interception" or "protection"
                && !enemies.Any(e => e.Id == state.Targets[0] && e.Hull > 0)
            )
                state.JobState = "failed";
            if (
                state.Job == "interception"
                && enemies.FirstOrDefault(e => e.Id == state.Targets[0]) is { State: "arrived" }
            )
                state.JobState = "failed";
            if (
                state.Job == "protection"
                && state.JobState == "active"
                && state.Targets.Skip(1).All(id => !enemies.Any(e => e.Id == id && e.Hull > 0))
            )
                state.JobState =
                    !Racing(p)
                    && state.Progress.Count > 0
                    && enemies.Any(e => e.Id == state.Targets[0] && e.Hull > 0)
                        ? "ready"
                        : "failed";
            if (
                state.Job is "exploration" or "recovery"
                && !worldCargo.Any(c => c.Id == state.Targets[0])
            )
                state.JobState = "failed";
        }
    }

    private string ScanAnomaly(Player p)
    {
        var state = Living(p);
        if (state.Job != "exploration" || state.JobState != "active")
            return "no_assignment";
        var target = worldCargo.FirstOrDefault(c => c.Id == state.Targets[0]);
        if (target is null)
            return "target_lost";
        var requiredRange = new[] { 2000, 900, 180 }[Math.Min(2, state.Progress.Count)];
        if (Vector2.Distance(p.Position, target.Position) > requiredRange)
            return "signal_too_weak";
        if (state.Progress.Count >= 2 && !ServiceLine(p.Position, target.Position))
            return "signal_obstructed";
        state.Progress.Add(state.Progress.Count);
        Broadcast(
            "mission",
            "Sensors",
            state.Progress.Count >= 3
                ? "Anomaly identified. Return for payment."
                : $"Signal narrowed. Bearing {MathF.Atan2(target.Position.Y - p.Position.Y, target.Position.X - p.Position.X) * 180 / MathF.PI:F0} degrees."
        );
        if (state.Progress.Count >= 3)
        {
            state.JobState = "ready";
            // The frozen technology belongs to this completed assignment. It cannot be
            // reserved again or taken by a scavenger while payment is awaiting retry.
            worldCargo.Remove(target);
        }
        return "ok";
    }

    private string ExploreDerelict(Player p, int target, string choice)
    {
        var wreck = worldCargo.FirstOrDefault(c => c.Id == target && c.Kind == "derelict");
        if (
            wreck is null
            || Vector2.Distance(p.Position, wreck.Position) > 180
            || !ServiceLine(p.Position, wreck.Position)
        )
            return "out_of_range";
        var key = $"{FriendBook.Key(p.Name)}:{Sector!.Seed}:{target}";
        if (resolvedDerelicts.Contains(key))
            return "already_searched";
        if (choice == "abandon")
            return "ok";
        if (p.EconomyPending)
            return "busy";
        // The clue is derived from the wreck identity, never from the selected choice or retry.
        var safe = target % 3;
        if (choice is not ("reactor" or "cargo" or "navigation"))
            return "invalid_choice";
        var selected =
            choice == "reactor" ? 0
            : choice == "cargo" ? 1
            : 2;
        var payout = selected == safe ? wreck.Credits : wreck.Credits / 4;
        var checkpoint = Checkpoint(p);
        p.EconomyPending = true;
        var committed = false;
        var sequence = p.LastReceived;
        PersistForRun(
            p,
            () =>
            {
                var result = profiles.WorldReward(
                    p.Name,
                    "derelict:" + key,
                    payout,
                    selected == safe ? 30 : 0,
                    checkpoint,
                    selected == safe ? wreck.Reward : null
                );
                committed = result == "ok";
                return result;
            },
            result =>
            {
                p.EconomyPending = false;
                if (committed)
                    resolvedDerelicts.Add(key);
                ExplorationFor(p).Receipt = new(sequence, result);
                if (result == "ok")
                {
                    resolvedDerelicts.Add(key);
                    if (selected == safe)
                        p.Experience = Math.Min(MaxExperience, p.Experience + 30);
                    Broadcast(
                        "mission",
                        "Salvage log",
                        selected == safe
                            ? "Evidence confirmed. Valuable salvage recovered."
                            : "Compartment compromised. Only basic salvage recovered."
                    );
                }
            }
        );
        return profiles.BackgroundWrites ? "pending" : ExplorationFor(p).Receipt!.Result;
    }
}
