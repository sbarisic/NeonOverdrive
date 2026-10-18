using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    // Null means that the legacy handler owns this action's validation. Empty means eligible.
    private string? ActionReason(Player p, string action, int target)
    {
        string Reach(Vector2 at, float range = 180) =>
            Vector2.Distance(p.Position, at) > range
                ? range == 100
                    ? "search_out_of_range"
                    : "out_of_range"
                : !ServiceLine(p.Position, at)
                    ? "blocked"
                    : "";
        var state = Living(p);
        var exploration = ExplorationFor(p);
        if (action == "dock")
        {
            var station = Location(target);
            return station?.Kind != "station" ? "target_gone"
                : !DeliveryStation(target) ? "station_contested"
                : Reach(station.Position);
        }
        if (action == "repair")
        {
            var cost = (MaxHullFor(p) - p.Hull + (p.HullFraction > 0 ? 1 : 0)) * 10;
            return p.Docked == 0 ? "not_docked"
                : p.EconomyPending ? "busy"
                : cost <= 0 ? "already_repaired"
                : profiles.Get(p.Name).Credits < cost ? "insufficient_credits"
                : "";
        }
        if (action == "tow")
        {
            if (target == 0)
                return "";
            var cargo = worldCargo.FirstOrDefault(c => c.Id == target);
            return state.Tow != 0 ? "already_towing"
                : cargo is null
                || cargo.Pending
                || cargo.Owner != 0
                || cargo.Kind is "derelict" or "anomaly"
                    ? "tow_unavailable"
                : Reach(cargo.Position);
        }
        if (action == "interact")
        {
            var location = Location(target);
            if (location is null)
                return "target_gone";
            if (p.Docked != 0)
                return "undock_first";
            if (p.Editing)
                return "close_menu_first";
            var reach = Reach(location.Position, 100);
            if (reach.Length > 0)
                return reach;
            if (location.Kind == "cache")
                return exploration.Caches.Contains(target) ? "cache_already_searched" : "";
            var contract = Contracts.FirstOrDefault(c => c.Id == exploration.Contract);
            return
                contract is not null
                && (
                    contract.Kind == "survey"
                        && location.Kind == "beacon"
                        && !exploration.Progress.Contains(target)
                    || contract.Kind == "recover"
                        && target == contract.Target
                        && !exploration.Carrying
                )
                ? ""
                : "assignment_unavailable";
        }
        if (action == "hail")
        {
            var contact = contacts.FirstOrDefault(c => c.Ship?.Id == target && c.Ship.Hull > 0);
            return contact?.Ship is null ? "target_gone"
                : p.Docked != 0 ? "undock_first"
                : Reach(contact.Ship.Position);
        }
        if (action == "convoy_buy")
        {
            var agent = civilians.FirstOrDefault(c => c.Stock.Any(s => s.Id == target));
            if (agent is null || agent.Ship.Hull <= 0)
                return "stock_unavailable";
            if (p.EconomyPending)
                return "busy";
            if (agent.Reserved.Contains(target))
                return "stock_reserved";
            if (ExplorationFor(p).Hailing != agent.Ship.Id)
                return "open_close_range_trade";
            var range = Reach(agent.Ship.Position);
            if (range.Length > 0)
                return range;
            return profiles.Get(p.Name).Credits < agent.Stock.First(s => s.Id == target).Price
                ? "insufficient_credits"
                : "";
        }
        if (action is "radio" or "radio_trade")
        {
            var ship = enemies.FirstOrDefault(e => e.Id == target && e.Allied && e.Hull > 0);
            if (ship is null)
                return "target_gone";
            if (Vector2.Distance(ship.Position, p.Position) > Detection(p))
                return "out_of_sensor_range";
            if (action == "radio")
                return "";
            if (
                !civilians.Any(c => c.Ship == ship && c.Ship.Role == NpcRole.Trader)
                && !contacts.Any(c => c.Ship == ship && c.Role == "trader")
            )
                return "trade_unavailable";
            return p.Docked != 0 ? "undock_first" : Reach(ship.Position);
        }
        if (action is "activate" or "turret_one" or "turret_two" or "repair_bot")
        {
            if (!salvageSites.TryGetValue(target, out var site))
                return "target_gone";
            if (territory.TryGetValue(target, out var control) && !control.Friendly)
                return "capture_required";
            if (p.Docked != 0)
                return "undock_first";
            var range = Reach(site.Location.Position);
            if (range.Length > 0)
                return range;
            var slot =
                action == "activate" ? 0
                : action == "turret_one" ? 1
                : action == "turret_two" ? 2
                : 3;
            if (p.EconomyPending || site.Reserved[slot])
                return "busy";
            if (site.Units[slot] is { Hull: > 0 } || slot > 0 && !site.Active)
                return "socket_unavailable";
            if (
                enemies.Count + salvageSites.Values.Sum(s => s.Reserved.Count(v => v))
                >= EnemyLimit
            )
                return "npc_capacity_reached";
            return profiles.Get(p.Name).Credits < (slot == 3 ? 150 : 100)
                ? "insufficient_credits"
                : "";
        }
        if (action == "mission_claim")
            return state.JobState != "ready" ? "assignment_incomplete"
                : !CanTurnIn(p) ? "contact_required"
                : state.IllegalCargo > 0 && state.Police.Length > 0 ? "resolve_inspection_first"
                : p.EconomyPending || state.RewardPending ? "busy"
                : "";
        if (action.StartsWith("mission_") && action is not ("mission_abandon" or "mission_scan"))
        {
            if (state.Job.Length > 0 || ExplorationFor(p).Job.Length > 0)
                return "assignment_active";
            if (Tick < Math.Max(state.JobCooldown, ExplorationFor(p).JobCooldown))
                return "assignment_cooldown";
            if (
                !DeliveryStation(p.Docked)
                && !enemies.Any(e =>
                    e.Id == state.Conversation
                    && MissionContact(e)
                    && Vector2.Distance(p.Position, e.Position) <= Detection(p)
                )
            )
                return "contact_required";
            var reserved = livingPilots
                .Values.Where(s => s.JobState is "active" or "waiting")
                .SelectMany(s => s.Targets)
                .ToHashSet();
            var kind = action[8..];
            var exists = kind switch
            {
                "smuggling" => DeliveryDestination(p, p.Docked) != 0,
                "bounty" or "interception" => enemies.Any(e =>
                    !e.Allied
                    && !Stationary(e)
                    && e.SurfacePlanet == 0
                    && e.Hull > 0
                    && !reserved.Contains(e.Id)
                    && (kind == "bounty" || e.Role == NpcRole.Courier && e.State != "arrived")
                ),
                "exploration" => worldCargo.Any(c =>
                    c.Kind == "anomaly" && !reserved.Contains(c.Id)
                ),
                "recovery" => worldCargo.Any(c =>
                    c.Kind is "container" or "disabled-ship"
                    && c.Owner == 0
                    && !c.Pending
                    && !reserved.Contains(c.Id)
                ),
                "protection" => civilians.Any(c =>
                    c.Ship.Hull > 0
                    && c.Ship.Role is NpcRole.Trader or NpcRole.Miner
                    && !reserved.Contains(c.Ship.Id)
                    && enemies.Any(e =>
                        !e.Allied
                        && e.Hull > 0
                        && !e.Installation
                        && e.NpcTarget == c.Ship.Id
                        && Vector2.Distance(e.Position, c.Ship.Position) < 900
                    )
                ),
                _ => false,
            };
            return exists ? "" : "no_eligible_target";
        }
        if (action == "race_join")
            return target is < 0 or > 2 ? "invalid_race"
                : Racing(p) ? "already_racing"
                : p.Menu
                || p.Editing
                || p.Docked != 0
                || p.EconomyPending
                || state.Tow != 0
                || state.Police.Length > 0
                || state.IllegalCargo > 0
                || p.LastDamage >= 0 && Tick - p.LastDamage < 5 * TickRate
                    ? "race_not_ready"
                : Vector2.Distance(p.Position, Location(10)!.Position + new Vector2(400, 0)) > 500
                    ? "return_to_race_start"
                : "";
        return null;
    }

    private void AddActionAvailability(Player p, List<ServiceAvailability> output)
    {
        output.Add(new(0, "repair", ActionReason(p, "repair", 0) ?? ""));
        foreach (
            var stock in civilians
                .Where(c => c.Ship.Id == Living(p).Conversation)
                .SelectMany(c => c.Stock)
        )
            output.Add(new(stock.Id, "convoy_buy", ActionReason(p, "convoy_buy", stock.Id) ?? ""));
        foreach (
            var kind in new[]
            {
                "bounty",
                "interception",
                "smuggling",
                "recovery",
                "exploration",
                "protection",
            }
        )
            output.Add(new(0, "mission_" + kind, ActionReason(p, "mission_" + kind, 0) ?? ""));
        for (var i = 0; i < 3; i++)
            output.Add(new(i, "race_join", ActionReason(p, "race_join", i) ?? ""));
    }
}
