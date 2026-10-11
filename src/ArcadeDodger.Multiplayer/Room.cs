using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed record PlayerSnapshot(int Id, string Name, float X, float Y, float Aim,
    int Hull, int Shield, int Ack, bool Protected, bool Connected, int DashCooldown = 0, int Experience = 0, int Level = 1);
public sealed record ShotSnapshot(int Id, int Owner, float X, float Y, float Dx = 0, float Dy = -1, long Born = 0);
public sealed record RoomSnapshot(int V, string Type, long Tick, PlayerSnapshot[] Players, ShotSnapshot[] Shots, EnemySnapshot[]? Enemies = null, EnergySnapshot[]? Energy = null);

/// <summary>Single-writer fixed-step network-slice simulation; no renderer or wall-clock dependency.</summary>
public sealed partial class Room
{
    public const int TickRate = 30;
    public const float Width = 2880, Height = 1620, Radius = 14, Speed = 300;
    public const int Capacity = 24;
    public const int DashDuration = 6, DashCooldownTicks = 90;
    private readonly SortedDictionary<int, Player> players = new();
    private readonly List<Shot> shots = new();
    private int nextPlayer, nextShot;
    public long Tick { get; private set; }
    public int PlayerCount => players.Count;
    private readonly bool enemiesEnabled;

    // Server rooms always use the default; isolated combat checks can disable AI.
    public Room(bool enemiesEnabled = true) => this.enemiesEnabled = enemiesEnabled;

    public int? Join(string name)
    {
        if (players.Count >= Capacity) return null;
        var spawn = SelectSpawn();
        var id = ++nextPlayer;
        players.Add(id, new Player { Id = id, Name = name, Position = spawn, Previous = spawn,
            ProtectedUntil = Tick + 60, LastInputTick = Tick });
        return id;
    }

    private Vector2 SelectSpawn()
    {
        var candidates = Enumerable.Range(0, 16).Select(i => new Vector2(
            Width / 2 + MathF.Cos(i * MathF.Tau / 16) * (Width / 2 - 100),
            Height / 2 + MathF.Sin(i * MathF.Tau / 16) * (Height / 2 - 100)));
        return candidates.OrderByDescending(c => players.Values.Where(p => p.Hull > 0)
            .Select(p => p.Position).Concat(enemies.Where(e => e.Hull > 0).Select(e => e.Position))
            .Concat(shots.Select(s => s.Position))
            .Select(p => Vector2.DistanceSquared(c, p)).DefaultIfEmpty(float.MaxValue).Min()).First();
    }

    public bool Respawn(int id, RespawnMessage request)
    {
        if (!players.TryGetValue(id, out var p) || !p.Connected || request.Sequence <= p.LastReceived) return false;
        p.LastReceived = request.Sequence;
        if (p.Hull > 0) return false;
        var spawn = SelectSpawn();
        players[id] = new Player { Id = id, Name = p.Name, Position = spawn, Previous = spawn,
            ProtectedUntil = Tick + 60, LastInputTick = Tick, LastReceived = request.Sequence, Ack = request.Sequence };
        return true;
    }
    public bool Input(int id, InputMessage input)
    {
        if (!players.TryGetValue(id, out var p) || !p.Connected || input.Sequence <= p.LastReceived ||
            !float.IsFinite(input.X) || !float.IsFinite(input.Y) || !float.IsFinite(input.Aim) ||
            MathF.Abs(input.X) > 1 || MathF.Abs(input.Y) > 1 || MathF.Abs(input.Aim) > MathF.PI) return false;
        p.LastReceived = input.Sequence;
        p.Pending = input; // Latest intent only: packet rate cannot increase movement rate.
        p.LastInputTick = Tick;
        return true;
    }

    public void Disconnect(int id)
    {
        if (!players.TryGetValue(id, out var p) || !p.Connected) return;
        p.Connected = false;
        p.RemoveAt = Tick + 150;
        p.Pending = null;
        p.Held = null;
    }

    public void Step()
    {
        Tick++;
        foreach (var p in players.Values)
        {
            p.Previous = p.Position;
            if (p.Pending is { } pending)
            {
                p.Held = pending;
                p.Ack = pending.Sequence;
                p.Pending = null;
            }
            if (Tick - p.LastInputTick > 7) p.Held = null;
            if (p.Hull > 0 && p.Shield < 2 && Tick >= p.ShieldAt)
            {
                p.Shield++;
                p.ShieldAt = Tick + TickRate;
            }
            if (p.Hull <= 0 || p.Held is not { } input) continue;
            p.Aim = input.Aim;
            var direction = new Vector2(input.X, input.Y);
            if (direction.LengthSquared() > 1) direction = Vector2.Normalize(direction);
            if (input.Dash && Tick >= p.DashReadyAt)
            {
                p.DashDirection = direction.LengthSquared() > 0 ? Vector2.Normalize(direction)
                    : new Vector2(MathF.Cos(p.Aim), MathF.Sin(p.Aim));
                p.DashUntil = Tick + DashDuration;
                p.DashReadyAt = Tick + DashCooldownTicks;
            }
            var movement = Tick < p.DashUntil ? p.DashDirection * 3 : direction;
            p.Position = Vector2.Clamp(p.Position + movement * (Speed / TickRate),
                new Vector2(Radius), new Vector2(Width - Radius, Height - Radius));
            if (input.Fire)
            {
                p.ProtectedUntil = 0;
                if (Tick >= p.FireAt && shots.Count < 256)
                {
                    p.FireAt = Tick + 7;
                    shots.Add(new Shot { Id = ++nextShot, Owner = p.Id, Position = p.Position,
                        Velocity = new Vector2(MathF.Cos(p.Aim), MathF.Sin(p.Aim)) * (650f / TickRate),
                        Born = Tick, Expires = Tick + 60 });
                }
            }
        }
        if (enemiesEnabled) StepEnemies();
        foreach (var shot in shots)
        {
            var end = shot.Position + shot.Velocity;
            Player? target = null;
            Enemy? enemyTarget = null;
            var closest = float.MaxValue;
            foreach (var p in players.Values)
            {
                if (p.Id == shot.Owner || p.Hull <= 0 || Tick < p.ProtectedUntil) continue;
                var time = Intersection(shot.Position - p.Previous, end - p.Position, Radius + 3);
                if (time.HasValue && time.Value < closest) { closest = time.Value; target = p; }
            }
            if (shot.Owner > 0)
                foreach (var enemy in enemies)
                {
                    if (enemy.Hull <= 0) continue;
                    var time = Intersection(shot.Position - enemy.Previous, end - enemy.Position, 23);
                    if (time.HasValue && time.Value < closest)
                    { closest = time.Value; enemyTarget = enemy; target = null; }
                }
            if (enemyTarget is not null)
            { enemyTarget.Hull--; shot.Expires = Tick; }
            if (target is not null)
            {
                Damage(target);
                shot.Expires = Tick;
            }
            shot.Position = end;
        }
        foreach (var enemy in enemies.Where(e => e.Hull <= 0)) DropEnergy(enemy);
        enemies.RemoveAll(e => e.Hull <= 0);
        StepEnergy();
        shots.RemoveAll(s => s.Expires <= Tick || s.Position.X < 0 || s.Position.X > Width ||
            s.Position.Y < 0 || s.Position.Y > Height);
        foreach (var id in players.Values.Where(p => !p.Connected && Tick >= p.RemoveAt).Select(p => p.Id).ToArray())
            players.Remove(id);
    }

    public RoomSnapshot Snapshot() => new(ClientProtocol.Version, "snapshot", Tick,
        players.Values.Select(p => new PlayerSnapshot(p.Id, p.Name, p.Position.X, p.Position.Y,
            p.Aim, p.Hull, p.Shield, p.Ack, Tick < p.ProtectedUntil, p.Connected,
            (int)Math.Max(0, p.DashReadyAt - Tick), p.Experience, LevelForExperience(p.Experience))).ToArray(),
        shots.Select(s => new ShotSnapshot(s.Id, s.Owner, s.Position.X, s.Position.Y, s.Velocity.X, s.Velocity.Y, s.Born)).ToArray(),
        enemies.Select(e => new EnemySnapshot(e.Id, e.Gunner ? "gunner" : "rammer", e.Position.X, e.Position.Y,
            e.Aim, e.Hull, e.Gunner && e.FireAt - Tick <= 15)).ToArray(),
        energy.Select(e => new EnergySnapshot(e.Id, e.Position.X, e.Position.Y, e.Value)).ToArray());

    private static float? Intersection(Vector2 start, Vector2 end, float radius)
    {
        var c = start.LengthSquared() - radius * radius;
        if (c <= 0) return 0;
        var delta = end - start;
        var a = delta.LengthSquared();
        if (a < 0.000001f) return null;
        var b = 2 * Vector2.Dot(start, delta);
        var discriminant = b * b - 4 * a * c;
        if (discriminant < 0) return null;
        var t = (-b - MathF.Sqrt(discriminant)) / (2 * a);
        return t is >= 0 and <= 1 ? t : null;
    }

    private void Damage(Player target)
    {
        if (target.Shield > 0) target.Shield--; else target.Hull--;
        target.ShieldAt = Tick + 5 * TickRate;
    }

    private sealed class Player
    {
        public int Id, Hull = 3, Shield = 2, Ack, LastReceived, Experience;
        public string Name = "Pilot";
        public Vector2 Position, Previous, DashDirection;
        public float Aim;
        public bool Connected = true;
        public long ProtectedUntil, FireAt, RemoveAt, LastInputTick, DashUntil, DashReadyAt, ShieldAt;
        public InputMessage? Held, Pending;
    }
    private sealed class Shot
    {
        public int Id, Owner;
        public Vector2 Position, Velocity;
        public long Expires, Born;
    }
}
