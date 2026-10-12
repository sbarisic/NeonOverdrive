using System.Collections.Immutable;
using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private sealed class Player
    {
        public int[] GearTiers = new int[12];
        public int InventoryPage;
        public string LootNotice = "";
        public long LootNoticeUntil;
        public bool Menu;
        public string RunId = "";
        public long DisconnectedAt;
        public bool RecoveryDisabled;
        public bool Recovered;
        public int LeaveSequence;
        public string ResetResult = "";
        public bool ResetPending,
            ResetComplete,
            CheckpointPending,
            EconomyPending;
        public int Id,
            Hull = 3,
            Shield = 2,
            Ack,
            LastReceived,
            Experience,
            Life;
        public HashSet<string>? StatsSkills;
        public int StatsCount = -1;
        public WeaponStats[] StatsCache = [];
        public HashSet<string> Skills = new();
        public SkillReceipt? SkillReceipt;
        public CreditReward? CreditReward;
        public int CreditSequence,
            Docked;
        public long LastDamage = -1;
        public string Name = "Pilot";
        public Vector2 Position,
            Previous,
            Velocity;
        public float Aim;
        public bool Connected = true,
            Editing,
            EquipAccepted;
        public int EquipSequence,
            SocialSequence;
        public string SocialResult = "";
        public ImmutableArray<WeaponConfig> Weapons = PilotProfile
            .Starter()
            .Weapons.ToImmutableArray();
        public double[] WeaponReady = new double[4];
        public long[] PlasmaReady = new long[4],
            MineReady = new long[4];
        public int WorkshopSequence;
        public string WorkshopResult = "";
        public float ShieldFraction,
            HullFraction,
            LeechFraction;
        public long ProtectedUntil,
            RemoveAt,
            LastInputTick,
            ShieldAt,
            HullAt;
        public InputMessage? Held,
            Pending;
    }

    private sealed class Shot
    {
        public int Id,
            Owner,
            Parent,
            Weapon;
        public float Damage = 1,
            InitialRange,
            VisualPower = 1,
            Spread = 1,
            ShieldMultiplier = 1;
        public int Bounces,
            Relays,
            Burns,
            Freezes,
            Rads,
            Corrodes,
            Teslas,
            Gravity,
            Blasts,
            Leeches;
        public bool Impact,
            Cancelled,
            Proximity,
            SalvageMark,
            IsBeam,
            Secondary,
            EmitPlasma,
            EmitMine;
        public string OwnerName = "";
        public Vector2 BirthPosition;
        public Vector2 Position,
            Velocity;
        public long Expires,
            Born;
        public bool FriendlyNpc;
        public int Kind = 1,
            Cursor,
            Pierce;
        public float SpeedMultiplier = 1,
            StatusPower = 1,
            SplashMultiplier = 1,
            RadiusMultiplier = 1;
        public int[]? Program;
        public int[] Ancestors = [];
        public float Distance,
            Range,
            Blast;
        public bool Burning,
            Radiation,
            Cryo,
            Homing;
        public HashSet<int> HitEnemies = new(),
            HitPlayers = new();

        public Shot Copy()
        {
            var copy = (Shot)MemberwiseClone();
            copy.HitEnemies = new(HitEnemies);
            copy.HitPlayers = new(HitPlayers);
            return copy;
        }
    }
}
