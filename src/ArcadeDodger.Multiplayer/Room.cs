using System.Collections.Immutable;
using System.Numerics;

namespace ArcadeDodger.Multiplayer;

/// <summary>Single-writer fixed-step simulation. An injectable clock bounds reconnect recovery.</summary>
public sealed partial class Room
{
    public const int TickRate = 30;
    public const float Width = SectorDefinition.DefaultWidth,
        Height = SectorDefinition.DefaultHeight,
        Radius = 14,
        Speed = 300;
    public const int Capacity = 24;
    public const int ProjectileCapacity = 512; // Verified with 24-pilot transport and browser playback benchmarks.
    private readonly SortedDictionary<int, Player> players = new();
    private readonly List<Shot> shots = new();
    public int ProjectileLimit { get; }
    public int EnemyLimit { get; }
    private int nextPlayer,
        nextShot;
    private readonly SpatialGrid<Enemy> enemyGrid = new();
    private readonly SpatialGrid<Player> playerGrid = new();
    public long Tick { get; private set; }
    public int PlayerCount => players.Count;
    private readonly bool enemiesEnabled;
    private readonly TimeProvider clock;

    // Server rooms always use the default; isolated combat checks can disable AI.
    public Room(
        bool enemiesEnabled = true,
        FriendBook? friends = null,
        ProfileStore? profiles = null,
        bool sectorEnabled = true,
        int? seed = null,
        int projectileLimit = ProjectileCapacity,
        int enemyLimit = EnemyCapacity,
        TimeProvider? clock = null
    )
    {
        ProjectileLimit = Math.Clamp(projectileLimit, 256, 1024);
        EnemyLimit = Math.Clamp(enemyLimit, 96, 256);
        this.enemiesEnabled = enemiesEnabled;
        this.clock = clock ?? TimeProvider.System;
        this.friends = friends ?? new();
        this.profiles = profiles ?? new();
        Sector = sectorEnabled ? SectorDefinition.Generate(seed ?? Random.Shared.Next()) : null;
        geometry = Sector is null ? null : new(Sector);
        navigation = Sector is null ? null : new(Sector, geometry!);
        InitializeSector();
        InitializeAllies();
        InitializeOutposts();
        InitializeContacts();
    }

    public void Step()
    {
        Tick++;
        CompleteTransactions();
        StepContacts();
        foreach (
            var p in players
                .Values.Skip((int)(Tick % Math.Max(1, players.Count)))
                .Concat(players.Values.Take((int)(Tick % Math.Max(1, players.Count))))
        )
            StepPlayer(p);
        StepExploration();
        enemyGrid.Rebuild(enemies, e => e.Position);
        playerGrid.Rebuild(players.Values, p => p.Position);
        StepStatuses();
        if (enemiesEnabled)
            StepEnemies();
        enemyGrid.Rebuild(enemies, e => e.Position);
        StepBeams();
        StepProjectiles();
        StepMines();
        beams.RemoveAll(b => players.TryGetValue(b.Owner, out var owner) && owner.Hull <= 0);
        StepRescue();
        StepOutposts();
        foreach (var enemy in enemies.Where(e => e.Hull <= 0))
        {
            CombatFx(0, 2, enemy.Position, EnemyRadius(enemy));
            if (!enemy.Allied)
                RewardEncounter(enemy);
            DestroyInstallation(enemy);
            if (!enemy.Installation && !enemy.Allied)
            {
                DropEnergy(enemy);
                DropCredits(enemy);
            }
        }
        enemies.RemoveAll(e => e.Hull <= 0 || e.Type == "relief" && e.State == "arrived");
        AdmitPersonalDrops();
        StepPickupMotion();
        StepEnergy();
        StepCredits();
        StepModuleDrops();
        if (Tick % 30 == 0)
            foreach (var p in players.Values)
            {
                if (p.Hull <= 0)
                    ResetAfterDeath(p);
                else
                    SaveCheckpoint(p);
            }
        shots.RemoveAll(s =>
        {
            if (
                s.Expires > Tick
                && s.Position.X >= 0
                && s.Position.X <= Width
                && s.Position.Y >= 0
                && s.Position.Y <= Height
            )
                return false;
            CombatFx(s.Id, s.Impact ? 1 : 0, s.Position, s.VisualPower);
            return true;
        });
        foreach (
            var id in players
                .Values.Where(p => !p.Connected && Tick >= p.RemoveAt)
                .Select(p => p.Id)
                .ToArray()
        )
        {
            var p = players[id];
            SaveCheckpoint(p);
            suspended[FriendBook.Key(p.Name)] = p;
            players.Remove(id);
        }
    }

    private static float? Intersection(Vector2 start, Vector2 end, float radius)
    {
        var c = start.LengthSquared() - radius * radius;
        if (c <= 0)
            return 0;
        var delta = end - start;
        var a = delta.LengthSquared();
        if (a < 0.000001f)
            return null;
        var b = 2 * Vector2.Dot(start, delta);
        var discriminant = b * b - 4 * a * c;
        if (discriminant < 0)
            return null;
        var t = (-b - MathF.Sqrt(discriminant)) / (2 * a);
        return t is >= 0 and <= 1 ? t : null;
    }

    private void Damage(Player target)
    {
        if (target.Hull <= 0 || target.Editing || Safe(target))
            return;
        target.LastDamage = Tick;
        if (target.Shield > 0)
            target.Shield--;
        else
            target.Hull--;
        if (target.Hull <= 0)
            PilotDied(target);
        target.ShieldAt = Tick + ShieldDelay(target);
        target.HullAt = Tick + 10 * TickRate;
    }
}
