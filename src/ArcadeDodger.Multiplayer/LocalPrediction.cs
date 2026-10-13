using System.Numerics;

namespace ArcadeDodger.Multiplayer;

/// <summary>
/// Presentation-only movement prediction. Ack identifies the latest held intent, NOT a
/// number of simulated steps. Replay integrates timestamped intent intervals, never packets.
/// No hits, shots, rewards or lifecycle transitions are predicted.
/// </summary>
public sealed class LocalPrediction
{
    public const double MaxPredictionSeconds = .5;
    public const double InputTimeoutSeconds = 8d / Room.TickRate;
    public const int MaxHistory = 128;
    public const float SnapDistance = 120;
    private const double HistorySeconds = 1;
    private readonly List<Intent> history = new();
    private readonly List<Sent> sent = new();
    private PlayerSnapshot? authoritative;
    private SectorCollision? geometry;

    public void SetSector(SectorDefinition sector) => geometry = new(sector);

    private long tick = -1;
    private double anchorTime,
        lastTime,
        correctionAt;
    private Vector2 correction;
    public int HistoryCount => history.Count;
    public int SentCount => sent.Count;

    private readonly record struct Intent(
        double At,
        double RefreshedAt,
        Vector2 Direction,
        float Aim,
        bool Menu
    );

    private readonly record struct Sent(int Sequence, double At);

    private static bool Active(PlayerSnapshot p) =>
        p.Connected && p.Hull > 0 && !p.Editing && p.Docked == 0;

    private static Vector2 Clamp(Vector2 p) =>
        Vector2.Clamp(
            p,
            new(Room.Radius),
            new(Room.Width - Room.Radius, Room.Height - Room.Radius)
        );

    public void Reset()
    {
        authoritative = null;
        tick = -1;
        anchorTime = lastTime = correctionAt = 0;
        correction = Vector2.Zero;
        history.Clear();
        sent.Clear();
    }

    // Called promptly on local changes, and refreshed independently of packet frequency.
    public void Input(float x, float y, float aim, bool menu, double seconds)
    {
        if (
            !double.IsFinite(seconds)
            || seconds < lastTime
            || !float.IsFinite(x)
            || !float.IsFinite(y)
            || !float.IsFinite(aim)
            || authoritative is not { } p
            || !Active(p)
        )
            return;
        lastTime = seconds;
        var direction = new Vector2(Math.Clamp(x, -1, 1), Math.Clamp(y, -1, 1));
        aim = MathF.IEEERemainder(aim, MathF.Tau);
        // RAF, pointer movement and transport can all refresh the same held keys.
        // Preserve the start of that interval instead of evicting replay history at high event rates.
        if (
            history.Count > 0
            && history[^1] is var prior
            && prior.Direction == direction
            && prior.Menu == menu
            && seconds < prior.RefreshedAt + InputTimeoutSeconds
        )
            history[^1] = prior with { RefreshedAt = seconds, Aim = aim };
        else
            history.Add(new(seconds, seconds, direction, aim, menu));
        Trim(seconds);
    }

    // The time an intent was actually sent, not the time a render frame happened.
    public void SentInput(int sequence, double seconds)
    {
        if (
            sequence <= 0
            || !double.IsFinite(seconds)
            || seconds < lastTime
            || (sent.Count > 0 && sequence <= sent[^1].Sequence)
        )
            return;
        lastTime = seconds;
        sent.Add(new(sequence, seconds));
        Trim(seconds);
    }

    public void Receive(PlayerSnapshot p, long serverTick, double seconds)
    {
        if (
            !double.IsFinite(seconds)
            || seconds < lastTime
            || serverTick < 0
            || serverTick <= tick
            || !float.IsFinite(p.X)
            || !float.IsFinite(p.Y)
            || !float.IsFinite(p.Aim)
            || !float.IsFinite(p.VelocityX)
            || !float.IsFinite(p.VelocityY)
            || !float.IsFinite(p.Heading)
            || !float.IsFinite(p.Throttle)
            || p.Throttle is < 0 or > 1
            || !float.IsFinite(p.MoveSpeed)
            || p.MoveSpeed <= 0
            || !float.IsFinite(p.Acceleration)
            || p.Acceleration <= 0
            || !float.IsFinite(p.Steering)
            || p.Steering <= 0
        )
            return;
        var old = Sample(seconds);
        var lifecycle =
            authoritative is not { } previous
            || previous.Id != p.Id
            || previous.Life != p.Life
            || Active(previous) != Active(p);
        var teleport =
            authoritative is { } prior
            && Vector2.Distance(new(prior.X, prior.Y), new(p.X, p.Y)) > SnapDistance * 2;
        // An ack timestamp is an approximate clock anchor (no synchronized server clock).
        // One ack can cover many ticks. Advance that anchor by tick deltas, even if ack
        // hasn't changed. New ack packets cannot manufacture an extra movement step.
        var mapped = tick < 0 ? seconds : anchorTime + (serverTick - tick) / (double)Room.TickRate;
        var acknowledged = sent.FindLastIndex(s => s.Sequence == p.Ack);
        if (acknowledged >= 0 && authoritative?.Ack != p.Ack)
            mapped = sent[acknowledged].At + 1d / Room.TickRate;
        anchorTime = Math.Clamp(mapped, seconds - MaxPredictionSeconds, seconds);
        authoritative = p with { X = Clamp(new(p.X, p.Y)).X, Y = Clamp(new(p.X, p.Y)).Y };
        tick = serverTick;
        lastTime = seconds;
        correction = Vector2.Zero;
        if (lifecycle || teleport || !Active(p))
        {
            history.Clear();
            sent.Clear();
            anchorTime = seconds;
        }
        else if (old is not null && Sample(seconds) is { } next)
        {
            var error = new Vector2(old.X - next.X, old.Y - next.Y);
            if (error.Length() <= SnapDistance)
                correction = error;
        }
        correctionAt = seconds;
        Trim(seconds);
    }

    public PlayerSnapshot? Sample(double seconds)
    {
        if (authoritative is not { } p)
            return null;
        if (!double.IsFinite(seconds) || seconds < lastTime)
            seconds = lastTime;
        if (!Active(p))
            return p;
        var position = new Vector2(p.X, p.Y);
        var aim = p.Aim;
        var velocity = new Vector2(p.VelocityX, p.VelocityY);
        var heading = p.Heading;
        var throttle = p.Throttle;
        var end = Math.Min(seconds, anchorTime + MaxPredictionSeconds);
        var time = anchorTime;
        var index = -1;
        while (index + 1 < history.Count && history[index + 1].At <= time)
            index++;
        // Fixed upper bound: <= 128 intent boundaries + 30 integration slices.
        while (time < end - 1e-9)
        {
            while (index + 1 < history.Count && history[index + 1].At <= time + 1e-9)
                index++;
            var intent = index >= 0 ? history[index] : default;
            var held = index >= 0 && time < intent.RefreshedAt + InputTimeoutSeconds - 1e-9;
            var next = Math.Min(end, time + 1d / Room.TickRate);
            if (index + 1 < history.Count)
                next = Math.Min(next, history[index + 1].At);
            if (held)
                next = Math.Min(next, intent.RefreshedAt + InputTimeoutSeconds);
            var dt = (float)(next - time);
            if (held)
                aim = intent.Aim;
            Movement.Steer(
                ref position,
                ref velocity,
                ref heading,
                ref throttle,
                held ? intent.Direction : Vector2.Zero,
                dt,
                p.MoveSpeed,
                p.Acceleration,
                p.Steering,
                geometry,
                p.Menu || held && intent.Menu,
                held,
                p.TurnRate,
                ChassisCatalog.Get(p.Chassis).Radius
            );
            time = next;
        }
        // Only visual residuals decay; authoritative gameplay fields are unchanged.
        position = Clamp(
            position + correction * (float)Math.Exp(-Math.Max(0, seconds - correctionAt) / .06)
        );
        return p with
        {
            X = position.X,
            Y = position.Y,
            Aim = aim,
            VelocityX = velocity.X,
            VelocityY = velocity.Y,
            Heading = heading,
            Throttle = throttle,
        };
    }

    private void Trim(double seconds)
    {
        while (history.Count > 1 && history[1].At < seconds - HistorySeconds)
            history.RemoveAt(0);
        while (history.Count > MaxHistory)
            history.RemoveAt(0);
        while (sent.Count > 0 && sent[0].At < seconds - HistorySeconds)
            sent.RemoveAt(0);
        while (sent.Count > MaxHistory)
            sent.RemoveAt(0);
    }
}
