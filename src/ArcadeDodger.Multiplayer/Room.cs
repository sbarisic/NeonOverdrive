using System.Numerics;
using System.Collections.Immutable;

namespace ArcadeDodger.Multiplayer;

public sealed record PlayerSnapshot(int Id, string Name, float X, float Y, float Aim,
    int Hull, int Shield, int Ack, bool Protected, bool Connected, float VelocityX = 0, int Experience = 0, int Level = 1,
    float VelocityY = 0, float Acceleration = 1200, float Steering = 1800, int Life = 0,
    int SkillPoints = 0, ImmutableArray<string> Skills = default, int MaxShield = 2, string Weapon = "blaster",
    int MaxHull = 3, float MoveSpeed = 300, bool Menu = false, bool Editing = false, ImmutableArray<WeaponConfig> Weapons = default, int EquipSequence = 0, bool EquipAccepted = false, bool[]? UnlockedWeapons = null, WeaponStats[]? WeaponStats = null, int Docked = 0, string RunId = "");
public sealed record ShotSnapshot(int Id, int Owner, float X, float Y, float Dx = 0, float Dy = -1, long Born = 0, int Kind = 1, int Effects = 0, bool Modular = false, int Parent = 0, float BirthX = 0, float BirthY = 0, int[]? Ancestors = null, int Weapon = 0, int Stage = 0, float Power = 1);
public sealed record RoomSnapshot(int V, string Type, long Tick, PlayerSnapshot[] Players, ShotSnapshot[] Shots, EnemySnapshot[]? Enemies = null, EnergySnapshot[]? Energy = null, ExplosionSnapshot[]? Explosions = null, BeamSnapshot[]? Beams = null, SocialSnapshot? Social = null, WorkshopSnapshot? Workshop = null, CreditSnapshot[]? Credits = null, MineSnapshot[]? Mines = null, SkillReceipt? SkillReceipt = null, CreditReward? CreditReward = null, SectorState? Sector = null, ModuleDropSnapshot[]? ModuleDrops = null,int PrivateRevision=0,CombatEvent[]? CombatEvents=null);

/// <summary>Single-writer fixed-step network-slice simulation; no renderer or wall-clock dependency.</summary>
public sealed partial class Room
{
    public const int TickRate = 30;
    public const float Width = SectorDefinition.DefaultWidth, Height = SectorDefinition.DefaultHeight, Radius = 14, Speed = 300;
    public const int Capacity = 24;
    public const int ProjectileCapacity = 512; // Verified with 24-pilot transport and browser playback benchmarks.
    private readonly SortedDictionary<int, Player> players = new();
    private readonly List<Shot> shots = new();
    public int ProjectileLimit {get;}
    public int EnemyLimit {get;}
    private int nextPlayer, nextShot;
    private readonly SpatialGrid<Enemy> enemyGrid=new();
    private readonly SpatialGrid<Player> playerGrid=new();
    public long Tick { get; private set; }
    public int PlayerCount => players.Count;
    private readonly bool enemiesEnabled;

    // Server rooms always use the default; isolated combat checks can disable AI.
    public Room(bool enemiesEnabled = true, FriendBook? friends = null, ProfileStore? profiles = null, bool sectorEnabled = true, int? seed = null,int projectileLimit=ProjectileCapacity,int enemyLimit=EnemyCapacity)
    { ProjectileLimit=Math.Clamp(projectileLimit,256,1024);EnemyLimit=Math.Clamp(enemyLimit,96,256);this.enemiesEnabled = enemiesEnabled; this.friends = friends ?? new(); this.profiles = profiles ?? new(); Sector=sectorEnabled?SectorDefinition.Generate(seed??Random.Shared.Next()):null; geometry=Sector is null?null:new(Sector); InitializeSector(); }

    public int? Join(string name)
    {
        if (players.Values.Any(p=>p.Connected && FriendBook.Key(p.Name)==FriendBook.Key(name)) || this.profiles.Ensure(name)!="ok") return null;
        var key=FriendBook.Key(name);
        var returning=players.Values.FirstOrDefault(p=>!p.Connected&&FriendBook.Key(p.Name)==key);
        if(returning is null)suspended.Remove(key,out returning);
        if(returning is not null)
        {
            if(!players.ContainsKey(returning.Id))players.Add(returning.Id,returning);
            returning.Connected=true;returning.LastReceived=0;returning.Ack=0;returning.LastInputTick=Tick;
            returning.Held=null;returning.Pending=null;returning.SkillReceipt=null;returning.WorkshopSequence=0;returning.WorkshopResult="";
            returning.EquipSequence=0;returning.SocialSequence=0;returning.SocialResult="";
            if(Sector is not null){var state=ExplorationFor(returning);state.Receipt=null;state.SessionPlayer=returning.Id;}
            return returning.Id;
        }
        if(players.Count>=Capacity)return null;
        var spawn = SelectSpawn();
        var id = ++nextPlayer;
        players.Add(id, new Player { Id = id, Name = name, Weapons = profiles.Get(name).Weapons.ToImmutableArray(), Position = spawn, Previous = spawn,
            ProtectedUntil = Tick + 60, LastInputTick = Tick, RunId=profiles.Get(name).RunId });
        RestoreCheckpoint(players[id],profiles.Get(name).Run);
        if(Sector is not null){var s=ExplorationFor(players[id]);s.SessionPlayer=id;s.Receipt=null;s.InteractionTarget=0;}
        return id;
    }

    private Vector2 SelectSpawn(int station=10)
    {
        if(Location(station) is {} port)return port.Position+new Vector2(140,0);
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
        PilotDied(p);
        if(!p.ResetComplete)return false;
        var spawn = SelectSpawn(Sector is null?10:ExplorationFor(p).LastStation);
        players[id] = new Player { Id = id, Name = p.Name, Weapons = profiles.Get(p.Name).Weapons.ToImmutableArray(), Position = spawn, Previous = spawn,
            ProtectedUntil = Tick + 60, LastInputTick = Tick, LastReceived = request.Sequence, Ack = request.Sequence,
            RunId=profiles.Get(p.Name).RunId, Life = p.Life + 1 };
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
        p.Editing = false;p.Menu=false;
        SaveCheckpoint(p);
    }

    public void Step()
    {
        Tick++;
        CompleteTransactions();
        foreach (var p in players.Values.Skip((int)(Tick%Math.Max(1,players.Count))).Concat(players.Values.Take((int)(Tick%Math.Max(1,players.Count)))))
        {
            p.Previous = p.Position;
            if (p.Pending is { } pending)
            {
                p.Held = pending;
                p.Editing = pending.Editing && p.Hull > 0;
                p.Menu = pending.Menu;
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
            var input=p.Held;
            if(input is not null)p.Aim=input.Aim;
            var frozen=p.Hull<=0||p.Editing||p.Menu||p.Docked!=0;
            Movement.Step(ref p.Position,ref p.Velocity,input is null?Vector2.Zero:new(input.X,input.Y),1f/TickRate,MoveSpeedFor(p),AccelerationFor(p),SteeringFor(p),geometry,frozen);
            if(frozen||input is null)continue;
            if (input.Fire && !Safe(p))
            {
                p.ProtectedUntil = 0;
                Fire(p);
            }
        }
        StepExploration();
        enemyGrid.Rebuild(enemies,e=>e.Position);playerGrid.Rebuild(players.Values,p=>p.Position);
        StepStatuses();
        if (enemiesEnabled) StepEnemies();
        enemyGrid.Rebuild(enemies,e=>e.Position);
        StepBeams();
        StepProjectiles();
        StepMines();
        beams.RemoveAll(b=>players.TryGetValue(b.Owner,out var owner)&&owner.Hull<=0);
        foreach (var enemy in enemies.Where(e => e.Hull <= 0)) { CombatFx(0,2,enemy.Position,EnemyRadius(enemy)); RewardEncounter(enemy); DestroyInstallation(enemy); if(!enemy.Installation){DropEnergy(enemy); DropCredits(enemy);} }
        enemies.RemoveAll(e => e.Hull <= 0);
        AdmitPersonalDrops();
        StepPickupMotion();
        StepEnergy();
        StepCredits();
        StepModuleDrops();
        if(Tick%30==0)foreach(var p in players.Values){if(p.Hull<=0)ResetAfterDeath(p);else SaveCheckpoint(p);}
        shots.RemoveAll(s => {
            if(s.Expires>Tick&&s.Position.X>=0&&s.Position.X<=Width&&s.Position.Y>=0&&s.Position.Y<=Height)return false;
            CombatFx(s.Id,s.Impact?1:0,s.Position,s.VisualPower);return true;
        });
        foreach (var id in players.Values.Where(p => !p.Connected && Tick >= p.RemoveAt).Select(p => p.Id).ToArray())
            {var p=players[id];SaveCheckpoint(p);suspended[FriendBook.Key(p.Name)]=p;players.Remove(id);}
    }

    public RoomSnapshot Snapshot() => new(ClientProtocol.Version, "snapshot", Tick,
        players.Values.Select(p => new PlayerSnapshot(p.Id, p.Name, p.Position.X, p.Position.Y,
            p.Aim, p.Hull, p.Shield, p.Ack, p.Editing || Safe(p) || Tick < p.ProtectedUntil, p.Connected,
            p.Velocity.X, p.Experience, LevelForExperience(p.Experience),
            p.Velocity.Y, AccelerationFor(p), SteeringFor(p), p.Life,
            SkillPointsFor(p), p.Skills.Order().ToImmutableArray(), MaxShieldFor(p), WeaponFor(p),
            MaxHullFor(p), MoveSpeedFor(p), p.Menu, p.Editing, p.Weapons, p.EquipSequence, p.EquipAccepted, Enumerable.Range(0,4).Select(i=>Unlocked(p,i)).ToArray(), Enumerable.Range(1,4).Select(e=>Stats(p,e)).ToArray(),p.Docked,p.RunId)).ToArray(),
        shots.Select(s => new ShotSnapshot(s.Id, s.Owner, s.Position.X, s.Position.Y, s.Velocity.X, s.Velocity.Y, s.Born, s.Kind, Effects(s), s.Program is not null, s.Parent, s.BirthPosition.X, s.BirthPosition.Y, s.Ancestors.Length > 0 ? s.Ancestors : null, s.Weapon, s.Cursor, s.VisualPower)).ToArray(),
        enemies.Select(e => new EnemySnapshot(e.Id, e.Installation ? "installation" : e.Type, e.Position.X, e.Position.Y,
            e.Aim, e.Hull, e.Gunner && e.State=="engage" && e.FireAt>Tick && e.FireAt - Tick <= (e.Type=="sniper"?30:15), (Tick < e.BurnUntil ? 1 : 0) | (Tick < e.RadiationUntil ? 2 : 0) | (Tick < e.SlowUntil ? 4 : 0) | (Tick < e.CorrosionUntil ? 8 : 0) | (Tick < e.StunUntil ? 16 : 0) | (Tick < e.MarkUntil ? 32 : 0),e.Home,e.State,e.Level,EnemyRadius(e),e.MaxHull)).ToArray(),
        energy.Select(e => new EnergySnapshot(e.Id, e.Position.X, e.Position.Y, e.Value,e.Owner)).ToArray(), explosions.ToArray(), beams.ToArray(), Credits: credits.Select(c=>new CreditSnapshot(c.Id,c.Position.X,c.Position.Y,c.Value,c.Owner)).ToArray(), Mines: mines.Select(m=>new MineSnapshot(m.Id,m.Payload.Owner,m.Payload.Position.X,m.Payload.Position.Y,Tick>=m.ArmedAt,m.Payload.VisualPower)).ToArray(), ModuleDrops:moduleDrops.Select(m=>new ModuleDropSnapshot(m.Id,m.Position.X,m.Position.Y,m.Module,m.Owner)).ToArray(),CombatEvents:combatEvents.Where(e=>Tick-e.Born<=8).ToArray());

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
        if (target.Hull <= 0 || target.Editing || Safe(target)) return;
        target.LastDamage=Tick;
        if (target.Shield > 0) target.Shield--; else target.Hull--;
        if(target.Hull<=0)PilotDied(target);
        target.ShieldAt = Tick + ShieldDelay(target);
        target.HullAt = Tick + 10 * TickRate;
    }

    private sealed class Player
    {
        public string LootNotice="";public long LootNoticeUntil;
        public bool Menu;
        public string RunId=""; public bool ResetPending,ResetComplete,CheckpointPending,EconomyPending;
        public int Id, Hull = 3, Shield = 2, Ack, LastReceived, Experience, Life;
        public HashSet<string>? StatsSkills; public int StatsCount=-1;public WeaponStats[] StatsCache=[];
        public HashSet<string> Skills = new();
        public SkillReceipt? SkillReceipt;
        public CreditReward? CreditReward;
        public int CreditSequence, Docked; public long LastDamage=-1;
        public string Name = "Pilot";
        public Vector2 Position, Previous, Velocity;
        public float Aim;
        public bool Connected = true, Editing, EquipAccepted;
        public int EquipSequence, SocialSequence;
        public string SocialResult = "";
        public ImmutableArray<WeaponConfig> Weapons = PilotProfile.Starter().Weapons.ToImmutableArray();
        public double[] WeaponReady = new double[4];
        public long[] PlasmaReady = new long[4], MineReady = new long[4];
        public int WorkshopSequence; public string WorkshopResult = "";
        public float ShieldFraction, HullFraction, LeechFraction;
        public long ProtectedUntil, RemoveAt, LastInputTick, ShieldAt, HullAt;
        public InputMessage? Held, Pending;
    }
    private sealed class Shot
    {
        public int Id, Owner, Parent, Weapon;
        public float Damage = 1, InitialRange, VisualPower = 1, Spread = 1, ShieldMultiplier = 1;
        public int Bounces, Relays, Burns, Freezes, Rads, Corrodes, Teslas, Gravity, Blasts, Leeches;
        public bool Impact, Cancelled, Proximity, SalvageMark, IsBeam, Secondary, EmitPlasma, EmitMine;
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
