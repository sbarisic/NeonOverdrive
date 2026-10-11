using System.Numerics;
using ArcadeDodger.Multiplayer;
using Raylib_cs;

namespace ArcadeDodger.Web;

// Presentation only: positions, damage, protection and warnings always come from the server.
// Cosmetic history is bounded by the protocol entity limits and is never fed back into play.
internal sealed class BrowserArenaWorld
{
    private static readonly Color Cyan = new(65, 245, 235, 255);
    private static readonly Color Rival = new(255, 65, 130, 255);
    private static readonly Color Hostile = new(255, 75, 180, 255);
    private static readonly Color Rammer = new(255, 100, 95, 255);
    private static readonly Color MutedMine = new(115,125,145,255);
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
    private Vector2 view = new(960, 540);
    private enum EffectKind { Trail, Muzzle, Hit, Death }
    private readonly record struct Effect(Vector2 Position, Vector2 Direction, Color Color,
        float Born, float Duration, EffectKind Kind, int Seed);

    public void Draw(RoomSnapshot? snapshot, int ownId, Vector2 origin, Vector2 viewport, float time, bool reducedEffects)
    {
        view = viewport;
        if (time < lastTime || snapshot is null || (snapshot is not null && snapshot.Tick < tick)) Reset();
        lastTime = time;
        effects.RemoveAll(e => time - e.Born >= e.Duration);
        // Bound flash history to live transition IDs and expire it even on repeated ticks.
        if (reducedEffects) enemyHitFlashes.Clear();
        else
            foreach (var id in enemyHitFlashes.Where(pair => time >= pair.Value).Select(pair => pair.Key).ToArray())
                enemyHitFlashes.Remove(id);
        // Static backdrop animation in low FX mode, but retain the native grid and boundary.
        NeonRendering.DrawBackdrop(reducedEffects ? 0 : time, origin, (int)Room.Width, (int)Room.Height, view);
        if (snapshot is null) return;
        if (snapshot.Tick > tick) Observe(snapshot, ownId, origin, time, reducedEffects);
        ObserveShots(snapshot, ownId, origin, time, reducedEffects);

        DrawEffects(origin, time, reducedEffects);
        foreach (var drop in snapshot.Energy ?? [])
        {
            Vector2 p = new(drop.X, drop.Y);
            if (!Visible(p, origin, 45)) continue;
            float radius = reducedEffects ? 14 : 14 + MathF.Sin(time * 5 + drop.Id) * 2;
            if (!reducedEffects) Raylib.DrawCircleV(p, radius + 5, Raylib.Fade(Cyan, .10f));
            Raylib.DrawCircleLines((int)p.X, (int)p.Y, radius, Cyan);
            Raylib.DrawPoly(p, 4, 7, 0, Raylib.Fade(Cyan, .65f));
            Raylib.DrawPolyLinesEx(p, 4, 7, 0, 2, Cyan);
            Raylib.DrawCircleV(p, 2, NeonRendering.Ink);
            Raylib.DrawText("ENERGY", (int)p.X - 18, (int)p.Y - 30, 10, Cyan);
        }
        foreach(var credit in snapshot.Credits ?? [])
        {
            Vector2 p=new(credit.X,credit.Y);if(!Visible(p,origin,30))continue;
            Raylib.DrawPoly(p,6,9,time*30,Raylib.Fade(Gold,.3f));Raylib.DrawPolyLinesEx(p,6,9,time*30,2,Gold);
            Raylib.DrawText("$",(int)p.X-4,(int)p.Y-5,10,Gold);
        }
        foreach(var mine in snapshot.Mines ?? [])
        {
            Vector2 p=new(mine.X,mine.Y);if(!Visible(p,origin,30))continue;
            var c=mine.Armed?Gold:MutedMine;
            Raylib.DrawPoly(p,4,8,45,c);Raylib.DrawCircleLines((int)p.X,(int)p.Y,mine.Armed?14:10,c);
        }
        foreach (var enemy in snapshot.Enemies ?? []) DrawEnemy(enemy, origin, time, reducedEffects);
        foreach (var player in snapshot.Players) DrawPlayer(player, ownId, origin, time, reducedEffects, snapshot.Social?.Friends.Any(n => FriendBook.Key(n) == FriendBook.Key(player.Name)) == true);
        foreach (var shot in snapshot.Shots)
        {
            Vector2 p = new(shot.X, shot.Y);
            if (!Visible(p, origin, 35)) continue;
            var size=Math.Clamp(MathF.Sqrt(shot.Power),.65f,4);
            if(size>1)Raylib.DrawCircleV(p,5*size,Raylib.Fade(ProjectileColor(shot),.5f));
            Color color = shot.Owner < 0 ? Hostile : ProjectileColor(shot);
            if (shot.Owner < 0)
            {
                // Alien rounds have a solid circular silhouette, unlike pilot tracer rounds.
                Raylib.DrawCircleV(p, 7, new Color(30, 0, 28, 255));
                Raylib.DrawCircleV(p, 5, Hostile);
                Raylib.DrawCircleV(p, 2, NeonRendering.Ink);
            }
            else if (shot.Kind == 2)
            {
                var direction = Direction(new(shot.Dx, shot.Dy));
                Raylib.DrawLineEx(p - direction * 42, p, reducedEffects ? 3 : 7, Raylib.Fade(color, .35f));
                Raylib.DrawLineEx(p - direction * 38, p, 2, color);
            }
            else if (shot.Kind == 3)
            {
                var direction = Direction(new(shot.Dx, shot.Dy));
                Raylib.DrawLineEx(p - direction * 22, p, 5, Gold);
                Raylib.DrawPoly(p, 3, 7, MathF.Atan2(shot.Dy, shot.Dx) * 180 / MathF.PI, color);
            }
            else if (shot.Kind == 4)
            {
                Raylib.DrawCircleV(p, 11, Raylib.Fade(color, .25f));
                Raylib.DrawCircleV(p, 6, color);
                Raylib.DrawCircleV(p, 2, NeonRendering.Ink);
            }
            else if (!reducedEffects) NeonRendering.DrawProjectile(p, new(shot.Dx, shot.Dy), color);
            else
            {
                Vector2 d = Direction(new(shot.Dx, shot.Dy));
                Raylib.DrawLineEx(p - d * 12, p, 3, color);
                Raylib.DrawCircleV(p, 2, NeonRendering.Ink);
            }
        }
        foreach (var beam in snapshot.Beams ?? [])
        {
            var owner = snapshot.Players.FirstOrDefault(p => p.Id == beam.Owner);
            if (owner is null || owner.Hull <= 0 || owner.Editing) continue;
            Vector2 start = new(beam.X, beam.Y), end = new(beam.EndX, beam.EndY);
            if (!SegmentVisible(start, end, origin)) continue;
            var color = ProjectileColor(new(0, beam.Owner, beam.X, beam.Y, Kind: 2, Effects: beam.Effects));
            var thickness=Math.Clamp(MathF.Sqrt(beam.Power),.5f,5);
            if (!reducedEffects) Raylib.DrawLineEx(start, end, 11*thickness, Raylib.Fade(color, .15f));
            Raylib.DrawLineEx(start, end, (reducedEffects ? 3 : 5)*thickness, Raylib.Fade(color, .8f));
            Raylib.DrawLineEx(start, end, 1.5f*thickness, NeonRendering.Ink);
            Raylib.DrawCircleV(end, reducedEffects ? 3 : 4 + MathF.Sin(time * 31) * 1.2f, color);
        }
        foreach (var blast in snapshot.Explosions ?? [])
        {
            var age = Math.Clamp((snapshot.Tick - blast.Born) / 10f, 0, 1);
            if (!Visible(new(blast.X, blast.Y), origin, blast.Radius)) continue;
            Raylib.DrawCircleLines((int)blast.X, (int)blast.Y, blast.Radius * (.3f + age * .7f), Raylib.Fade(Gold, 1 - age));
            if (!reducedEffects) Raylib.DrawCircleV(new(blast.X, blast.Y), blast.Radius * .7f, Raylib.Fade(Gold, (1 - age) * .12f));
        }
        DrawThreatWarnings(snapshot, ownId, origin);
    }

    private void Observe(RoomSnapshot frame, int ownId, Vector2 origin, float time, bool low)
    {
        bool initial = tick < 0;
        // Suppress stale transition bursts after a connection stall / room switch.
        bool transitions = !initial && frame.Tick - tick <= Room.TickRate;
        shotTransitions = transitions; // Keep initial/stall suppression for all samples of this tick.
        if (transitions)
        {
            foreach (var p in frame.Players)
            {
                if (!players.TryGetValue(p.Id, out var old)) continue;
                Vector2 position = new(p.X, p.Y);
                Color color = PlayerColor(p);
                if (old.Hull > 0 && p.Hull <= 0)
                    Add(position, Vector2.Zero, color, time, .6f, EffectKind.Death, p.Id, origin);
                else if (p.Hull > 0)
                {
                    if (p.Hull < old.Hull || p.Shield < old.Shield)
                        Add(position, Vector2.Zero, p.Shield < old.Shield ? Shield : color,
                            time, .22f, EffectKind.Hit, p.Id, origin);
                    float distance = Vector2.DistanceSquared(position, new(old.X, old.Y));
                    // No trails spanning teleports/respawns; low FX has no trails at all.
                    if (!low && old.Hull > 0 && distance > 4 && distance < 160 * 160)
                        Add(new(old.X, old.Y), position - new Vector2(old.X, old.Y), color,
                            time, .20f, EffectKind.Trail, p.Id, origin);
                }
            }
            var currentEnemies = new HashSet<int>();
            foreach (var e in frame.Enemies ?? [])
            {
                currentEnemies.Add(e.Id);
                if (enemies.TryGetValue(e.Id, out var old) && e.Hull < old.Hull)
                {
                    Add(new(e.X, e.Y), Vector2.Zero, EnemyColor(e), time, .22f,
                        EffectKind.Hit, e.Id, origin);
                    if (!low && e.Hull > 0 &&
                        (enemyHitFlashes.ContainsKey(e.Id) || enemyHitFlashes.Count < Room.EnemyCapacity))
                        enemyHitFlashes[e.Id] = time + EnemyHitFlashDuration;
                }
            }
            // Server removes killed aliens. Never infer pilot death from disconnect/removal.
            foreach (var old in enemies.Values)
                if (!currentEnemies.Contains(old.Id))
                    Add(new(old.X, old.Y), Vector2.Zero, EnemyColor(old), time, .6f,
                        EffectKind.Death, old.Id, origin);
        }
        players.Clear();
        foreach (var p in frame.Players.Take(Room.Capacity)) players[p.Id] = p;
        enemies.Clear();
        foreach (var e in (frame.Enemies ?? []).Take(Room.EnemyCapacity)) enemies[e.Id] = e;
        tick = frame.Tick; // Damage/death transitions remain tick-deduplicated.
    }

    private void ObserveShots(RoomSnapshot frame, int ownId, Vector2 origin, float time, bool low)
    {
        // Playback birth-gates shots: new IDs can appear without a new authoritative tick.
        // Remember each sampled ID even when FX are suppressed, so it cannot flash later.
        if (shotTransitions)
        {
            foreach (var s in frame.Shots.Take(256))
            {
                if (shots.Contains(s.Id) || low || s.Parent > 0) continue;
                Vector2 muzzle = new(s.X, s.Y);
                if (s.Owner > 0)
                {
                    var owner = frame.Players.FirstOrDefault(p => p.Id == s.Owner && p.Hull > 0);
                    if (owner is not null) muzzle = new Vector2(owner.X, owner.Y) + Direction(new(s.Dx, s.Dy)) * 25;
                }
                else
                {
                    var owner = (frame.Enemies ?? []).FirstOrDefault(e => e.Id == -s.Owner);
                    if (owner is not null) muzzle = new Vector2(owner.X, owner.Y) + Direction(new(s.Dx, s.Dy)) * 25;
                }
                Add(muzzle, Direction(new(s.Dx, s.Dy)), ShotColor(s.Owner, ownId), time,
                    .09f, EffectKind.Muzzle, s.Id, origin);
            }
        }
        shots.Clear();
        foreach (var s in frame.Shots.Take(256)) shots.Add(s.Id);
    }

    private void DrawPlayer(PlayerSnapshot p, int ownId, Vector2 origin, float time, bool low, bool friendly)
    {
        Vector2 position = new(p.X, p.Y);
        if (p.Hull <= 0 || !Visible(position, origin, 65)) return;
        Color color = PlayerColor(p);
        if (p.Editing) color = Raylib.Fade(color, .35f);
        Box box = new(p.X - 14, p.Y - 14, 28, 28);
        if (!low) NeonRendering.GlowBox(box, color);
        else
        {
            Raylib.DrawRectangleRec(new Rectangle(box.X, box.Y, 28, 28), Raylib.Fade(color, .65f));
            Raylib.DrawRectangleLinesEx(new Rectangle(box.X, box.Y, 28, 28), 2, color);
        }
        NeonRendering.DrawPlayerCore(position, low ? 0 : time, color);
        Vector2 forward = new(MathF.Cos(p.Aim), MathF.Sin(p.Aim));
        Raylib.DrawLineEx(position, position + forward * 23, 5, NeonRendering.Ink);
        if (p.Shield > 0)
        {
            Raylib.DrawCircleLines((int)p.X, (int)p.Y, 22, Shield);
            for (int i = 0; i < Math.Min(p.Shield, 2); i++)
                Raylib.DrawRectangle((int)p.X - 8 + i * 10, (int)p.Y + 35, 6, 2, Shield);
        }
        if (p.Protected)
        {
            // Always visible, including reduced FX: protection is gameplay information.
            Raylib.DrawCircleLines((int)p.X, (int)p.Y, 26, NeonRendering.Ink);
            Raylib.DrawText(p.Editing ? "CLOAKED" : "SAFE", (int)p.X - 12, (int)p.Y - 49, 10, NeonRendering.Ink);
        }
        string name = p.Name.Length > 18 ? p.Name[..18] : p.Name;
        name += $" L{p.Level}";
        if (friendly) { name += " [ALLY]"; Raylib.DrawCircleLines((int)p.X, (int)p.Y, 30, new Color(120, 245, 170, 255)); }
        if (!p.Connected) name += " [LINK]";
        Raylib.DrawText(name, (int)p.X - Raylib.MeasureText(name, 10) / 2, (int)p.Y - 36, 10, color);
        for (int i = 0; i < 3; i++)
            Raylib.DrawRectangle((int)p.X - 14 + i * 10, (int)p.Y + 29, 8, 3,
                i < p.Hull ? color : new Color(35, 42, 55, 255));
    }

    private static Color PlayerColor(PlayerSnapshot p) => p.Level switch
    { < 3 => Cyan, < 5 => new(90, 165, 255, 255), < 7 => new(178, 110, 255, 255),
      < 10 => new(255, 100, 185, 255), < 15 => Gold, _ => new(240, 255, 220, 255) };
    private static Color ProjectileColor(ShotSnapshot s) => (s.Effects & 16) != 0 ? new(190,135,255,255) : (s.Effects & 8) != 0 ? new(185,255,85,255) : (s.Effects & 2) != 0 ? new(150, 255, 70, 255)
        : (s.Effects & 1) != 0 ? new(255, 130, 45, 255) : (s.Effects & 4) != 0 ? new(125, 200, 255, 255)
        : s.Kind switch { 2 => Cyan, 3 => new(255, 135, 70, 255), 4 => new(195, 110, 255, 255), _ => Gold };

    private void DrawEnemy(EnemySnapshot e, Vector2 origin, float time, bool low)
    {
        if (e.Hull <= 0) return;
        Vector2 p = new(e.X, e.Y);
        Vector2 d = new(MathF.Cos(e.Aim), MathF.Sin(e.Aim));
        Color color = !low && enemyHitFlashes.TryGetValue(e.Id, out var until) && time < until
            ? NeonRendering.Ink : EnemyColor(e);
        bool gunner = e.Kind == "gunner";
        // Draw warning rays even when their emitter is just outside the viewport.
        if (e.Telegraph && gunner && SegmentVisible(p, p + d * 650, origin))
            Raylib.DrawLineEx(p, p + d * 650, 1, Raylib.Fade(Hostile, .65f));
        if (!Visible(p, origin, 80)) return;
        if (e.Effects != 0)
            Raylib.DrawCircleLines((int)e.X, (int)e.Y, 27, (e.Effects & 16) != 0 ? new Color(190,135,255,255) : (e.Effects & 8) != 0 ? new Color(185,255,85,255) : (e.Effects & 32) != 0 ? Gold : (e.Effects & 2) != 0 ? new Color(150,255,70,255)
                : (e.Effects & 1) != 0 ? new Color(255,130,45,255) : new Color(125,200,255,255));
        if (!low)
        {
            Raylib.DrawCircleV(p, 29, Raylib.Fade(color, .10f));
            Raylib.DrawCircleV(p + new Vector2(0, 25), 3 + 2 * MathF.Sin(time * 20), Raylib.Fade(color, .6f));
        }
        if (gunner)
        {
            Raylib.DrawPoly(p, 6, 20, 30, Raylib.Fade(color, .35f));
            Raylib.DrawPolyLinesEx(p, 6, 20, 30, 2, color);
            Raylib.DrawLineEx(p, p + d * 34, 5, color);
        }
        else
        {
            Raylib.DrawTriangle(p + new Vector2(0, -20), p + new Vector2(-20, 20), p + new Vector2(20, 20), Raylib.Fade(color, .45f));
            Raylib.DrawTriangleLines(p + new Vector2(0, -20), p + new Vector2(-20, 20), p + new Vector2(20, 20), color);
            Raylib.DrawLineEx(p + new Vector2(-20, 0), p + new Vector2(-28, -8), 2, color);
            Raylib.DrawLineEx(p + new Vector2(20, 0), p + new Vector2(28, -8), 2, color);
        }
        Raylib.DrawCircleV(p, 5, NeonRendering.Ink);
        if (e.Telegraph)
        {
            Raylib.DrawCircleLines((int)p.X, (int)p.Y, 32, Hostile);
            Raylib.DrawText("!", (int)p.X - 4, (int)p.Y - 46, 20, NeonRendering.Ink);
        }
    }

    private void DrawThreatWarnings(RoomSnapshot frame, int ownId, Vector2 origin)
    {
        var self = frame.Players.FirstOrDefault(p => p.Id == ownId && p.Hull > 0);
        if (self is null) return;
        Vector2 pilot = new(self.X, self.Y);
        foreach (var e in (frame.Enemies ?? []).Take(Room.EnemyCapacity))
        {
            Vector2 p = new(e.X, e.Y);
            if (e.Hull <= 0 || Visible(p, origin, 0) ||
                Vector2.DistanceSquared(p, pilot) > 700 * 700) continue;
            Vector2 screen = p - origin;
            // Keep the entire marker (12-unit ring/stroke) clear of telemetry through
            // y=105 and the bottom console starting at y=466, in logical canvas units.
            Vector2 edge = Vector2.Clamp(screen, new(24, 122), new(view.X - 24, view.Y - 92));
            Vector2 marker = origin + edge; // This pass is inside the world camera.
            Vector2 direction = Direction(screen - edge);
            Raylib.DrawCircleLines((int)marker.X, (int)marker.Y, e.Telegraph ? 12 : 7, Hostile);
            Raylib.DrawLineEx(marker, marker + direction * 12, 3, Hostile);
            // Telegraphs are gameplay information: retain them in reduced FX mode.
            if (e.Telegraph)
                Raylib.DrawText("!", (int)marker.X - 4, (int)marker.Y - 10, 18, NeonRendering.Ink);
        }
    }

    private void Add(Vector2 p, Vector2 d, Color color, float time, float duration,
        EffectKind kind, int seed, Vector2 origin)
    {
        if (effects.Count >= EffectCapacity || !Visible(p, origin, 100)) return;
        effects.Add(new(p, d, color, time, duration, kind, seed));
    }

    private void DrawEffects(Vector2 origin, float time, bool low)
    {
        foreach (var e in effects)
        {
            float age = Math.Max(0, time - e.Born);
            float alpha = Math.Clamp(1 - age / e.Duration, 0, 1);
            if (!Visible(e.Position, origin, 120)) continue;
            Color color = Raylib.Fade(e.Color, alpha);
            if (e.Kind == EffectKind.Trail)
            {
                if (!low) Raylib.DrawRectangleLinesEx(new Rectangle(e.Position.X - 14, e.Position.Y - 14, 28, 28), 1, Raylib.Fade(color, .35f));
            }
            else if (e.Kind == EffectKind.Muzzle)
            {
                if (low) continue;
                Raylib.DrawCircleV(e.Position, 14 * alpha, Raylib.Fade(color, .2f));
                Raylib.DrawCircleV(e.Position, 5 * alpha, Raylib.Fade(NeonRendering.Ink, alpha));
            }
            else if (e.Kind == EffectKind.Hit)
            {
                Raylib.DrawCircleLines((int)e.Position.X, (int)e.Position.Y, 17 + age * 70, color);
                if (!low) Raylib.DrawCircleV(e.Position, 10 * alpha, Raylib.Fade(NeonRendering.Ink, alpha * .55f));
            }
            else
            {
                Raylib.DrawCircleLines((int)e.Position.X, (int)e.Position.Y, 12 + age * 140, color);
                // Deterministic cosmetic spark fan; no random simulation or reward inference.
                for (int i = 0; i < (low ? 4 : 16); i++)
                {
                    float angle = i * MathF.Tau / (low ? 4 : 16) + e.Seed * .37f;
                    Vector2 d = new(MathF.Cos(angle), MathF.Sin(angle));
                    Vector2 p = e.Position + d * age * (70 + i % 4 * 35);
                    Raylib.DrawLineEx(p, p - d * (low ? 3 : 7), 2, color);
                }
            }
        }
    }

    private void Reset()
    {
        tick = -1;
        shotTransitions = false;
        effects.Clear();
        enemyHitFlashes.Clear();
        players.Clear();
        enemies.Clear();
        shots.Clear();
    }

    private static Color ShotColor(int owner, int ownId) => owner < 0 ? Hostile : owner == ownId ? Gold : Rival;
    private static Color EnemyColor(EnemySnapshot e) => e.Kind == "gunner" ? Hostile : Rammer;
    private static Vector2 Direction(Vector2 d) => d.LengthSquared() < .001f ? -Vector2.UnitY : Vector2.Normalize(d);
    private bool Visible(Vector2 p, Vector2 origin, float margin) =>
        p.X >= origin.X - margin && p.X <= origin.X + view.X + margin &&
        p.Y >= origin.Y - margin && p.Y <= origin.Y + view.Y + margin;
    private bool SegmentVisible(Vector2 a, Vector2 b, Vector2 origin) =>
        Math.Max(a.X, b.X) >= origin.X && Math.Min(a.X, b.X) <= origin.X + view.X &&
        Math.Max(a.Y, b.Y) >= origin.Y && Math.Min(a.Y, b.Y) <= origin.Y + view.Y;
}
