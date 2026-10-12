using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed record RescueSnapshot(
    int Id,
    int Ship,
    string State,
    float X,
    float Y,
    int Hull,
    int MaxHull,
    int Shield,
    int MaxShield,
    int Remaining,
    int Level
);

public sealed partial class Room
{
    private sealed class Rescue
    {
        public required Enemy Ship;
        public required HashSet<int> Attackers;
        public readonly Dictionary<int, (string Run, float Damage)> Contributions = [];
        public int Id;
        public string State = "active";
        public long Resolved;
    }

    private Rescue? rescue;
    private int nextRescue;
    private long nextReliefAt = 240 * TickRate;

    private RescueSnapshot? RescueState() =>
        rescue is { } r
            ? new(
                r.Id,
                r.Ship.Id,
                r.State,
                r.Ship.Position.X,
                r.Ship.Position.Y,
                r.Ship.Hull,
                r.Ship.MaxHull,
                r.Ship.Shield,
                r.Ship.MaxShield,
                r.Attackers.Count(id => enemies.Any(e => e.Id == id && e.Hull > 0)),
                r.Ship.Level
            )
            : null;

    private void RecordRescueContribution(Enemy enemy, Player player, float amount)
    {
        if (rescue is not { State: "active" } r || !r.Attackers.Contains(enemy.Id))
            return;
        var old = r.Contributions.GetValueOrDefault(player.Id);
        r.Contributions[player.Id] = (
            player.RunId,
            (old.Run == player.RunId ? old.Damage : 0) + amount
        );
    }

    private void StepRescue()
    {
        if (Sector is null || !enemiesEnabled)
            return;
        if (rescue is { State: "active" } current)
        {
            // Evaluate after all damage for this tick: ship death takes precedence.
            var failed = current.Ship.Hull <= 0;
            var won = current.Attackers.All(id => !enemies.Any(e => e.Id == id && e.Hull > 0));
            if (!failed && !won)
                return;
            current.State = failed ? "failed" : "rescued";
            current.Resolved = Tick;
            nextReliefAt = Tick + new Random(Sector.Seed ^ current.Id).Next(240, 361) * TickRate;
            foreach (var p in players.Values)
            {
                p.LootNotice = failed
                    ? "Emergency mission failed: relief ship destroyed"
                    : "Relief ship saved";
                p.LootNoticeUntil = Tick + 240;
            }
            if (won && !failed)
                current.Ship.Patrol = 1;
            if (won && !failed)
                foreach (var (id, contribution) in current.Contributions)
                {
                    var p =
                        players.GetValueOrDefault(id)
                        ?? suspended.Values.FirstOrDefault(p => p.Id == id);
                    if (
                        p is null
                        || p.Hull <= 0
                        || p.RunId != contribution.Run
                        || contribution.Damage < 1
                    )
                        continue;
                    var xp = Eject(current.Ship, id);
                    var money = Eject(current.Ship, id + 1);
                    pendingEnergy.Add(
                        new(
                            ++nextEnergy,
                            xp.Position,
                            60 * current.Ship.Level,
                            Tick + EnergyLifetimeTicks,
                            p.Id
                        )
                        {
                            Velocity = xp.Velocity,
                        }
                    );
                    pendingCredits.Add(
                        new(
                            ++nextCredit,
                            money.Position,
                            80 * current.Ship.Level,
                            Tick + EnergyLifetimeTicks,
                            p.Id
                        )
                        {
                            Velocity = money.Velocity,
                        }
                    );
                    p.LootNotice = "Rescue complete: personal rewards beside the relief ship";
                    p.LootNoticeUntil = Tick + 240;
                }
        }
        if (rescue is { State: not "active" } ended && Tick - ended.Resolved > 240)
            rescue = null;
        if (enemies.Any(e => e.Type == "relief" && e.Hull <= 0) && rescue is null)
            nextReliefAt = Tick + new Random(Sector.Seed ^ nextRescue).Next(240, 361) * TickRate;
        if (Tick < nextReliefAt)
            return;
        var ship = enemies.FirstOrDefault(e => e.Type == "relief" && e.Hull > 0);
        if (ship is null && Tick >= nextReliefAt && players.Count > 0 && enemies.Count < EnemyLimit)
        {
            var home = nextRescue % 2 == 0 ? 22 : 23;
            var level = EnemyCatalog.Region(home);
            var scale = EnemyCatalog.Health(level);
            var at = Location(nextRescue % 2 == 0 ? 11 : 12)!.Position + new Vector2(340, 0);
            at = Exterior(at, 150);
            // A physical arrival from the station-side shipping lane, not a player-relative spawn.
            ship = new()
            {
                Id = ++nextEnemy,
                Type = "relief",
                Allied = true,
                Gunner = true,
                Home = home,
                Level = level,
                Hull = (int)MathF.Ceiling(240 * scale),
                MaxHull = (int)MathF.Ceiling(240 * scale),
                Shield = (int)MathF.Ceiling(120 * scale),
                MaxShield = (int)MathF.Ceiling(120 * scale),
                Position = at,
                Previous = at,
            };
            enemies.Add(ship);
        }
        if (ship is null || rescue is not null)
            return;
        var attackers = enemies
            .Where(e =>
                !e.Allied
                && !e.Installation
                && e.Type != "tender"
                && e.SurfacePlanet == 0
                && e.Hull > 0
                && e.NpcTarget == ship.Id
            )
            .OrderBy(e => e.Id)
            .Take(8)
            .Select(e => e.Id)
            .ToHashSet();
        if (attackers.Count < 6 || ship.LastHit == 0)
            return;
        rescue = new()
        {
            Id = ++nextRescue,
            Ship = ship,
            Attackers = attackers,
        };
        foreach (var attacker in enemies.Where(e => attackers.Contains(e.Id)))
        foreach (var (id, contribution) in attacker.Contributors)
        {
            var old = rescue.Contributions.GetValueOrDefault(id);
            rescue.Contributions[id] = (
                contribution.Run,
                (old.Run == contribution.Run ? old.Damage : 0) + contribution.Damage
            );
        }
        foreach (var p in players.Values)
        {
            p.LootNotice =
                "Emergency broadcast: relief ship under attack - select the radio beacon on your map";
            p.LootNoticeUntil = Tick + 240;
        }
    }
}
