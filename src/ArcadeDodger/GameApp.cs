using System.Numerics;
using Raylib_cs;

namespace ArcadeDodger;

public sealed partial class GameApp
{
    private readonly GameModel model = new();
    private static readonly Color Background = new(6, 8, 23, 255);
    private static readonly Color PlayerColor = new(65, 245, 235, 255);
    private static readonly Color ObstacleColor = new(255, 65, 130, 255);
    private static readonly Color Ink = new(235, 240, 250, 255);
    // Slim cyan screen-space HUD; world-space threat colors remain unchanged.
    private static readonly Color ConsoleFill = new(4, 14, 22, 238);
    private static readonly Color ConsoleBorder = new(40, 112, 132, 255);
    private static readonly Color ConsoleCyan = new(65, 245, 235, 255);
    private static readonly Color ConsoleAmber = new(255, 210, 80, 255);
    private static readonly Color ConsoleMuted = new(184, 218, 228, 255);
    private readonly List<Vector2> trail = new();
    private readonly List<Spark> sparks = new();
    private readonly Random effectsRandom = new(73);
    private float clock;
    private float impact;
    private Vector2 impactPosition;
    private Vector2 aim, shake;
    private float muzzle, shakeLife;
    private bool reducedEffects;
    private Vector2 cameraOrigin;
    private readonly List<Spark> rings = new();
    private record struct Spark(Vector2 Position, Vector2 Velocity, float Life);

    private bool smokeTest;
    private int frames;

    public void Run(bool smokeTest = false)
    {
        try
        {
            Initialize(smokeTest);
            while (!Raylib.WindowShouldClose())
                if (!UpdateFrame()) break;
        }
        finally { Close(); }
    }

    // Browser host calls this once, then yields to JavaScript between frames.
    public void Initialize(bool smokeTest = false)
    {
        this.smokeTest = smokeTest;
        frames = 0;
        Raylib.InitWindow(1920, 1080, "Arcade Dodger | NEON OVERDRIVE");
            if (!Raylib.IsWindowReady()) throw new InvalidOperationException("Raylib window initialization failed.");
            // The web backend adopts the CSS display size during initialization,
            // but our cameras render a fixed 1920 x 1080 surface. Restore both
            // the backing buffer and GL viewport; CSS alone handles display scaling.
            if (OperatingSystem.IsBrowser()) Raylib.SetWindowSize(1920, 1080);

            // EndDrawing must not busy-wait on the browser's UI thread.
            Raylib.SetTargetFPS(OperatingSystem.IsBrowser() ? 0 : 60);
            if (smokeTest)
            {
                model.Start();
                Vector2 center = new(model.Player.X + 14, model.Player.Y + 14);
                model.Combat.Pickups.Add(new Pickup { Kind = PickupKind.Spread, Position = center });
                model.Combat.Pickups.Add(new Pickup { Kind = PickupKind.Multishot, Position = center });
                model.Combat.Pickups.Add(new Pickup { Kind = PickupKind.Pierce, Position = center });
                model.Arena.Enemies.Add(new Enemy { Kind = EnemyKind.Rammer, Position = center - new Vector2(0,100), Health = 1 });
                model.Arena.Enemies.Add(new Enemy { Kind = EnemyKind.Rammer, Position = center - new Vector2(0,200), Health = 1 });
                model.Arena.Enemies.Add(new Enemy { Kind = EnemyKind.Gunner, Position = center + new Vector2(550,0), Health = 3, AttackCooldown = 0 });
                model.Arena.Enemies.Add(new Enemy { Kind = EnemyKind.Spinner, Position = center - new Vector2(550,0), Health = 4, AttackCooldown = 0 });
            }
    }

    public void Close() => Raylib.CloseWindow();

    // Exactly one input/update/draw pass; no blocking window loop.
    public bool UpdateFrame()
    {
                if (Raylib.IsKeyPressed(KeyboardKey.Enter) &&
                    model.State is GameState.Title or GameState.GameOver)
                {
                    model.Start();
                    trail.Clear();
                    sparks.Clear();
                    impact = 0; rings.Clear(); muzzle = shakeLife = 0; shake = Vector2.Zero;
                }
                if (Raylib.IsKeyPressed(KeyboardKey.P)) model.TogglePause();
                if (Raylib.IsKeyPressed(KeyboardKey.F)) { reducedEffects = !reducedEffects; shake = Vector2.Zero; }
                float x = (Down(KeyboardKey.D, KeyboardKey.Right) ? 1 : 0) -
                          (Down(KeyboardKey.A, KeyboardKey.Left) ? 1 : 0);
                float y = (Down(KeyboardKey.S, KeyboardKey.Down) ? 1 : 0) -
                          (Down(KeyboardKey.W, KeyboardKey.Up) ? 1 : 0);
                float dt = Math.Clamp(Raylib.GetFrameTime(), 0, GameModel.MaxDelta);
                GameState previous = model.State;
                cameraOrigin = ArenaView.CameraOrigin(model.Player);
                aim = ArenaView.ScreenToWorld(Raylib.GetMousePosition(), cameraOrigin, shake);
                if (smokeTest) aim = new Vector2(model.Player.X + 14, 0);
                int shots = model.Combat.ShotsFired;
                model.Update(dt, x, y, smokeTest || Raylib.IsKeyDown(KeyboardKey.Space) ||
                    Raylib.IsMouseButtonDown(MouseButton.Left), aim);
                if (model.Combat.ShotsFired > shots) muzzle = 0.075f;
                UpdateEffects(dt, previous);
                Raylib.BeginDrawing();
                try
                {
                    // Double the rendering scale without changing model speed or difficulty.
                    cameraOrigin = ArenaView.CameraOrigin(model.Player);
                    Raylib.BeginMode2D(new Camera2D { Zoom = 2f, Target = cameraOrigin, Offset = shake });
                    try { Draw(); }
                    finally { Raylib.EndMode2D(); }
                    Raylib.BeginMode2D(new Camera2D { Zoom = 2f });
                    try { DrawHud(); }
                    finally { Raylib.EndMode2D(); }
                }
                finally { Raylib.EndDrawing(); }
                if (smokeTest && ++frames >= 120)
                {
                    if (model.Combat.ShotsFired == 0 || model.Combat.Kills < 2 || model.Arena.HostileShotsFired == 0)
                        throw new InvalidOperationException("Arena smoke did not exercise player kills and hostile firing.");
                    Console.WriteLine($"SMOKE_OK: {Raylib.GetScreenWidth()}x{Raylib.GetScreenHeight()}, rendered 120 frames; shots={model.Combat.ShotsFired}, kills={model.Combat.Kills}, hostileShots={model.Arena.HostileShotsFired}, closing cleanly.");
                    return false;
                }
        return true;
    }

    private static bool Down(KeyboardKey first, KeyboardKey second) =>
        Raylib.IsKeyDown(first) || Raylib.IsKeyDown(second);

    private void UpdateEffects(float dt, GameState previous)
    {
        clock += dt;
        if (model.State == GameState.Paused) return;
        impact = Math.Max(0, impact - dt);
        AdvanceCombatEffects(dt, previous);
        if (model.State == GameState.Playing)
        {
            trail.Add(new Vector2(model.Player.X, model.Player.Y));
            if (trail.Count > (reducedEffects ? 6 : 18)) trail.RemoveAt(0);
        }
        if (previous == GameState.Playing && model.State == GameState.GameOver)
        {
            impact = 0.8f;
            impactPosition = new Vector2(model.Player.X + 14, model.Player.Y + 14);
            for (int i = 0; i < (reducedEffects ? 14 : 70) && sparks.Count < 800; i++)
            {
                float angle = effectsRandom.NextSingle() * MathF.Tau;
                float speed = 70 + effectsRandom.NextSingle() * 210;
                sparks.Add(new Spark(impactPosition,
                    new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed, 1f));
            }
        }
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            Spark spark = sparks[i];
            spark = spark with { Position = spark.Position + spark.Velocity * dt, Life = spark.Life - dt };
            if (spark.Life <= 0) sparks.RemoveAt(i);
            else sparks[i] = spark;
        }
    }

    private void DrawBackdrop()
    {
        for (int i = 0; i < 600; i++)
        {
            float x = (i * 137 + 31) % GameModel.ArenaWidth;
            float y = (i * 83 + clock * (8 + i % 4 * 7)) % GameModel.ArenaHeight;
            if (!ArenaView.Visible(new(x, y), cameraOrigin)) continue;
            float brightness = 0.25f + 0.3f * (1 + MathF.Sin(clock * 2 + i));
            Raylib.DrawCircleV(new Vector2(x, y), i % 5 == 0 ? 1.5f : 0.7f,
                Raylib.Fade(PlayerColor, brightness));
        }
        for (int x = 0; x <= GameModel.ArenaWidth; x += 60)
            Raylib.DrawLine(x, 0, x, GameModel.ArenaHeight, new Color(19, 30, 55, 255));
        for (float y = 0; y < GameModel.ArenaHeight; y += 60)
            Raylib.DrawLine(0, (int)y, GameModel.ArenaWidth, (int)y, new Color(24, 34, 62, 255));
        Raylib.DrawRectangleGradientV(0, 0, GameModel.ArenaWidth, GameModel.ArenaHeight,
            new Color(30, 10, 65, 45), new Color(0, 180, 190, 18));
        Raylib.DrawRectangleLinesEx(new Rectangle(2, 2, GameModel.ArenaWidth - 4, GameModel.ArenaHeight - 4), 3, PlayerColor);
    }

    private void Draw()
    {
        Raylib.ClearBackground(Background);
        DrawBackdrop();
        for (int i = 0; i < trail.Count; i++)
        {
            float alpha = (i + 1f) / trail.Count * 0.22f;
            Raylib.DrawRectangleV(trail[i], new Vector2(28, 28), Raylib.Fade(PlayerColor, alpha));
        }
        foreach (var obstacle in model.Obstacles) DrawBox(obstacle.Bounds, ObstacleColor);
        DrawBox(model.Player, PlayerColor);
        DrawCombat();
        Raylib.DrawRectangleRec(new Rectangle(model.Player.X + 8, model.Player.Y + 8, 12, 12), Ink);
        Raylib.DrawCircleV(new Vector2(model.Player.X + 14, model.Player.Y + 35),
            3 + MathF.Sin(clock * 24), PlayerColor);
        // HUD is drawn in an unshaken screen-space pass.
        foreach (Spark spark in sparks)
            Raylib.DrawCircleV(spark.Position, 2 + spark.Life * 2, Raylib.Fade(ObstacleColor, spark.Life));
        if (impact > 0)
        {
            Raylib.DrawCircleLines((int)impactPosition.X, (int)impactPosition.Y,
                20 + (0.8f - impact) * 250, Raylib.Fade(ObstacleColor, impact));
        }
        // Lethal bullets and warnings stay above cosmetic particles.
        DrawAliens();
    }

    private void DrawHud()
    {
        DrawConsolePanel(12, 8, 936, 28, ConsoleCyan);
        Raylib.DrawText("SURVIVAL", 24, 11, 10, ConsoleMuted);
        Raylib.DrawText($"{model.Score:0.0}s", 24, 22, 14, Ink);
        Raylib.DrawLine(384, 14, 384, 30, ConsoleBorder);
        Center("NEON OVERDRIVE", 16, 16, ConsoleCyan);
        Raylib.DrawLine(576, 14, 576, 30, ConsoleBorder);
        Raylib.DrawText("KILLS", 595, 19, 10, ConsoleMuted);
        Raylib.DrawText(model.Combat.Kills.ToString(), 640, 16, 16, Ink);
        Raylib.DrawLine(810, 14, 810, 30, ConsoleBorder);
        Raylib.DrawText("BEST SURVIVAL", 830, 11, 10, ConsoleMuted);
        Raylib.DrawText($"{model.BestScore:0.0}s", 830, 22, 14, Ink);
        DrawConsolePanel(12, 480, 936, 52, ConsoleCyan);
        DrawLoadout();
        DrawRadar(cameraOrigin);
        if (model.State == GameState.Playing) return;

        Raylib.DrawRectangle(0, 38, GameModel.Width, 440, new Color(4, 12, 22, 180));
        DrawConsolePanel(230, 156, 500, 228, ConsoleCyan);
        string heading = model.State switch
        {
            GameState.Title => "NEON OVERDRIVE",
            GameState.Paused => "PAUSED",
            _ => "GAME OVER"
        };
        Center(heading, 180, 28, ConsoleCyan);
        Raylib.DrawLine(254, 222, 706, 222, ConsoleBorder);
        if (model.State == GameState.Title)
        {
            Center("Blast aliens. Recover weapons. Survive.", 240, 16, Ink);
            Center("ONE HIT ENDS THE RUN", 266, 12, ConsoleMuted);
        }
        else
        {
            Center($"SURVIVAL {model.Score:0.0}s   /   BEST {model.BestScore:0.0}s", 244, 18, Ink);
            Center(model.State == GameState.Paused ? "Take a breath. Your run is on hold." : $"{model.Combat.Kills} KILLS  /  Try again. Stay alive.",
                270, 14, ConsoleMuted);
        }
        Center(model.State == GameState.Paused ? "[ P ] RESUME" :
            model.State == GameState.Title ? "[ ENTER ] START" : "[ ENTER ] RESTART", 306, 16, ConsoleCyan);
        Center("WASD/ARROWS move  /  MOUSE aim  /  SPACE/LMB fire", 342, 12, ConsoleMuted);
        Center($"P pause  /  F effects: {(reducedEffects ? "LOW" : "FULL")}  /  ESC exit", 364, 10, ConsoleMuted);
    }

    // Shared by the three HUD partials; all coordinates are logical 960 x 540.
    private static void DrawConsolePanel(int x, int y, int width, int height, Color accent)
    {
        Raylib.DrawRectangle(x, y, width, height, ConsoleFill);
        Raylib.DrawRectangleLinesEx(new Rectangle(x, y, width, height), 1, ConsoleBorder);
        const int corner = 7;
        Raylib.DrawLine(x, y, x + corner, y, accent);
        Raylib.DrawLine(x, y, x, y + corner, accent);
        Raylib.DrawLine(x + width - corner, y, x + width - 1, y, accent);
        Raylib.DrawLine(x + width - 1, y, x + width - 1, y + corner, accent);
        Raylib.DrawLine(x, y + height - 1, x + corner, y + height - 1, accent);
        Raylib.DrawLine(x, y + height - corner, x, y + height - 1, accent);
        Raylib.DrawLine(x + width - corner, y + height - 1, x + width - 1, y + height - 1, accent);
        Raylib.DrawLine(x + width - 1, y + height - corner, x + width - 1, y + height - 1, accent);
    }

    private static void DrawBox(Box box, Color color) =>
        GlowBox(box, color);

    private static void GlowBox(Box box, Color color)
    {
        for (int padding = 9; padding >= 3; padding -= 3)
            Raylib.DrawRectangleRec(new Rectangle(box.X - padding, box.Y - padding,
                box.Width + padding * 2, box.Height + padding * 2), Raylib.Fade(color, 0.055f));
        Raylib.DrawRectangleRec(new Rectangle(box.X, box.Y, box.Width, box.Height), Raylib.Fade(color, 0.65f));
        Raylib.DrawRectangleLinesEx(new Rectangle(box.X, box.Y, box.Width, box.Height), 2, color);
        Raylib.DrawLine((int)box.X + 4, (int)box.Y + 4,
            (int)(box.X + box.Width - 4), (int)box.Y + 4, Ink);
    }

    private static void Center(string text, int y, int size, Color color) =>
        Raylib.DrawText(text, (GameModel.Width - Raylib.MeasureText(text, size)) / 2, y, size, color);
}
