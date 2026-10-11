using System.Numerics;
using System.Collections.Immutable;

namespace ArcadeDodger.Multiplayer;

public sealed record PlayerSnapshot(int Id, string Name, float X, float Y, float Aim,
    int Hull, int Shield, int Ack, bool Protected, bool Connected, int DashCooldown = 0, int Experience = 0, int Level = 1,
    int DashTicks = 0, float DashX = 0, float DashY = 0, int Life = 0,
    int SkillPoints = 0, ImmutableArray<string> Skills = default, int MaxShield = 2, string Weapon = "blaster", string Ability = "dash",
    int MaxHull = 3, float MoveSpeed = 300, float DashMultiplier = 3, int DashCooldownMax = 90, bool Editing = false, ImmutableArray<WeaponConfig> Weapons = default, int EquipSequence = 0, bool EquipAccepted = false, bool[]? UnlockedWeapons = null, WeaponStats[]? WeaponStats = null);
public sealed record ShotSnapshot(int Id, int Owner, float X, float Y, float Dx = 0, float Dy = -1, long Born = 0, int Kind = 1, int Effects = 0, bool Modular = false, int Parent = 0, float BirthX = 0, float BirthY = 0, int[]? Ancestors = null, int Weapon = 0, int Stage = 0, float Power = 1);
public sealed record RoomSnapshot(int V, string Type, long Tick, PlayerSnapshot[] Players, ShotSnapshot[] Shots, EnemySnapshot[]? Enemies = null, EnergySnapshot[]? Energy = null, ExplosionSnapshot[]? Explosions = null, BeamSnapshot[]? Beams = null, SocialSnapshot? Social = null, WorkshopSnapshot? Workshop = null, CreditSnapshot[]? Credits = null, MineSnapshot[]? Mines = null, SkillReceipt? SkillReceipt = null, CreditReward? CreditReward = null);

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
    public Room(bool enemiesEnabled = true, FriendBook? friends = null, ProfileStore? profiles = null)
    { this.enemiesEnabled = enemiesEnabled; this.friends = friends ?? new(); this.profiles = profiles ?? new(); }

    public int? Join(string name)
    {
        if (players.Count >= Capacity || players.Values.Any(p=>p.Connected && FriendBook.Key(p.Name)==FriendBook.Key(name)) || this.profiles.Ensure(name)!="ok") return null;
        var spawn = SelectSpawn();
        var id = ++nextPlayer;
        players.Add(id, new Player { Id = id, Name = name, Weapons = profiles.Get(name).Weapons.ToImmutableArray(), Position = spawn, Previous = spawn,
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
        players[id] = new Player { Id = id, Name = p.Name, Weapons = profiles.Get(p.Name).Weapons.ToImmutableArray(), Position = spawn, Previous = spawn,
            ProtectedUntil = Tick + 60, LastInputTick = Tick, LastReceived = request.Sequence, Ack = request.Sequence,
            Life = p.Life + 1 };
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
        p.Editing = false;
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
                p.Editing = pending.Editing && p.Hull > 0;
                if (p.Editing) p.DashUntil = 0;
                p.Ack = pending.Sequence;
                p.Pending = null;
            }
            if (Tick - p.LastInputTick > 7) p.Held = null;
            if (Tick - p.LastInputTick > 90) p.Editing = false;
            if (p.Hull > 0 && (p.Shield < MaxShieldFor(p) || p.ShieldFraction > 0) && Tick >= p.ShieldAt)
            {
                p.Shield = Math.Min(MaxShieldFor(p),p.Shield+1);
                if(p.Shield==MaxShieldFor(p))p.ShieldFraction=0;
                p.ShieldAt = Tick + (int)(TickRate / Value(p,"flux",1));
            }
            if (p.Hull > 0 && (p.Hull < MaxHullFor(p) || p.HullFraction > 0) && Rank(p,"repair")>0 && Tick >= p.HullAt)
            {
                p.Hull = Math.Min(MaxHullFor(p),p.Hull+1);
                if(p.Hull==MaxHullFor(p))p.HullFraction=0;
                p.HullAt = Tick + (int)(Value(p,"repair",10) * TickRate);
            }
            if (p.Hull <= 0 || p.Editing || p.Held is not { } input) continue;
            p.Aim = input.Aim;
            var direction = new Vector2(input.X, input.Y);
            if (direction.LengthSquared() > 1) direction = Vector2.Normalize(direction);
            if (input.Dash && Tick >= p.DashReadyAt)
            {
                p.DashDirection = direction.LengthSquared() > 0 ? Vector2.Normalize(direction)
                    : new Vector2(MathF.Cos(p.Aim), MathF.Sin(p.Aim));
                p.DashUntil = Tick + DashDurationForAbility(AbilityFor(p));
                p.DashReadyAt = Tick + DashCooldownFor(p);
                DashNova(p);
            }
            var movement = Tick < p.DashUntil ? p.DashDirection * DashMultiplierFor(p) : direction;
            p.Position = Vector2.Clamp(p.Position + movement * (MoveSpeedFor(p) / TickRate),
                new Vector2(Radius), new Vector2(Width - Radius, Height - Radius));
            if (input.Fire)
            {
                p.ProtectedUntil = 0;
                Fire(p);
            }
        }
        StepStatuses();
        if (enemiesEnabled) StepEnemies();
        StepBeams();
        StepProjectiles();
        StepMines();
        foreach (var enemy in enemies.Where(e => e.Hull <= 0)) { DropEnergy(enemy); DropCredits(enemy); }
        enemies.RemoveAll(e => e.Hull <= 0);
        StepEnergy();
        StepCredits();
        shots.RemoveAll(s => s.Expires <= Tick || s.Position.X < 0 || s.Position.X > Width ||
            s.Position.Y < 0 || s.Position.Y > Height);
        foreach (var id in players.Values.Where(p => !p.Connected && Tick >= p.RemoveAt).Select(p => p.Id).ToArray())
            players.Remove(id);
    }

    public RoomSnapshot Snapshot() => new(ClientProtocol.Version, "snapshot", Tick,
        players.Values.Select(p => new PlayerSnapshot(p.Id, p.Name, p.Position.X, p.Position.Y,
            p.Aim, p.Hull, p.Shield, p.Ack, p.Editing || Tick < p.ProtectedUntil, p.Connected,
            (int)Math.Max(0, p.DashReadyAt - Tick), p.Experience, LevelForExperience(p.Experience),
            (int)Math.Max(0, p.DashUntil - Tick), p.DashDirection.X, p.DashDirection.Y, p.Life,
            SkillPointsFor(p), p.Skills.Order().ToImmutableArray(), MaxShieldFor(p), WeaponFor(p), AbilityFor(p),
            MaxHullFor(p), MoveSpeedFor(p), DashMultiplierFor(p), DashCooldownFor(p), p.Editing, p.Weapons, p.EquipSequence, p.EquipAccepted, Enumerable.Range(0,4).Select(i=>Unlocked(p,i)).ToArray(), Enumerable.Range(1,4).Select(e=>CalculateWeaponStats(p.Skills,e)).ToArray())).ToArray(),
        shots.Select(s => new ShotSnapshot(s.Id, s.Owner, s.Position.X, s.Position.Y, s.Velocity.X, s.Velocity.Y, s.Born, s.Kind, Effects(s), s.Program is not null, s.Parent, s.BirthPosition.X, s.BirthPosition.Y, s.Ancestors.Length > 0 ? s.Ancestors : null, s.Weapon, s.Cursor, s.VisualPower)).ToArray(),
        enemies.Select(e => new EnemySnapshot(e.Id, e.Gunner ? "gunner" : "rammer", e.Position.X, e.Position.Y,
            e.Aim, e.Hull, e.Gunner && e.FireAt - Tick <= 15, (Tick < e.BurnUntil ? 1 : 0) | (Tick < e.RadiationUntil ? 2 : 0) | (Tick < e.SlowUntil ? 4 : 0) | (Tick < e.CorrosionUntil ? 8 : 0) | (Tick < e.StunUntil ? 16 : 0) | (Tick < e.MarkUntil ? 32 : 0))).ToArray(),
        energy.Select(e => new EnergySnapshot(e.Id, e.Position.X, e.Position.Y, e.Value)).ToArray(), explosions.ToArray(), beams.ToArray(), Credits: credits.Select(c=>new CreditSnapshot(c.Id,c.Position.X,c.Position.Y,c.Value)).ToArray(), Mines: mines.Select(m=>new MineSnapshot(m.Id,m.Payload.Owner,m.Payload.Position.X,m.Payload.Position.Y,Tick>=m.ArmedAt,m.Payload.VisualPower)).ToArray());

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
        if (target.Hull <= 0 || target.Editing || (HasSkill(target, PhaseDash) && Tick < target.DashUntil)) return;
        if (target.Shield > 0) target.Shield--; else target.Hull--;
        target.ShieldAt = Tick + ShieldDelay(target);
        target.HullAt = Tick + 10 * TickRate;
    }

    private sealed class Player
    {
        public int Id, Hull = 3, Shield = 2, Ack, LastReceived, Experience, Life;
        public HashSet<string> Skills = new();
        public SkillReceipt? SkillReceipt;
        public CreditReward? CreditReward;
        public int CreditSequence;
        public string Name = "Pilot";
        public Vector2 Position, Previous, DashDirection;
        public float Aim;
        public bool Connected = true, Editing, EquipAccepted;
        public int EquipSequence, SocialSequence;
        public string SocialResult = "";
        public ImmutableArray<WeaponConfig> Weapons = PilotProfile.Starter().Weapons.ToImmutableArray();
        public double[] WeaponReady = new double[4];
        public long[] PlasmaReady = new long[4], MineReady = new long[4];
        public int WorkshopSequence; public string WorkshopResult = "";
        public float ShieldFraction, HullFraction, LeechFraction;
        public long ProtectedUntil, RemoveAt, LastInputTick, DashUntil, DashReadyAt, ShieldAt, HullAt;
        public InputMessage? Held, Pending;
    }
    private sealed class Shot
    {
        public int Id, Owner, Parent, Weapon;
        public float Damage = 1, InitialRange, VisualPower = 1, Spread = 1, ShieldMultiplier = 1;
        public int Bounces, Relays, Burns, Freezes, Rads, Corrodes, Teslas, Gravity, Blasts, Leeches;
        public bool Proximity, SalvageMark, IsBeam, Secondary, EmitPlasma, EmitMine;
        public string OwnerName = "";
        public Vector2 BirthPosition;
        public Vector2 Position, Velocity;
        public long Expires, Born;
        public int Kind = 1, Cursor, Pierce;
        public float SpeedMultiplier = 1, StatusPower = 1, SplashMultiplier = 1, RadiusMultiplier = 1;
        public int[]? Program;
        public int[] Ancestors = [];
        public float Distance, Range, Blast;
        public bool Burning, Radiation, Cryo, Homing;
        public HashSet<int> HitEnemies = new(), HitPlayers = new();
        public Shot Copy()
        {
            var copy = (Shot)MemberwiseClone();
            copy.HitEnemies = new(HitEnemies); copy.HitPlayers = new(HitPlayers);
            return copy;
        }
    }
}
