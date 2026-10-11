using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed record EnemySnapshot(int Id, string Kind, float X, float Y, float Aim, int Hull, bool Telegraph);

public sealed partial class Room
{
    public const int EnemyCapacity = 24;
    private readonly List<Enemy> enemies = new();
    private int nextEnemy;

    private void StepEnemies()
    {
        var alive = players.Values.Where(p => p.Hull > 0).ToArray();
        if (alive.Length == 0) return;
        if (Tick % 90 == 0 && enemies.Count < EnemyCapacity)
        {
            var anchor = alive[(nextEnemy / 2) % alive.Length];
            var angle = Tick / 90 * 2.399963f;
            var position = Vector2.Clamp(anchor.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 460,
                new Vector2(50), new Vector2(Width - 50, Height - 50));
            if (alive.All(p => Vector2.DistanceSquared(p.Position, position) > 300 * 300))
                enemies.Add(new Enemy { Id = ++nextEnemy, Gunner = nextEnemy % 2 == 0,
                    Position = position, Previous = position, FireAt = Tick + 60 });
        }
        foreach (var enemy in enemies)
        {
            enemy.Previous = enemy.Position;
            var target = alive.Where(p => p.Hull > 0).OrderBy(p => Vector2.DistanceSquared(p.Position, enemy.Position)).FirstOrDefault();
            if (target is null) break;
            var delta = target.Position - enemy.Position;
            var distance = delta.Length();
            var direction = distance > 0.001f ? delta / distance : Vector2.UnitX;
            enemy.Aim = MathF.Atan2(direction.Y, direction.X);
            var movement = enemy.Gunner ? (distance > 320 ? 1 : distance < 220 ? -1 : 0) : 1;
            enemy.Position = Vector2.Clamp(enemy.Position + direction * (enemy.Gunner ? 100 : 145) / TickRate * movement,
                new Vector2(20), new Vector2(Width - 20, Height - 20));
            if (enemy.Gunner && distance < 650 && Tick >= enemy.FireAt)
            {
                if (shots.Count < 256)
                    shots.Add(new Shot { Id = ++nextShot, Owner = -enemy.Id, Position = enemy.Position,
                        Velocity = direction * (280f / TickRate), Born = Tick, Expires = Tick + 90 });
                enemy.FireAt = Tick + 75;
            }
            if (!enemy.Gunner && Tick >= enemy.FireAt && Tick >= target.ProtectedUntil &&
                Intersection(enemy.Previous - target.Previous, enemy.Position - target.Position, Radius + 20).HasValue)
            {
                Damage(target);
                enemy.FireAt = Tick + 30;
            }
        }
    }

    private sealed class Enemy
    {
        public int Id, Hull = 3;
        public bool Gunner;
        public Vector2 Position, Previous;
        public float Aim;
        public long FireAt;
    }
}
