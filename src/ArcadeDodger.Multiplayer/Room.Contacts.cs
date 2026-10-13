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

public sealed record ServiceSnapshot(
    ContactSnapshot[] Contacts,
    OutpostSnapshot[] Outposts,
    RaidSnapshot? Raid,
    int Hailing,
    int[] ClaimedCaches,
    AssignmentSnapshot Assignment,
    int[] KnownCaches
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
                    Location(10)!.Position
                    + new Vector2(-100 + contacts.IndexOf(contact) * 100, 100);
                contact.Ship = new()
                {
                    Id = ++nextEnemy,
                    Type = "contact",
                    Allied = true,
                    Home = 10,
                    Position = at,
                    Previous = at,
                    Hull = 120,
                    MaxHull = 120,
                    Shield = 60,
                    MaxShield = 60,
                };
                enemies.Add(contact.Ship);
                contact.Destination = 11;
                contact.DepartAt = Tick + 60 * TickRate;
            }
        }
        foreach (var p in players.Values)
        {
            var state = ExplorationFor(p);
            if (state.Hailing == 0)
                continue;
            var npc = contacts.FirstOrDefault(c => c.Ship?.Id == state.Hailing)?.Ship;
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
        npc.State = "contact";
        if (
            players.Values.Any(p =>
                p.Connected && p.Hull > 0 && ExplorationFor(p).Hailing == npc.Id
            )
        )
            return true;
        if (Tick < contact.DepartAt)
            return true;
        var destination = Location(contact.Destination)!.Position + new Vector2(150, 0);
        if (Vector2.Distance(npc.Position, destination) < 180)
        {
            contact.Destination = 10 + (contact.Destination - 9) % 3;
            contact.DepartAt = Tick + 60 * TickRate;
            return true;
        }
        var delta = Navigate(npc, destination) - npc.Position;
        if (delta.LengthSquared() > 1)
        {
            npc.Aim = MathF.Atan2(delta.Y, delta.X);
            npc.Position = MoveShip(
                npc.Position,
                npc.Position + Vector2.Normalize(delta) * 100 / TickRate,
                EnemyRadius(npc)
            );
        }
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
            if (
                p.Docked != 0
                || contact?.Ship is not { } ship
                || Vector2.Distance(p.Position, ship.Position) > 180
                || !ServiceLine(p.Position, ship.Position)
            )
                return "out_of_range";
            state.Hailing = ship.Id;
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
            PersistForRun(
                p,
                () => profiles.Credit(p.Name, reward),
                result =>
                {
                    p.EconomyPending = false;
                    if (result == "ok")
                    {
                        p.Experience = Math.Min(
                            MaxExperience,
                            p.Experience + (state.Job == "explorer" ? 30 : 45)
                        );
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
        if (state.Job.Length > 0 || Tick < state.JobCooldown)
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

    private ServiceSnapshot ServicesFor(Player p)
    {
        var state = ExplorationFor(p);
        return new(
            contacts
                .Where(c => c.Ship is { Hull: > 0 })
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
            state.Discovered.Where(id => Location(id)?.Kind == "cache").ToArray()
        );
    }
}
