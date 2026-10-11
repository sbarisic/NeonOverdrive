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
        var shots = latest.Shots.Where(s => s.Born <= playhead).Select(current =>
        {
            // Reconstruct only past positions, never extrapolate beyond the newest tick.
            // Birth gating prevents a new shot freezing ahead of the delayed clock.
            if (current.Born > 0) return current with
            {
                X = current.X - current.Dx * (float)(latest.Tick - playhead),
                Y = current.Y - current.Dy * (float)(latest.Tick - playhead)
            };
            var from = a.Shots.FirstOrDefault(p => p.Id == current.Id);
            var to = b.Shots.FirstOrDefault(p => p.Id == current.Id);
            return from is null || to is null ? current : current with { X = Lerp(from.X, to.X, t), Y = Lerp(from.Y, to.Y, t) };
        }).ToArray();
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
