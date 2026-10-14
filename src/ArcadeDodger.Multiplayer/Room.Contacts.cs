using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed record ContactSnapshot(int Id, string Name, string Role, float X, float Y, int Hull);

public sealed record AssignmentSnapshot(
    string Kind,
    int Progress,
    int[] Targets,
    bool Ready,
    int Credits,
    int Experience,
    int Cooldown
);

public sealed record ServiceAvailability(int Target, string Action, string Reason);

public sealed record ServiceSnapshot(
    ContactSnapshot[] Contacts,
    OutpostSnapshot[] Outposts,
    RaidSnapshot? Raid,
    int Hailing,
    int[] ClaimedCaches,
    AssignmentSnapshot Assignment,
    int[] KnownCaches,
    ServiceAvailability[]? Availability = null
);

public sealed partial class Room
{
    private bool ServiceLine(Vector2 from, Vector2 to) => TraceCover(from, to) is null;

    private sealed class ContactShip
    {
        public required string Name,
            Role;
        public Enemy? Ship;
        public int Destination = 10;
        public long DepartAt,
            ReplaceAt;
    }

    private readonly List<ContactShip> contacts = [];

    private void InitializeContacts()
    {
        if (Sector is null || !enemiesEnabled)
            return;
        var roles = new[] { "trader", "explorer", "bounty" };
        var random = new Random(Sector.Seed ^ 4721);
        random.Shuffle(roles);
        for (var i = 0; i < 3; i++)
            contacts.Add(
                new()
                {
                    Name = new[]
                    {
                        "Mira / Wayfarer",
                        "Orin / Copper Finch",
                        "Vela / Blue Lantern",
                    }[i],
                    Role = roles[i],
                }
            );
        StepContacts();
    }

    private void StepContacts()
    {
        foreach (var contact in contacts)
        {
            if (contact.Ship is { Hull: <= 0 })
            {
                contact.Ship = null;
                contact.ReplaceAt = Tick + 300 * TickRate;
            }
            if (contact.Ship is null && Tick >= contact.ReplaceAt && enemies.Count < EnemyLimit)
            {
                var at =
                    Location(10)!.Position + new Vector2(220, -80 + contacts.IndexOf(contact) * 80);
                if (!TryLocalSpawn(at, ChassisCatalog.Get(3).Radius * EnemyCatalog.Size(1), out at))
                    continue;
                contact.Ship = new()
                {
                    Id = ++nextEnemy,
                    Type = "contact",
                    Role =
                        contact.Role == "trader" ? NpcRole.Trader
                        : contact.Role == "bounty" ? NpcRole.Mercenary
                        : NpcRole.Scavenger,
                    Allied = true,
                    Home = 10,
                    Position = at,
                    Previous = at,
                    Hull = 120,
                    MaxHull = 120,
                    Shield = 60,
                    MaxShield = 60,
                };
                AddEnemy(contact.Ship);
                contact.Destination = 11;
                contact.DepartAt = Tick + 60 * TickRate;
            }
        }
        foreach (var p in players.Values)
        {
            var state = ExplorationFor(p);
            if (state.Hailing == 0)
                continue;
            var npc = enemies.FirstOrDefault(e => e.Id == state.Hailing && e.Allied);
            if (
                !p.Connected
                || p.Hull <= 0
                || npc is null
                || npc.Hull <= 0
                || Vector2.Distance(p.Position, npc.Position) > 180
                || !ServiceLine(p.Position, npc.Position)
            )
                EndHail(p, state);
        }
    }

    private bool MoveContact(Enemy npc)
    {
        if (npc.Type != "contact")
            return false;
        var contact = contacts.FirstOrDefault(c => c.Ship == npc);
        if (contact is null)
            return true;
        npc.State = Tick < contact.DepartAt ? "docking" : "traveling";
        if (
            players.Values.Any(p =>
                p.Connected && p.Hull > 0 && ExplorationFor(p).Hailing == npc.Id
            )
        )
            return true;
        if (Tick < contact.DepartAt)
            return true;
        if (!FriendlyStation(contact.Destination))
            contact.Destination = NextFriendlyStation(npc.Home);
        var destination = Location(contact.Destination)!.Position + new Vector2(150, 0);
        if (Vector2.Distance(npc.Position, destination) < 180 && npc.Velocity.LengthSquared() < 100)
        {
            npc.Home = contact.Destination;
            contact.Destination = NextFriendlyStation(npc.Home);
            contact.DepartAt = Tick + 60 * TickRate;
            return true;
        }
        FlyNpc(npc, destination, 100, 120);
        return true;
    }

    private void EndHail(Player p, Exploration state)
    {
        state.Hailing = 0;
        p.Editing = false;
        p.Menu = false;
        p.Held = null;
        p.Pending = null;
        p.Throttle = 0;
        p.Velocity = Vector2.Zero;
    }

    private bool TraderAvailable(Player p) =>
        contacts.Any(c =>
            c.Role == "trader"
            && c.Ship is { Hull: > 0 } ship
            && ExplorationFor(p).Hailing == ship.Id
            && Vector2.Distance(p.Position, ship.Position) <= 180
        );

    private string ContactAction(Player p, Exploration state, SectorActionMessage request)
    {
        if (request.Action == "goodbye")
        {
            EndHail(p, state);
            return "ok";
        }
        var contact = contacts.FirstOrDefault(c => c.Ship?.Id == request.Target && c.Ship.Hull > 0);
        if (request.Action == "hail")
        {
            if (p.Docked != 0)
                return "undock_first";
            if (contact?.Ship is not { } ship)
                return "target_gone";
            if (Vector2.Distance(p.Position, ship.Position) > 180)
                return "out_of_range";
            if (!ServiceLine(p.Position, ship.Position))
                return "blocked";
            state.Hailing = ship.Id;
            p.Pending = p.Held = null;
            state.InteractionTarget = 0;
            p.Editing = true;
            p.Menu = true;
            p.Throttle = 0;
            p.Velocity = Vector2.Zero;
            return "ok";
        }
        if (request.Action == "job_claim")
        {
            if (p.Docked == 0 && state.Hailing == 0)
                return "hail_or_dock";
            if (state.Job.Length == 0 || state.JobProgress.Count < 3)
                return "incomplete";
            if (p.EconomyPending)
                return "busy";
            p.EconomyPending = true;
            state.Receipt = new(request.Sequence, "pending");
            var reward = state.Job == "explorer" ? 40 : 60;
            var xp = state.Job == "explorer" ? 30 : 45;
            var checkpoint = Checkpoint(p);
            var transaction =
                $"contact-job:{Sector!.Seed}:{state.Job}:{state.JobCooldown}:{string.Join(',', state.JobTargets)}";
            PersistForRun(
                p,
                () => profiles.WorldReward(p.Name, transaction, reward, xp, checkpoint),
                result =>
                {
                    p.EconomyPending = false;
                    if (result == "ok")
                    {
                        p.Experience = Math.Min(MaxExperience, p.Experience + xp);
                        state.Job = "";
                        state.JobProgress.Clear();
                        state.JobTargets = [];
                        state.JobCooldown = Tick + 300 * TickRate;
                    }
                    state.Receipt = new(request.Sequence, result);
                }
            );
            return profiles.BackgroundWrites ? "pending" : state.Receipt.Result;
        }
        if (contact is null || state.Hailing != request.Target || contact.Role == "trader")
            return "unavailable";
        if (
            state.Job.Length > 0
            || Living(p).Job.Length > 0
            || Tick < Math.Max(state.JobCooldown, Living(p).JobCooldown)
        )
            return "assignment_unavailable";
        var targets =
            contact.Role == "explorer"
                ? Sector!
                    .Locations.Where(l => l.Kind == "cache" && !state.Caches.Contains(l.Id))
                    .OrderBy(l => Vector2.DistanceSquared(l.Position, p.Position))
                    .Take(3)
                    .Select(l => l.Id)
                    .ToArray()
                : [];
        if (contact.Role == "explorer" && targets.Length < 3)
            return "no_unsearched_caches";
        state.Job = contact.Role;
        state.JobProgress.Clear();
        state.JobTargets = targets;
        return "ok";
    }

    private void SearchCache(Player p, Exploration state, SectorLocation cache)
    {
        if (!state.Caches.Add(cache.Id))
            return;
        if (state.Job == "explorer" && state.JobTargets.Contains(cache.Id))
            state.JobProgress.Add(cache.Id);
        CombatFx(0, 5, cache.Position, 2);
        var level = cache.Parent;
        pendingCredits.Add(
            new(++nextCredit, cache.Position, 20 * level, Tick + EnergyLifetimeTicks, p.Id)
        );
        pendingEnergy.Add(
            new(++nextEnergy, cache.Position, 10 * level, Tick + EnergyLifetimeTicks, p.Id)
        );
        p.LootNotice = "Cache searched: collect your personal credits and XP";
        p.LootNoticeUntil = Tick + 240;
    }

    private ServiceAvailability[] ServiceAvailabilityFor(Player p)
    {
        var result = new List<ServiceAvailability>();
        string Range(Vector2 target) =>
            Vector2.Distance(p.Position, target) > 180 ? "out_of_range"
            : !ServiceLine(p.Position, target) ? "blocked"
            : "";
        var state = ExplorationFor(p);
        var contract = Contracts.FirstOrDefault(c => c.Id == state.Contract);
        foreach (
            var l in Sector!.Locations.Where(l =>
                l.Kind is "station" or "cache" or "beacon" or "wreck"
            )
        )
        {
            if (Vector2.Distance(p.Position, l.Position) > 240)
                continue;
            var action = l.Kind == "station" ? "dock" : "interact";
            var reason = Range(l.Position);
            if (p.Docked != 0)
                reason = "undock_first";
            else if (
                l.Kind == "station"
                && territory.TryGetValue(l.Id, out var control)
                && (!control.Friendly || control.Contested)
            )
                reason = "station_contested";
            else if (l.Kind != "station")
            {
                if (Vector2.Distance(p.Position, l.Position) > 100)
                    reason = "out_of_range";
                else if (
                    l.Kind == "cache"
                        ? state.Caches.Contains(l.Id)
                        : !(
                            contract?.Kind == "survey"
                                && l.Kind == "beacon"
                                && !state.Progress.Contains(l.Id)
                            || contract?.Kind == "recover"
                                && contract.Target == l.Id
                                && !state.Carrying
                        )
                )
                    reason = "assignment_unavailable";
            }
            result.Add(new(l.Id, action, reason));
        }
        foreach (var c in contacts.Where(c => c.Ship is { Hull: > 0 }))
            result.Add(new(c.Ship!.Id, "hail", Range(c.Ship.Position)));
        foreach (
            var ship in enemies.Where(e =>
                e.Allied && e.Hull > 0 && Vector2.Distance(p.Position, e.Position) <= Detection(p)
            )
        )
            if (
                civilians.Any(c => c.Ship == ship && c.Ship.Role == NpcRole.Trader)
                || contacts.Any(c => c.Ship == ship && c.Role == "trader")
            )
                result.Add(
                    new(
                        ship.Id,
                        "radio_trade",
                        p.Docked != 0 ? "undock_first" : Range(ship.Position)
                    )
                );
        foreach (var site in salvageSites.Values)
        {
            var reason =
                territory.TryGetValue(site.Location.Id, out var control) && !control.Friendly
                    ? "capture_required"
                    : Range(site.Location.Position);
            foreach (var action in new[] { "activate", "turret_one", "turret_two", "repair_bot" })
                result.Add(new(site.Location.Id, action, reason));
        }
        var living = Living(p);
        if (living.Tow != 0)
            result.Add(new(0, "tow", ""));
        foreach (
            var cargo in worldCargo.Where(c => Vector2.Distance(p.Position, c.Position) <= 240)
        )
            if (cargo.Kind is not ("derelict" or "anomaly"))
                result.Add(
                    new(
                        cargo.Id,
                        "tow",
                        living.Tow != 0 ? "already_towing"
                            : cargo.Pending || cargo.Owner != 0 ? "tow_unavailable"
                            : Range(cargo.Position)
                    )
                );
        if (living.Job == "exploration" && living.JobState == "active")
        {
            var target = worldCargo.FirstOrDefault(c => c.Id == living.Targets.FirstOrDefault());
            var reach = new[] { 2000, 900, 180 }[Math.Min(2, living.Progress.Count)];
            result.Add(
                new(
                    0,
                    "mission_scan",
                    target is null ? "target_lost"
                        : Vector2.Distance(p.Position, target.Position) > reach ? "signal_too_weak"
                        : living.Progress.Count >= 2 && !ServiceLine(p.Position, target.Position)
                            ? "signal_obstructed"
                        : ""
                )
            );
        }
        if (living.JobState == "ready")
            result.Add(
                new(
                    0,
                    "mission_claim",
                    p.EconomyPending || living.RewardPending ? "busy"
                        : living.IllegalCargo > 0 && living.Police.Length > 0
                            ? "resolve_inspection_first"
                        : CanTurnIn(p) ? ""
                        : "contact_required"
                )
            );
        AddActionAvailability(p, result);
        return result
            .Select(v => v with { Reason = ActionReason(p, v.Action, v.Target) ?? v.Reason })
            .ToArray();
    }

    private ServiceSnapshot ServicesFor(Player p)
    {
        var state = ExplorationFor(p);
        return new(
            contacts
                .Where(c =>
                    c.Ship is { Hull: > 0 }
                    && Vector2.Distance(c.Ship.Position, p.Position) <= Detection(p)
                )
                .Select(c => new ContactSnapshot(
                    c.Ship!.Id,
                    c.Name,
                    c.Role,
                    c.Ship.Position.X,
                    c.Ship.Position.Y,
                    c.Ship.Hull
                ))
                .ToArray(),
            OutpostsState(),
            RaidState(),
            state.Hailing,
            state.Caches.Order().ToArray(),
            new(
                state.Job,
                state.JobProgress.Count,
                state.JobTargets,
                state.JobProgress.Count >= 3,
                state.Job == "explorer" ? 40 : 60,
                state.Job == "explorer" ? 30 : 45,
                (int)Math.Max(0, (state.JobCooldown - Tick) / TickRate)
            ),
            state.Discovered.Where(id => Location(id)?.Kind == "cache").ToArray(),
            ServiceAvailabilityFor(p)
        );
    }
}
