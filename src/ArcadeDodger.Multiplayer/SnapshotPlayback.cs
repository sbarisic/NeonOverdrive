namespace ArcadeDodger.Multiplayer;

/// <summary>Presentation-only delayed playback. Never extrapolates or alters authoritative state.</summary>
public sealed class SnapshotPlayback
{
    private sealed class Frame(RoomSnapshot value)
    {
        public RoomSnapshot Value = value;
        public Dictionary<int, PlayerSnapshot> Players = value.Players.ToDictionary(p => p.Id);
        public Dictionary<int, ShotSnapshot> Shots = value.Shots.ToDictionary(p => p.Id);
        public Dictionary<int, EnemySnapshot> Enemies = (value.Enemies ?? []).ToDictionary(p =>
            p.Id
        );
        public Dictionary<int, ShotSnapshot> Descendants = IndexDescendants(value.Shots);

        private static Dictionary<int, ShotSnapshot> IndexDescendants(ShotSnapshot[] shots)
        {
            var result = new Dictionary<int, ShotSnapshot>();
            foreach (var shot in shots)
            {
                if (shot.Parent > 0)
                    result.TryAdd(shot.Parent, shot);
                foreach (var ancestor in shot.Ancestors ?? [])
                    result.TryAdd(ancestor, shot);
            }
            return result;
        }
    }

    private readonly List<Frame> frames = new();
    private readonly Dictionary<int, CombatEvent> endpoints = new();
    private readonly HashSet<int> sampled = new();
    private bool ancestryDirty;
    private double receivedAt,
        playhead;
    public RoomSnapshot? Latest => frames.Count == 0 ? null : frames[^1].Value;

    public void Receive(RoomSnapshot frame, double seconds)
    {
        if (Latest is { } last && frame.Tick <= last.Tick)
            return;
        if (frames.Count == 0)
            playhead = frame.Tick;
        frames.Add(new Frame(frame));
        ancestryDirty = true;
        receivedAt = seconds;
        if (frames.Count > 32)
            frames.RemoveAt(0);
    }

    public RoomSnapshot? Sample(double seconds)
    {
        if (Latest is not { } latest)
            return null;
        // 100 ms jitter buffer at 30 Hz. Freeze on missing packets, never rewind.
        playhead = Math.Min(
            latest.Tick,
            Math.Max(playhead, latest.Tick + Math.Max(0, seconds - receivedAt) * Room.TickRate - 3)
        );
        while (frames.Count > 2 && frames[1].Value.Tick <= playhead)
        {
            frames.RemoveAt(0);
            ancestryDirty = true;
        }
        var ai = frames[0];
        var bi = frames.Count > 1 ? frames[1] : ai;
        var a = ai.Value;
        var b = bi.Value;
        if (ancestryDirty)
        {
            endpoints.Clear();
            foreach (var frame in frames)
            foreach (var e in frame.Value.CombatEvents ?? [])
                if (e.Shot > 0 && e.Kind is 0 or 1)
                    endpoints[e.Shot] = e;
            ancestryDirty = false;
        }
        sampled.Clear();
        var t =
            b.Tick == a.Tick
                ? 1f
                : (float)Math.Clamp((playhead - a.Tick) / (b.Tick - a.Tick), 0, 1);
        var players = latest
            .Players.Select(current =>
            {
                var from = ai.Players.GetValueOrDefault(current.Id);
                var to = bi.Players.GetValueOrDefault(current.Id);
                if (
                    from is null
                    || to is null
                    || from.Hull <= 0
                    || to.Hull <= 0
                    || from.X == to.X
                        && from.Y == to.Y
                        && from.Aim == to.Aim
                        && from.Heading == to.Heading
                )
                    return current;
                var angle = MathF.IEEERemainder(to.Aim - from.Aim, MathF.Tau);
                return current with
                {
                    X = Lerp(from.X, to.X, t),
                    Y = Lerp(from.Y, to.Y, t),
                    Aim = from.Aim + angle * t,
                    Heading =
                        from.Heading
                        + MathF.IEEERemainder(to.Heading - from.Heading, MathF.Tau) * t,
                };
            })
            .ToArray();
        // Use the playback interval's entity membership as well as its positions.
        // Dropping a parent from the newest snapshot while birth-gating its children left an empty frame.
        var shots = a
            .Shots.Concat(b.Shots)
            .Where(s => sampled.Add(s.Id))
            .Select(seed =>
            {
                var from = ai.Shots.GetValueOrDefault(seed.Id);
                var to = bi.Shots.GetValueOrDefault(seed.Id);
                var current = to ?? from!;
                if (current.Born > playhead)
                    return null;
                if (to is null)
                {
                    var child = bi.Descendants.GetValueOrDefault(seed.Id);
                    var endpoint = endpoints.GetValueOrDefault(seed.Id);
                    var endTick = endpoint?.Born ?? child?.Born ?? b.Tick;
                    if (playhead >= endTick)
                        return null;
                    if (endpoint is not null || child is not null)
                    {
                        var progress = (float)
                            Math.Clamp((playhead - a.Tick) / Math.Max(1, endTick - a.Tick), 0, 1);
                        return current with
                        {
                            X = Lerp(current.X, endpoint?.X ?? child!.BirthX, progress),
                            Y = Lerp(current.Y, endpoint?.Y ?? child!.BirthY, progress),
                        };
                    }
                    return current;
                }
                if (from is not null)
                    return current with { X = Lerp(from.X, to.X, t), Y = Lerp(from.Y, to.Y, t) };
                if (current.Born > 0)
                {
                    var progress = (float)
                        Math.Clamp(
                            (playhead - current.Born) / Math.Max(1, b.Tick - current.Born),
                            0,
                            1
                        );
                    // New split children start at the fork, never the owner's ship.
                    if (current.Parent > 0)
                        return current with
                        {
                            X = Lerp(current.BirthX, current.X, progress),
                            Y = Lerp(current.BirthY, current.Y, progress),
                        };
                    if (!current.Modular)
                        return current with
                        {
                            X = current.X - current.Dx * (float)(b.Tick - playhead),
                            Y = current.Y - current.Dy * (float)(b.Tick - playhead),
                        };
                }
                return current;
            })
            .OfType<ShotSnapshot>()
            .ToArray();
        var enemies = ((t < 1 ? a : b).Enemies ?? [])
            .Select(current =>
            {
                var from = ai.Enemies.GetValueOrDefault(current.Id);
                var to = bi.Enemies.GetValueOrDefault(current.Id);
                return
                    from is null
                    || to is null
                    || from.X == to.X && from.Y == to.Y && from.Aim == to.Aim
                    ? current
                    : current with
                    {
                        X = Lerp(from.X, to.X, t),
                        Y = Lerp(from.Y, to.Y, t),
                        Aim = from.Aim + MathF.IEEERemainder(to.Aim - from.Aim, MathF.Tau) * t,
                    };
            })
            .ToArray();
        return latest with
        {
            Players = players,
            Shots = shots,
            Enemies = enemies,
            CombatEvents = (latest.CombatEvents ?? []).Where(e => e.Born <= playhead).ToArray(),
        };
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
