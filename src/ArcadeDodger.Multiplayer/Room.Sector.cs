using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    public SectorDefinition? Sector { get; }
    private readonly SectorCollision? geometry;
    private readonly Dictionary<int, Source> sources = [];
    private readonly Dictionary<int, System.Numerics.Vector2[]> patrolRoutes = [];

    private Vector2[] PatrolRoute(int homeId)
    {
        if (patrolRoutes.TryGetValue(homeId, out var cached))
            return cached;
        var home = Location(homeId)?.Position ?? Vector2.Zero;
        var route =
            Location(homeId)?.Kind == "corridor"
                ? Location(homeId == 30 ? 22 : 23)!.Position
                : home;
        return patrolRoutes[homeId] = new[]
        {
            route + new Vector2(300, -200),
            route + new Vector2(550, 200),
            route + new Vector2(-250, 500),
            home + new Vector2(-150, -250),
        }
            .Select(p => Vector2.Clamp(p, new(70), new(Width - 70, Height - 70)))
            .ToArray();
    }

    private readonly Dictionary<string, Exploration> explorers = [];
    private readonly List<MapPing> pings = [];
    private int nextPing;

    private sealed class Source
    {
        public required SectorLocation Location;
        public int LaunchCount;
        public long NextLaunch;
        public long Warning;
        public bool Destroyed;
    }

    private sealed class Exploration
    {
        public int Hailing;
        public string Job = "";
        public long JobCooldown;
        public int[] JobTargets = [];
        public readonly HashSet<int> JobProgress = [],
            Caches = [];
        public int LastStation = 10,
            Contract,
            InteractionTarget,
            SessionPlayer;
        public long InteractionStart,
            LastPing = -150;
        public readonly Dictionary<int, long> ContractCooldown = [];
        public bool Carrying,
            Ready;
        public readonly HashSet<int> Discovered = [10],
            Cells = [],
            Progress = [],
            Completed = [];
        public SectorReceipt? Receipt;
    }

    public ContractDefinition[] Contracts =>
        Sector is null
            ? []
            : Enumerable
                .Range(0, 3)
                .SelectMany(i =>
                    new[]
                    {
                        new ContractDefinition(i * 3 + 1, 10 + i, "survey", 50, 40, 30),
                        new(i * 3 + 2, 10 + i, "recover", 40 + i * 2, 60, 45),
                        new(i * 3 + 3, 10 + i, "suppress", 20 + i, 100, 75),
                        new(100 + i, 10 + i, "interdict", i == 0 ? 30 : 31, 60, 45),
                    }
                )
                .ToArray();

    private Exploration ExplorationFor(Player p)
    {
        var key = FriendBook.Key(p.Name);
        if (!explorers.TryGetValue(key, out var state))
            explorers[key] = state = new();
        return state;
    }

    private void PilotDied(Player p)
    {
        if (!p.ResetPending && !p.ResetComplete)
            CombatFx(0, 2, p.Position, 20);
        ResetAfterDeath(p);
        if (Sector is null)
            return;
        var s = ExplorationFor(p);
        if (s.SessionPlayer != p.Id)
            return;
        s.Carrying = false;
        s.InteractionTarget = 0;
        if (Contracts.FirstOrDefault(c => c.Id == s.Contract)?.Kind == "recover")
            s.Ready = false;
    }

    private SectorLocation? Location(int id) => Sector?.Locations.FirstOrDefault(l => l.Id == id);

    private bool Sanctuary(Vector2 p) =>
        Sector?.Locations.Any(l =>
            l.Kind == "station" && Vector2.DistanceSquared(p, l.Position) <= 300 * 300
        ) == true;

    private bool Safe(Player p) => p.Docked != 0 || Sanctuary(p.Position);

    private bool VisibleLine(Vector2 from, Vector2 to) =>
        TraceCover(from, to) is null
        && !(
            Sector?.Locations.Any(l =>
                l.Kind == "station"
                && SectorCollision.Circle(from - l.Position, to - l.Position, 300).HasValue
            )
            ?? false
        );

    private (float T, Vector2 Normal)? TraceCover(Vector2 from, Vector2 to, float radius = 0)
    {
        var hit = geometry?.Trace(from, to, radius);
        foreach (var source in sources.Values)
        {
            if (!source.Destroyed)
                continue;
            var location = source.Location;
            if (
                SectorCollision.Circle(
                    from - location.Position,
                    to - location.Position,
                    location.Radius + radius
                )
                    is not { } t
                || hit is not null && t >= hit.Value.T
            )
                continue;
            var offset = Vector2.Lerp(from, to, t) - location.Position;
            hit = (t, offset.LengthSquared() > .001f ? Vector2.Normalize(offset) : Vector2.UnitX);
        }
        return hit;
    }

    private Vector2 MoveShip(Vector2 from, Vector2 to, float radius) =>
        geometry?.Move(from, to, radius)
        ?? Vector2.Clamp(to, new(radius), new(Width - radius, Height - radius));

    private void StepExploration()
    {
        if (Sector is null)
            return;
        pings.RemoveAll(p => p.Expires <= Tick);
        foreach (var p in players.Values.Where(p => p.Connected))
        {
            var s = ExplorationFor(p);
            if (p.Hull <= 0)
            {
                s.Carrying = false;
                if (Contracts.FirstOrDefault(c => c.Id == s.Contract)?.Kind == "recover")
                    s.Ready = false;
                s.InteractionTarget = 0;
                continue;
            }
            foreach (var l in Sector.Locations)
                if (Vector2.DistanceSquared(p.Position, l.Position) <= Survey(p) * Survey(p))
                    s.Discovered.Add(l.Id);
            var cx = (int)p.Position.X / 200;
            var cy = (int)p.Position.Y / 200;
            var cellRadius = (int)MathF.Ceiling(Survey(p) / 200);
            for (
                var x = Math.Max(0, cx - cellRadius);
                x <= Math.Min((int)(Sector.Width / 200) - 1, cx + cellRadius);
                x++
            )
            for (
                var y = Math.Max(0, cy - cellRadius);
                y <= Math.Min((int)(Sector.Height / 200) - 1, cy + cellRadius);
                y++
            )
                if (
                    Vector2.DistanceSquared(p.Position, new(x * 200 + 100, y * 200 + 100))
                    <= Survey(p) * Survey(p)
                )
                    s.Cells.Add(y * (int)(Sector.Width / 200) + x);
            if (s.InteractionTarget != 0)
            {
                var target = Location(s.InteractionTarget)!;
                if (
                    p.Editing
                    || p.Menu
                    || p.Docked != 0
                    || p.Position != p.Previous
                    || p.Held is { X: not 0 } or { Y: not 0 }
                    || p.Held?.Fire == true
                    || Vector2.Distance(p.Position, target.Position) > 100
                    || p.LastDamage >= s.InteractionStart
                )
                    s.InteractionTarget = 0;
                else if (Tick - s.InteractionStart >= 90)
                {
                    if (target.Kind == "cache")
                        SearchCache(p, s, target);
                    else if (target.Kind == "beacon")
                    {
                        s.Progress.Add(target.Id);
                        s.Ready = s.Progress.Count >= 3;
                    }
                    else
                    {
                        s.Carrying = true;
                        s.Ready = true;
                    }
                    s.InteractionTarget = 0;
                }
            }
            if (Tick % 30 == 0 && !Safe(p) && !p.Editing)
                foreach (var thermal in Sector.Locations.Where(l => l.Kind == "thermal"))
                    if (
                        Tick % 300 >= 60
                        && Tick % 300 < 180
                        && Vector2.Distance(p.Position, thermal.Position) < thermal.Radius
                    )
                        Damage(p);
        }
    }
}
