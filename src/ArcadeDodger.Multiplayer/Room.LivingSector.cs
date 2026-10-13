using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private sealed class Civilian
    {
        public required Enemy Ship;
        public required string Name;
        public int Destination = 11;
        public long DepartAt;
        public int Identity;
        public int Cargo;
        public long WorkUntil;
        public long CargoRevision;
        public readonly List<TradeStock> Stock = [];
        public readonly HashSet<int> Reserved = [];
    }

    private sealed class WorldCargo
    {
        public int Id,
            Credits,
            Owner;
        public required string Kind;
        public required Vector2 Position;
        public long Expires;
        public bool Pending;
        public WeaponItem? Reward;
    }

    private sealed class LivingPilot
    {
        public required string Run;
        public int Conversation,
            Tow;
        public string Job = "",
            JobState = "",
            Police = "";
        public int[] Targets = [];
        public readonly HashSet<int> Progress = [];
        public long ScanAt,
            LastPoliceSeen,
            JobCooldown;
        public int IllegalCargo,
            JobRegion = 1;
        public bool RewardPending;
        public WeaponItem? Reward;
    }

    private readonly List<Civilian> civilians = [];
    private readonly List<WorldCargo> worldCargo = [];
    private readonly List<RadioMessage> radio = [];
    private readonly Dictionary<int, LivingPilot> livingPilots = [];
    private readonly HashSet<string> resolvedDerelicts = [];
    private int nextWorldId = 1000000,
        nextRadio;
    private Random? worldRandom;

    private LivingPilot Living(Player p)
    {
        if (!livingPilots.TryGetValue(p.Id, out var state) || state.Run != p.RunId)
            livingPilots[p.Id] = state = new() { Run = p.RunId };
        return state;
    }

    private void Broadcast(string channel, string speaker, string text)
    {
        if (
            channel is "conversation" or "trade"
            && radio.Any(m => m.Speaker == speaker && m.Expires > Tick + 12 * TickRate)
        )
            return;
        if (radio.Count > 0 && radio[^1].Text == text && radio[^1].Expires > Tick + 8 * TickRate)
            return;
        radio.Add(new(++nextRadio, channel, speaker, text, Tick + 15 * TickRate));
        if (radio.Count > 32)
            radio.RemoveAt(0);
    }

    private void InitializeLivingSector()
    {
        if (Sector is null)
            return;
        worldRandom = new Random(Sector.Seed ^ 0x154c7);
        InitializeTerritory();
        foreach (var field in Sector.Locations.Where(l => l.Kind == "planet").Take(3))
            AddCargo(
                "derelict",
                Exterior(field.Position + new Vector2(field.Radius + 600, 400)),
                80 * field.Id,
                0
            );
    }

    private void StepLivingSector()
    {
        if (Sector is null)
            return;
        if (enemiesEnabled && Tick % (60 * TickRate) == 1)
        {
            civilians.RemoveAll(c => c.Ship.Hull <= 0 && c.Reserved.Count == 0);
            var roles = new[]
            {
                NpcRole.Trader,
                NpcRole.Trader,
                NpcRole.Trader,
                NpcRole.Miner,
                NpcRole.Scavenger,
                NpcRole.Patrol,
                NpcRole.Police,
                NpcRole.Mercenary,
            };
            for (var i = 0; i < roles.Length && enemies.Count < EnemyLimit; i++)
                if (!civilians.Any(c => c.Identity == i))
                    SpawnCivilian(roles[i], i);
        }
        radio.RemoveAll(m => m.Expires <= Tick);
        worldCargo.RemoveAll(c => c.Expires <= Tick && !c.Pending);
        foreach (var cargo in worldCargo.Where(c => c.Owner > 0 && !c.Pending))
            if (
                !players.TryGetValue(cargo.Owner, out var owner)
                || !owner.Connected
                || Living(owner).Tow != cargo.Id
            )
                cargo.Owner = 0;
        foreach (var cargo in worldCargo.Where(c => c.Owner < 0 && !c.Pending))
            if (!enemies.Any(e => e.Id == -cargo.Owner && e.Hull > 0))
                cargo.Owner = 0;
        foreach (var p in players.Values)
        {
            var state = Living(p);
            if (
                state.Conversation != 0
                && (
                    !p.Connected
                    || !enemies.Any(e =>
                        e.Id == state.Conversation
                        && e.Hull > 0
                        && Vector2.Distance(e.Position, p.Position) <= Detection(p)
                    )
                )
            )
                state.Conversation = 0;
            var cargo = worldCargo.FirstOrDefault(c => c.Id == state.Tow);
            if (cargo is not null && !cargo.Pending)
            {
                if (
                    !p.Connected
                    || p.Hull <= 0
                    || Tick == p.LastDamage
                    || p.Menu
                    || p.Editing
                    || Vector2.Distance(p.Position, cargo.Position) > 350
                    || !ServiceLine(p.Position, cargo.Position)
                )
                    ReleaseTow(p);
                else
                {
                    var delta = p.Position - cargo.Position;
                    if (delta.Length() > 70)
                        cargo.Position = MoveShip(
                            cargo.Position,
                            cargo.Position
                                + Vector2.Normalize(delta)
                                    * MathF.Min(delta.Length() - 70, 240f / TickRate),
                            28
                        );
                    if (
                        Sector.Locations.Any(l =>
                            l.Kind == "station"
                            && FriendlyStation(l.Id)
                            && Vector2.Distance(l.Position, cargo.Position) < 220
                        )
                    )
                        DeliverCargo(p, cargo);
                }
            }
        }
        StepTerritory();
        StepWorldJobs();
        StepPolice();
        StepRaces();
        StepWorldEvents();
    }
}
