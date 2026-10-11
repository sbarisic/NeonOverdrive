namespace ArcadeDodger.Multiplayer;

/// <summary>Presentation-only delayed playback. Never extrapolates or alters authoritative state.</summary>
public sealed class SnapshotPlayback
{
    private readonly List<RoomSnapshot> frames = new();
    private double receivedAt, playhead;
    public RoomSnapshot? Latest => frames.Count == 0 ? null : frames[^1];

    public void Receive(RoomSnapshot frame, double seconds)
    {
        if (Latest is { } last && frame.Tick <= last.Tick) return;
        if (frames.Count == 0) playhead = frame.Tick;
        frames.Add(frame);
        receivedAt = seconds;
        if (frames.Count > 32) frames.RemoveAt(0);
    }

    public RoomSnapshot? Sample(double seconds)
    {
        if (Latest is not { } latest) return null;
        // 100 ms jitter buffer at 30 Hz. Freeze on missing packets, never rewind.
        playhead = Math.Min(latest.Tick, Math.Max(playhead,
            latest.Tick + Math.Max(0, seconds - receivedAt) * Room.TickRate - 3));
        while (frames.Count > 2 && frames[1].Tick <= playhead) frames.RemoveAt(0);
        var a = frames[0];
        var b = frames.Count > 1 ? frames[1] : a;
        var t = b.Tick == a.Tick ? 1f : (float)Math.Clamp((playhead - a.Tick) / (b.Tick - a.Tick), 0, 1);
        var players = latest.Players.Select(current =>
        {
            var from = a.Players.FirstOrDefault(p => p.Id == current.Id);
            var to = b.Players.FirstOrDefault(p => p.Id == current.Id);
            if (from is null || to is null || from.Hull <= 0 || to.Hull <= 0) return current;
            var angle = MathF.IEEERemainder(to.Aim - from.Aim, MathF.Tau);
            return current with { X = Lerp(from.X, to.X, t), Y = Lerp(from.Y, to.Y, t), Aim = from.Aim + angle * t };
        }).ToArray();
        // Use the playback interval's entity membership as well as its positions.
        // Dropping a parent from the newest snapshot while birth-gating its children left an empty frame.
        var shots = a.Shots.Concat(b.Shots).DistinctBy(s => s.Id).Select(seed =>
        {
            if (!latest.Shots.Any(s => s.Id == seed.Id) && !frames.Any(f => f.Shots.Any(s => (s.Parent == seed.Id || s.Ancestors?.Contains(seed.Id) == true)))) return null;
            var from = a.Shots.FirstOrDefault(s => s.Id == seed.Id);
            var to = b.Shots.FirstOrDefault(s => s.Id == seed.Id);
            var current = to ?? from!;
            if (current.Born > playhead) return null;
            if (to is null)
            {
                var child = b.Shots.FirstOrDefault(s => (s.Parent == seed.Id || s.Ancestors?.Contains(seed.Id) == true));
                var endTick = child?.Born ?? b.Tick;
                if (playhead >= endTick) return null;
                if (child is not null)
                {
                    var progress = (float)Math.Clamp((playhead - a.Tick) / Math.Max(1, endTick - a.Tick), 0, 1);
                    return current with { X = Lerp(current.X, child.BirthX, progress), Y = Lerp(current.Y, child.BirthY, progress) };
                }
                return current;
            }
            if (from is not null) return current with { X = Lerp(from.X, to.X, t), Y = Lerp(from.Y, to.Y, t) };
            if (current.Born > 0)
            {
                var progress = (float)Math.Clamp((playhead - current.Born) / Math.Max(1, b.Tick - current.Born), 0, 1);
                // New split children start at the fork, never the owner's ship.
                if (current.Parent > 0) return current with { X = Lerp(current.BirthX, current.X, progress), Y = Lerp(current.BirthY, current.Y, progress) };
                if (!current.Modular) return current with { X = current.X - current.Dx * (float)(b.Tick - playhead), Y = current.Y - current.Dy * (float)(b.Tick - playhead) };
            }
            return current;
        }).OfType<ShotSnapshot>().ToArray();
        var enemies = (latest.Enemies ?? []).Select(current =>
        {
            var from = a.Enemies?.FirstOrDefault(e => e.Id == current.Id);
            var to = b.Enemies?.FirstOrDefault(e => e.Id == current.Id);
            return from is null || to is null ? current : current with
            {
                X = Lerp(from.X, to.X, t), Y = Lerp(from.Y, to.Y, t),
                Aim = from.Aim + MathF.IEEERemainder(to.Aim - from.Aim, MathF.Tau) * t
            };
        }).ToArray();
        return latest with { Players = players, Shots = shots, Enemies = enemies };
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
