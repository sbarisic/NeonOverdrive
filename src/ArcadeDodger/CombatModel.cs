using System.Numerics;
namespace ArcadeDodger;

public enum WeaponKind { Blaster, Spread, Rapid }
public enum PickupKind { Blaster, Spread, Rapid, Multishot, Pierce, Overdrive }
public sealed class Projectile
{
    public Vector2 Position, Previous, Velocity;
    public float Life = 2;
    public int RemainingHits = 1;
    public HashSet<Enemy> HitEnemies { get; } = new();
}
public sealed class Pickup
{
    public Vector2 Position;
    public PickupKind Kind;
    public float Life = 12;
}
public sealed class CombatModel
{
    private readonly Random random = new(417);
    private float cooldown, pickupTimer;
    private readonly Dictionary<PickupKind, int> collectedPickups = Enum.GetValues<PickupKind>()
        .ToDictionary(kind => kind, _ => 0);
    public IReadOnlyDictionary<PickupKind, int> CollectedPickups { get; }
    public CombatModel()
    {
        CollectedPickups = new System.Collections.ObjectModel.ReadOnlyDictionary<PickupKind, int>(collectedPickups);
    }
    public List<Projectile> Projectiles { get; } = new();
    public List<Pickup> Pickups { get; } = new();
    public List<Vector2> Explosions { get; } = new();
    public WeaponKind Weapon { get; private set; }
    public int Multishot { get; private set; }
    public int Pierce { get; private set; }
    public int Overdrive { get; private set; }
    public int ShotsFired { get; private set; }
    public int Kills { get; private set; }
    public void Reset()
    {
        Projectiles.Clear(); Pickups.Clear(); Explosions.Clear();
        foreach (PickupKind kind in Enum.GetValues<PickupKind>()) collectedPickups[kind] = 0;
        Weapon = WeaponKind.Blaster;
        Multishot = Pierce = Overdrive = ShotsFired = Kills = 0;
        cooldown = 0; pickupTimer = 0;
    }
    public void Update(float dt, Box player, Vector2 aim, bool firing, List<Obstacle> obstacles, List<Enemy>? enemies = null)
    {
        Explosions.Clear();
        if (dt <= 0) return;
        cooldown = Math.Max(0, cooldown - dt);
        pickupTimer += dt;
        Vector2 center = new(player.X + player.Width / 2, player.Y + player.Height / 2);
        if (pickupTimer >= 6)
        {
            pickupTimer -= 6;
            AddPickup(new Vector2(Math.Clamp(center.X + (random.NextSingle() - 0.5f) * 180, 30, GameModel.ArenaWidth - 30),
                Math.Clamp(center.Y - 80, 30, GameModel.ArenaHeight - 30)));
        }
        for (int i = Pickups.Count - 1; i >= 0; i--)
        {
            Pickup p = Pickups[i]; p.Life -= dt;
            if (player.Intersects(new Box(p.Position.X - 14, p.Position.Y - 14, 28, 28)))
            {
                collectedPickups[p.Kind]++;
                switch (p.Kind)
                {
                    case PickupKind.Blaster: Weapon = WeaponKind.Blaster; break;
                    case PickupKind.Spread: Weapon = WeaponKind.Spread; break;
                    case PickupKind.Rapid: Weapon = WeaponKind.Rapid; break;
                    case PickupKind.Multishot: Multishot = Math.Min(3, Multishot + 1); break;
                    case PickupKind.Pierce: Pierce = Math.Min(3, Pierce + 1); break;
                    case PickupKind.Overdrive: Overdrive = Math.Min(3, Overdrive + 1); break;
                }
                Pickups.RemoveAt(i);
            }
            else if (p.Life <= 0) Pickups.RemoveAt(i);
        }
        if (firing && cooldown <= 0 && Projectiles.Count < 240)
        {
            Vector2 direction = aim - center;
            direction = direction.LengthSquared() < 0.001f ? -Vector2.UnitY : Vector2.Normalize(direction);
            int count = (Weapon == WeaponKind.Spread ? 3 : 1) + Multishot * 2;
            for (int i = 0; i < count && Projectiles.Count < 240; i++)
            {
                float angle = (i - (count - 1) / 2f) * 0.13f;
                Vector2 d = new(direction.X * MathF.Cos(angle) - direction.Y * MathF.Sin(angle),
                    direction.X * MathF.Sin(angle) + direction.Y * MathF.Cos(angle));
                Projectiles.Add(new Projectile { Position = center, Previous = center,
                    Velocity = d * (650 + Overdrive * 70), RemainingHits = 1 + Pierce });
            }
            ShotsFired++;
            cooldown = (Weapon == WeaponKind.Rapid ? 0.09f : Weapon == WeaponKind.Spread ? 0.3f : 0.22f) / (1 + Overdrive * 0.25f);
        }
        for (int i = Projectiles.Count - 1; i >= 0; i--)
        {
            Projectile p = Projectiles[i]; p.Previous = p.Position;
            p.Position += p.Velocity * dt; p.Life -= dt;
            // Sort intersections by travel distance so non-piercing shots hit the nearest hazard.
            var hits = obstacles.Select(o => (Obstacle: o, T: HitTime(p.Previous, p.Position, o.Bounds)))
                .Where(h => h.T.HasValue).OrderBy(h => h.T).ToArray();
            foreach (var hit in hits)
            {
                if (p.RemainingHits <= 0) break;
                var b = hit.Obstacle.Bounds;
                obstacles.Remove(hit.Obstacle); Kills++; p.RemainingHits--;
                Vector2 position = new(b.X + b.Width / 2, b.Y + b.Height / 2);
                Explosions.Add(position);
                if (random.NextSingle() < 0.28f) AddPickup(new Vector2(Math.Clamp(position.X, 30, GameModel.ArenaWidth - 30), Math.Clamp(position.Y, 30, GameModel.ArenaHeight - 30)));
            }
            if (enemies != null)
            {
                var alienHits = enemies.Where(e => !p.HitEnemies.Contains(e))
                    .Select(e => (Enemy: e, T: HitTime(p.Previous, p.Position, e.Bounds)))
                    .Where(h => h.T.HasValue).OrderBy(h => h.T).ToArray();
                foreach (var hit in alienHits)
                {
                    if (p.RemainingHits <= 0) break;
                    var enemy = hit.Enemy;
                    p.HitEnemies.Add(enemy); p.RemainingHits--;
                    enemy.Health--; enemy.HitFlash = .12f;
                    if (enemy.Health > 0) continue;
                    enemies.Remove(enemy); Kills++; Explosions.Add(enemy.Position);
                    if (random.NextSingle() < .28f) AddPickup(enemy.Position);
                }
            }
            if (p.RemainingHits <= 0 || p.Life <= 0 || p.Position.X < -30 || p.Position.X > GameModel.ArenaWidth + 30 || p.Position.Y < -30 || p.Position.Y > GameModel.ArenaHeight + 30)
                Projectiles.RemoveAt(i);
        }
    }
    private void AddPickup(Vector2 position)
    {
        if (Pickups.Count < 24) Pickups.Add(new Pickup { Position = position, Kind = (PickupKind)random.Next(6) });
    }
    private static float? HitTime(Vector2 a, Vector2 b, Box box)
    {
        float enter = 0, exit = 1;
        Vector2 d = b - a;
        for (int axis = 0; axis < 2; axis++)
        {
            float start = axis == 0 ? a.X : a.Y, delta = axis == 0 ? d.X : d.Y;
            float min = (axis == 0 ? box.X : box.Y) - 3;
            float max = (axis == 0 ? box.X + box.Width : box.Y + box.Height) + 3;
            if (MathF.Abs(delta) < 0.00001f) { if (start < min || start > max) return null; }
            else
            {
                float t1 = (min - start) / delta, t2 = (max - start) / delta;
                enter = Math.Max(enter, Math.Min(t1, t2)); exit = Math.Min(exit, Math.Max(t1, t2));
                if (enter > exit) return null;
            }
        }
        return enter;
    }
}
