using System;
using System.Collections.Generic;
using System.Numerics;

namespace ArcadeDodger
{
    public enum GameState
    {
        Title,
        Playing,
        Paused,
        GameOver
    }

    public readonly record struct Box(float X, float Y, float Width, float Height)
    {
        public bool Intersects(Box other)
        {
            return X < other.X + other.Width
                && other.X < X + Width
                && Y < other.Y + other.Height
                && other.Y < Y + Height;
        }
    }

    public sealed class Obstacle
    {
        public Box Bounds { get; set; }
        public float Speed { get; set; }
    }

    public sealed class GameModel
    {
        public const int Width = 960;
        public const int Height = 540;
        public const int ArenaWidth = 2880;
        public const int ArenaHeight = 1620;
        public const float PlayerSize = 28f;
        public const float PlayerSpeed = 300f;
        public const float MaxDelta = 0.05f;

        // Obstacle size range (random).
        private const float MinObstacleWidth = 40f;
        private const float MaxObstacleWidth = 120f;
        private const float MinObstacleHeight = 30f;
        private const float MaxObstacleHeight = 60f;

        // Difficulty tuning: derived from Score, clamped to these bounds.
        private const float BaseFallSpeed = 180f;
        private const float MinFallSpeed = 150f;
        private const float MaxFallSpeed = 480f;
        private const float FallSpeedPerScore = 2f;

        private const float BaseSpawnInterval = 1.4f;
        private const float MaxSpawnInterval = BaseSpawnInterval;
        private const float MinSpawnInterval = 0.4f;
        private const float SpawnIntervalPerScore = 0.01f;

        private readonly Random _random;

        public GameModel(Random? random = null)
        {
            _random = random ?? new Random();
            Arena = new ArenaModel(_random);
        }

        public GameState State { get; private set; } = GameState.Title;

        public Box Player { get; private set; } =
            new Box((ArenaWidth - PlayerSize) / 2f, (ArenaHeight - PlayerSize) / 2f, PlayerSize, PlayerSize);

        public List<Obstacle> Obstacles { get; } = new();
        public CombatModel Combat { get; } = new();
        public ArenaModel Arena { get; }

        public float Score { get; private set; }

        public float BestScore { get; private set; }

        public float FallSpeed
        {
            get
            {
                float speed = BaseFallSpeed + Score * FallSpeedPerScore;
                return Math.Clamp(speed, MinFallSpeed, MaxFallSpeed);
            }
        }

        public float SpawnInterval
        {
            get
            {
                float interval = BaseSpawnInterval - Score * SpawnIntervalPerScore;
                return Math.Clamp(interval, MinSpawnInterval, MaxSpawnInterval);
            }
        }

        public void Start()
        {
            Score = 0f;
            Obstacles.Clear();
            Combat.Reset();
            Arena.Reset();
            Player = new Box((ArenaWidth - PlayerSize) / 2f, (ArenaHeight - PlayerSize) / 2f, PlayerSize, PlayerSize);
            State = GameState.Playing;
        }

        public void TogglePause()
        {
            if (State == GameState.Playing)
            {
                State = GameState.Paused;
            }
            else if (State == GameState.Paused)
            {
                State = GameState.Playing;
            }
        }

        public void Update(float delta, float horizontal, float vertical) =>
            Update(delta, horizontal, vertical, false, Vector2.Zero);

        public void Update(float delta, float horizontal, float vertical, bool firing, Vector2 aim)
        {
            if (State != GameState.Playing)
            {
                return;
            }

            float dt = Math.Clamp(delta, 0f, MaxDelta);
            if (dt == 0f)
            {
                return;
            }

            float inputX = horizontal;
            float inputY = vertical;
            float magnitude = MathF.Sqrt(inputX * inputX + inputY * inputY);
            if (magnitude > 1f)
            {
                inputX /= magnitude;
                inputY /= magnitude;
            }

            Score += dt;

            // Alien director replaces falling-block spawning.

            for (int i = 0; i < Obstacles.Count; i++)
            {
                Obstacle obstacle = Obstacles[i];
                obstacle.Bounds = obstacle.Bounds with { Y = obstacle.Bounds.Y + obstacle.Speed * dt };
            }

            Box previousPlayer = Player;
            Player = new Box(
                Math.Clamp(Player.X + inputX * PlayerSpeed * dt, 0f, ArenaWidth - PlayerSize),
                Math.Clamp(Player.Y + inputY * PlayerSpeed * dt, 0f, ArenaHeight - PlayerSize),
                PlayerSize,
                PlayerSize);

            RemoveOffscreenObstacles();
            Combat.Update(dt, Player, aim, firing, Obstacles, Arena.Enemies);
            if (Arena.Update(dt, Player, previousPlayer, Score))
            {
                State = GameState.GameOver;
                BestScore = Math.Max(BestScore, Score);
                return;
            }

            for (int i = 0; i < Obstacles.Count; i++)
            {
                if (Player.Intersects(Obstacles[i].Bounds))
                {
                    State = GameState.GameOver;
                    if (Score > BestScore)
                    {
                        BestScore = Score;
                    }
                    return;
                }
            }
        }

        private void SpawnObstacle()
        {
            float width = NextFloat(MinObstacleWidth, MaxObstacleWidth);
            float height = NextFloat(MinObstacleHeight, MaxObstacleHeight);
            float x = _random.NextSingle() * (Width - width);

            Obstacles.Add(new Obstacle
            {
                Bounds = new Box(x, -height, width, height),
                Speed = FallSpeed,
            });
        }

        private void RemoveOffscreenObstacles()
        {
            int i = 0;
            while (i < Obstacles.Count)
            {
                if (Obstacles[i].Bounds.Y > ArenaHeight)
                {
                    Obstacles.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }
        }

        private float NextFloat(float min, float max)
        {
            return min + _random.NextSingle() * (max - min);
        }
    }
}
