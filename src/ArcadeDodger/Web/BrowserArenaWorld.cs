using System.Numerics;
using ArcadeDodger.Multiplayer;
using Raylib_cs;

namespace ArcadeDodger.Web;

// Presentation only: positions, damage, protection and warnings always come from the server.
// Cosmetic history is bounded by the protocol entity limits and is never fed back into play.
internal sealed partial class BrowserArenaWorld
{
    private static readonly Color Cyan = new(65, 245, 235, 255);
    private static readonly Color Hostile = new(255, 75, 180, 255);
    private static readonly Color Rammer = new(255, 100, 95, 255);
    private static readonly Color MutedMine = new(115, 125, 145, 255);
    private static readonly Color Gold = new(255, 210, 80, 255);
    private static readonly Color Shield = new(70, 150, 240, 255);
    private const int EffectCapacity = 160;
    private readonly List<Effect> effects = new(EffectCapacity);
    private const float EnemyHitFlashDuration = .12f;
    private readonly Dictionary<int, float> enemyHitFlashes = new();
    private readonly Dictionary<int, PlayerSnapshot> players = new();
    private readonly Dictionary<int, EnemySnapshot> enemies = new();
    private readonly HashSet<int> shots = new();
    private long tick = -1;
    private bool shotTransitions;
    private float lastTime;
    private int lastCombatEvent;
    private Vector2 view = new(960, 540);

    private enum EffectKind
    {
        Trail,
        Muzzle,
        Hit,
        Death,
    }

    private readonly record struct Effect(
        Vector2 Position,
        Vector2 Direction,
        Color Color,
        float Born,
        float Duration,
        EffectKind Kind,
        int Seed,
        float Scale = 1
    );

    public void Draw(
        RoomSnapshot? snapshot,
        int ownId,
        Vector2 origin,
        Vector2 viewport,
        float time,
        bool reducedEffects
    )
    {
        view = viewport;
        reducedEffects |=
            (snapshot?.Shots.Length ?? 0) > 180 || (snapshot?.Beams?.Length ?? 0) > 400;
        if (time < lastTime || snapshot is null || (snapshot is not null && snapshot.Tick < tick))
            Reset();
        lastTime = time;
        effects.RemoveAll(e => time - e.Born >= e.Duration);
        // Bound flash history to live transition IDs and expire it even on repeated ticks.
        if (reducedEffects)
            enemyHitFlashes.Clear();
        else
            foreach (
                var id in enemyHitFlashes
                    .Where(pair => time >= pair.Value)
                    .Select(pair => pair.Key)
                    .ToArray()
            )
                enemyHitFlashes.Remove(id);
        // Static backdrop animation in low FX mode, but retain the native grid and boundary.
        NeonRendering.DrawBackdrop(
            reducedEffects ? 0 : time,
            origin,
            (int)Room.Width,
            (int)Room.Height,
            view
        );
        if (snapshot is null)
            return;
        DrawSector(snapshot, origin, time, reducedEffects);
        DrawLivingSector(snapshot, origin, ownId);
        if (snapshot.Tick > tick)
            Observe(snapshot, ownId, origin, time, reducedEffects);
        ObserveShots(snapshot, ownId, origin, time, reducedEffects);
        ObserveCombat(snapshot, origin, time);

        DrawEffects(origin, time, reducedEffects);
        foreach (var drop in snapshot.Energy ?? [])
        {
            Vector2 p = new(drop.X, drop.Y);
            if (!Visible(p, origin, 45))
                continue;
            float radius = reducedEffects ? 14 : 14 + MathF.Sin(time * 5 + drop.Id) * 2;
            if (!reducedEffects)
                Raylib.DrawCircleV(p, radius + 5, Raylib.Fade(Cyan, .10f));
            Raylib.DrawCircleLines((int)p.X, (int)p.Y, radius, Cyan);
            Raylib.DrawPoly(p, 4, 7, 0, Raylib.Fade(Cyan, .65f));
            Raylib.DrawPolyLinesEx(p, 4, 7, 0, 2, Cyan);
            Raylib.DrawCircleV(p, 2, NeonRendering.Ink);
            BrowserText.DrawText("ENERGY", (int)p.X - 18, (int)p.Y - 30, 10, Cyan);
        }
        foreach (var credit in snapshot.Credits ?? [])
        {
            Vector2 p = new(credit.X, credit.Y);
            if (!Visible(p, origin, 30))
                continue;
            Raylib.DrawPoly(p, 6, 9, time * 30, Raylib.Fade(Gold, .3f));
            Raylib.DrawPolyLinesEx(p, 6, 9, time * 30, 2, Gold);
            BrowserText.DrawText("$", (int)p.X - 4, (int)p.Y - 5, 10, Gold);
        }
        foreach (var module in snapshot.ModuleDrops ?? [])
        {
            Vector2 pos = new(module.X, module.Y);
            if (!Visible(pos, origin, 50))
                continue;
            Raylib.DrawRectangle(
                (int)pos.X - 12,
                (int)pos.Y - 12,
                24,
                24,
                new Color(25, 35, 48, 255)
            );
            Raylib.DrawRectangleLines((int)pos.X - 12, (int)pos.Y - 12, 24, 24, Gold);
            BrowserText.DrawText(
                module.Kind == "weapon" ? "WPN"
                    : module.Kind == "gear" ? "SYS"
                    : "MOD",
                (int)pos.X - 10,
                (int)pos.Y - 4,
                10,
                Gold
            );
        }
        foreach (var mine in snapshot.Mines ?? [])
        {
            Vector2 p = new(mine.X, mine.Y);
            if (!Visible(p, origin, 30))
                continue;
            var c = mine.Armed ? Gold : MutedMine;
            Raylib.DrawPoly(p, 4, 8, 45, c);
            Raylib.DrawCircleLines((int)p.X, (int)p.Y, mine.Armed ? 14 : 10, c);
        }
        foreach (var enemy in snapshot.Enemies ?? [])
            if (enemy.Kind != "installation")
                DrawEnemy(enemy, origin, time, reducedEffects);
        foreach (var player in snapshot.Players)
            DrawPlayer(
                player,
                ownId,
                origin,
                time,
                reducedEffects,
                snapshot.Social?.Friends.Any(n => FriendBook.Key(n) == FriendBook.Key(player.Name))
                    == true
            );
        DrawWeapons(snapshot, origin, time, reducedEffects);
        foreach (var blast in snapshot.Explosions ?? [])
        {
            var age = Math.Clamp((snapshot.Tick - blast.Born) / 10f, 0, 1);
            if (!Visible(new(blast.X, blast.Y), origin, blast.Radius))
                continue;
            Raylib.DrawCircleLines(
                (int)blast.X,
                (int)blast.Y,
                blast.Radius * (.3f + age * .7f),
                Raylib.Fade(Gold, 1 - age)
            );
            if (!reducedEffects)
                Raylib.DrawCircleV(
                    new(blast.X, blast.Y),
                    blast.Radius * .7f,
                    Raylib.Fade(Gold, (1 - age) * .12f)
                );
        }
        DrawThreatWarnings(snapshot, ownId, origin);
    }

    private static Color EnemyColor(EnemySnapshot e) =>
        e.Faction == "friendly" ? Cyan
        : e.Faction == "neutral" ? Gold
        : e.Kind switch
        {
            "gunner" => Hostile,
            "interceptor" => Gold,
            "sniper" => new Color(195, 120, 255, 255),
            "tender" => new Color(100, 240, 160, 255),
            _ => Rammer,
        };

    private static Vector2 Direction(Vector2 d) =>
        d.LengthSquared() < .001f ? -Vector2.UnitY : Vector2.Normalize(d);

    private bool Visible(Vector2 p, Vector2 origin, float margin) =>
        p.X >= origin.X - margin
        && p.X <= origin.X + view.X + margin
        && p.Y >= origin.Y - margin
        && p.Y <= origin.Y + view.Y + margin;

    private bool SegmentVisible(Vector2 a, Vector2 b, Vector2 origin) =>
        Math.Max(a.X, b.X) >= origin.X
        && Math.Min(a.X, b.X) <= origin.X + view.X
        && Math.Max(a.Y, b.Y) >= origin.Y
        && Math.Min(a.Y, b.Y) <= origin.Y + view.Y;
}
