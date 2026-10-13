using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed record StructureSnapshot(
    int Slot,
    float X,
    float Y,
    int Hull,
    int MaxHull,
    int Cost,
    bool Pending
);

public sealed record OutpostSnapshot(
    int Id,
    string Name,
    float X,
    float Y,
    bool Active,
    StructureSnapshot[] Sockets
);

public sealed record RaidSnapshot(int Site, string State, int Remaining);

public sealed partial class Room
{
    private sealed class SalvageSite
    {
        public required SectorLocation Location;
        public required Vector2[] Positions;
        public int Level,
            Generation;
        public Enemy?[] Units = new Enemy?[4];
        public bool[] Reserved = new bool[4];
        public bool Active => Units[0] is { Hull: > 0 };
    }

    private sealed class ConstructionRefund
    {
        public required Player Pilot;
        public required string Run;
        public required SalvageSite Site;
        public int Slot,
            Cost,
            Sequence;
        public bool Pending;
    }

    private readonly Dictionary<int, SalvageSite> salvageSites = [];
    private readonly List<ConstructionRefund> constructionRefunds = [];
    private HashSet<int> raiders = [];
    private readonly HashSet<int> returningRaiders = [];
    private int raidSite;
    private long raidAt = -1;
    private int raidCount;

    private void InitializeOutposts()
    {
        if (Sector is null)
            return;
        foreach (var wreck in Sector.Locations.Where(l => l.Kind == "wreck"))
        {
            var positions = new List<Vector2>();
            // Search fixed rings; each socket must have clear space and an approach.
            for (var ring = 140; positions.Count < 4 && ring <= 700; ring += 70)
            for (var i = 0; i < 24 && positions.Count < 4; i++)
            {
                var angle = i * MathF.Tau / 24;
                var at = wreck.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ring;
                if (
                    positions.Any(p => Vector2.Distance(p, at) < 100)
                    || geometry!.TraceShips(at, at, 40) is not null
                    || geometry.TraceShips(wreck.Position, at, Radius) is not null
                )
                    continue;
                positions.Add(at);
            }
            if (positions.Count != 4)
                throw new InvalidOperationException("No reachable salvage sockets");
            var level = Sector
                .Locations.Where(l => l.Kind == "planet")
                .MinBy(l => Vector2.DistanceSquared(l.Position, wreck.Position))!
                .Id;
            salvageSites[wreck.Id] = new()
            {
                Location = wreck,
                Positions = positions.ToArray(),
                Level = level,
            };
        }
    }

    private string BuildOutpost(Player p, Exploration state, SectorActionMessage request)
    {
        var slot = request.Action switch
        {
            "activate" => 0,
            "turret_one" => 1,
            "turret_two" => 2,
            _ => 3,
        };
        if (territory.TryGetValue(request.Target, out var control) && !control.Friendly)
            return "capture_required";
        if (
            !salvageSites.TryGetValue(request.Target, out var site)
            || Vector2.Distance(p.Position, site.Location.Position) > 180
            || !ServiceLine(p.Position, site.Location.Position)
            || p.Docked != 0
        )
            return "out_of_range";
        if (p.EconomyPending || site.Reserved[slot])
            return "busy";
        if (site.Units[slot] is { Hull: > 0 } || slot > 0 && !site.Active)
            return "socket_unavailable";
        if (enemies.Count + salvageSites.Values.Sum(s => s.Reserved.Count(b => b)) >= EnemyLimit)
            return "npc_capacity_reached";
        var cost = slot == 3 ? 150 : 100;
        var generation = site.Generation;
        var run = p.RunId;
        site.Reserved[slot] = true;
        p.EconomyPending = true;
        state.Receipt = new(request.Sequence, "pending");
        Persist(
            () => profiles.ForRun(p.Name, run, () => profiles.Debit(p.Name, cost)),
            result =>
            {
                if (
                    result == "ok"
                    && (
                        p.Hull <= 0
                        || !p.Connected
                        || p.RunId != run
                        || profiles.Get(p.Name).RunId != run
                        || Vector2.Distance(p.Position, site.Location.Position) > 180
                        || !ServiceLine(p.Position, site.Location.Position)
                        || generation != site.Generation
                        || territory.TryGetValue(site.Location.Id, out var owner) && !owner.Friendly
                        || slot > 0 && !site.Active
                        || enemies.Count >= EnemyLimit
                    )
                )
                {
                    constructionRefunds.Add(
                        new()
                        {
                            Pilot = p,
                            Run = run,
                            Site = site,
                            Slot = slot,
                            Cost = cost,
                            Sequence = request.Sequence,
                        }
                    );
                    state.Receipt = new(request.Sequence, "refund_pending");
                    return;
                }
                if (result == "ok")
                {
                    var hull = (int)
                        MathF.Ceiling((slot == 0 ? 120 : 60) * EnemyCatalog.Health(site.Level));
                    var unit = new Enemy
                    {
                        Id = ++nextEnemy,
                        Home = site.Location.Id,
                        Allied = true,
                        Type =
                            slot == 0 ? "salvage-core"
                            : slot == 3 ? "repair-bot"
                            : "salvage-turret",
                        Gunner = slot is 1 or 2,
                        Hull = hull,
                        MaxHull = hull,
                        Level = site.Level,
                        Position = site.Positions[slot],
                        Previous = site.Positions[slot],
                        FireAt = Tick + 30,
                    };
                    site.Units[slot] = unit;
                    enemies.Add(unit);
                    if (raidAt < 0)
                        raidAt = Tick + 240 * TickRate;
                }
                site.Reserved[slot] = false;
                p.EconomyPending = false;
                state.Receipt = new(request.Sequence, result);
            }
        );
        return profiles.BackgroundWrites ? "pending" : state.Receipt.Result;
    }

    private void StepOutposts()
    {
        returningRaiders.RemoveWhere(id => !enemies.Any(e => e.Id == id && e.Hull > 0));
        foreach (var refund in constructionRefunds.Where(r => !r.Pending).ToArray())
        {
            refund.Pending = true;
            Persist(
                () =>
                    profiles.ForRun(
                        refund.Pilot.Name,
                        refund.Run,
                        () => profiles.Credit(refund.Pilot.Name, refund.Cost)
                    ),
                result =>
                {
                    refund.Pending = false;
                    if (result is not ("ok" or "stale_run"))
                        return;
                    refund.Site.Reserved[refund.Slot] = false;
                    refund.Pilot.EconomyPending = false;
                    ExplorationFor(refund.Pilot).Receipt = new(
                        refund.Sequence,
                        "construction_cancelled_refunded"
                    );
                    constructionRefunds.Remove(refund);
                }
            );
        }
        foreach (var site in salvageSites.Values)
        {
            if (site.Units[0] is { Hull: <= 0 })
            {
                site.Generation++;
                foreach (var unit in site.Units)
                    if (unit is not null)
                        unit.Hull = 0;
                site.Units = new Enemy?[4];
            }
            if (!site.Active || site.Units[3] is not { Hull: > 0 } repair)
                continue;
            const float amount = .5f / TickRate;
            foreach (var ally in enemyGrid.Query(repair.Position, repair.Position, 350))
                if (
                    ally != repair
                    && ally.Allied == repair.Allied
                    && ally.Hull > 0
                    && Vector2.DistanceSquared(ally.Position, repair.Position) <= 350 * 350
                    && ServiceLine(repair.Position, ally.Position)
                )
                    HealHull(ref ally.Hull, ref ally.DamageFraction, ally.MaxHull, amount);
            foreach (var pilot in players.Values)
                if (
                    repair.Allied
                    && pilot.Hull > 0
                    && Vector2.DistanceSquared(pilot.Position, repair.Position) <= 350 * 350
                    && ServiceLine(repair.Position, pilot.Position)
                )
                    HealHull(ref pilot.Hull, ref pilot.HullFraction, MaxHullFor(pilot), amount);
        }
        StepRaid();
    }

    private static void HealHull(ref int hull, ref float fraction, int maximum, float amount)
    {
        if (hull <= 0 || hull == maximum && fraction <= 0)
            return;
        var health = Math.Min(maximum, hull - fraction + amount);
        hull = (int)MathF.Ceiling(health);
        fraction = hull - health;
    }

    private void StepRaid()
    {
        if (!enemiesEnabled || Sector is null)
            return;
        if (raidSite != 0)
        {
            if (
                territory.TryGetValue(raidSite, out var defendedSite)
                && defendedSite.Friendly
                && enemies.Any(e => raiders.Contains(e.Id) && e.Hull > 0)
            )
                return;
            foreach (var e in enemies.Where(e => raiders.Contains(e.Id)))
            {
                e.NpcTarget = 0;
                e.State = "return";
                returningRaiders.Add(e.Id);
            }
            raidSite = 0;
            raiders.Clear();
            raidCount++;
            raidAt = Tick + (int)((300 - 120 * FriendlyControl) * TickRate);
        }
        if (
            raidAt < 0
            || Tick < raidAt
            || !sources.TryGetValue(23, out var factory)
            || factory.Destroyed
        )
            return;
        var site = territory
            .Values.Where(s => s.Friendly)
            .OrderBy(s => Vector2.DistanceSquared(s.Position, factory.Location.Position))
            .ThenBy(s => s.Id)
            .FirstOrDefault();
        if (site is null)
            return;
        var group = enemies
            .Where(e =>
                e.Home == 23
                && !returningRaiders.Contains(e.Id)
                && !e.Allied
                && !e.Installation
                && e.SurfacePlanet == 0
                && e.Type != "tender"
                && e.Hull > 0
                && !(rescue is { State: "active" } r && r.Attackers.Contains(e.Id))
            )
            .OrderBy(e => e.Id)
            .Take(4 + (int)MathF.Round(8 * FriendlyControl))
            .ToArray();
        if (group.Length < 4 + (int)MathF.Round(8 * FriendlyControl))
            return;
        raidSite = site.Id;
        raiders = group.Select(e => e.Id).ToHashSet();
        foreach (var p in players.Values)
        {
            p.LootNotice = $"RAID WARNING: {Location(site.Id)!.Name} is under threat";
            p.LootNoticeUntil = Tick + 300;
        }
    }

    private bool ReturnFromRaid(Enemy npc)
    {
        if (!returningRaiders.Contains(npc.Id))
            return false;
        var home = Exterior(Location(23)!.Position, 240);
        if (Vector2.Distance(npc.Position, home) < 280)
        {
            returningRaiders.Remove(npc.Id);
            return false;
        }
        npc.State = "return";
        npc.Target = npc.NpcTarget = 0;
        var delta = Navigate(npc, home) - npc.Position;
        if (delta.LengthSquared() > 1)
        {
            npc.Aim = MathF.Atan2(delta.Y, delta.X);
            npc.Position = MoveShip(
                npc.Position,
                npc.Position
                    + Vector2.Normalize(delta)
                        * EnemyCatalog.Get(npc.Type).Speed
                        / TickRate
                        * (Tick < npc.SlowUntil ? npc.SlowFactor : 1),
                EnemyRadius(npc)
            );
        }
        return true;
    }

    private Enemy? RaidTarget(Enemy npc) =>
        raidSite != 0 && raiders.Contains(npc.Id)
            ? enemies
                .Where(e =>
                    e.Hull > 0 && e.Allied && e.Home == raidSite && e.Type.StartsWith("salvage-")
                )
                .MinBy(e => Vector2.DistanceSquared(e.Position, npc.Position))
            : null;

    private RaidSnapshot? RaidState() =>
        raidSite == 0
            ? null
            : new(raidSite, "incoming", enemies.Count(e => e.Hull > 0 && raiders.Contains(e.Id)));

    private OutpostSnapshot[] OutpostsState() =>
        salvageSites
            .Values.Select(s => new OutpostSnapshot(
                s.Location.Id,
                s.Location.Name,
                s.Location.X,
                s.Location.Y,
                s.Active,
                s.Positions.Select(
                        (p, i) =>
                            new StructureSnapshot(
                                i,
                                p.X,
                                p.Y,
                                Math.Max(0, s.Units[i]?.Hull ?? 0),
                                s.Units[i]?.MaxHull
                                    ?? (int)
                                        MathF.Ceiling(
                                            (i == 0 ? 120 : 60) * EnemyCatalog.Health(s.Level)
                                        ),
                                i == 3 ? 150 : 100,
                                s.Reserved[i]
                            )
                    )
                    .ToArray()
            ))
            .ToArray();
}
