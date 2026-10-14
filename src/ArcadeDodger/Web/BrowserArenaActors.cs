using System.Numerics;
using ArcadeDodger.Multiplayer;
using Raylib_cs;

namespace ArcadeDodger.Web;

internal sealed partial class BrowserArenaWorld
{
    public string SelectedContact { get; set; } = "";

    private void DrawPlayer(
        PlayerSnapshot p,
        int ownId,
        Vector2 origin,
        float time,
        bool low,
        bool friendly
    )
    {
        Vector2 position = new(p.X, p.Y);
        if (p.Hull <= 0 || !Visible(position, origin, 65))
            return;
        Color color = PlayerColor(p);
        var radius = MathF.Max(20, ChassisCatalog.Get(p.Chassis).Radius);
        if (p.Editing)
            color = Raylib.Fade(color, .35f);
        DrawChassis(p.Chassis, position, p.Heading, p.Aim, p.Throttle, p.ActiveBays, color, low);
        // Travel direction stays independent of hull/weapon orientation, even with reduced FX.
        var velocity = new Vector2(p.VelocityX, p.VelocityY);
        if (p.Id == ownId && velocity.LengthSquared() > 1)
        {
            var direction = Vector2.Normalize(velocity);
            var side = new Vector2(-direction.Y, direction.X);
            var tip = position + direction * (radius + 16);
            var tail = tip - direction * 7;
            var marker = p.Braking ? new Color(255, 210, 80, 255) : new Color(235, 240, 250, 255);
            Raylib.DrawLineEx(tail + side * 4, tip, 1.5f, marker);
            Raylib.DrawLineEx(tail - side * 4, tip, 1.5f, marker);
        }
        if (p.Id == ownId && sector is not null && !p.Editing && p.Docked == 0)
        {
            var body = sector.Locations.FirstOrDefault(l =>
                l.Solid && Vector2.Distance(position, l.Position) < l.Radius + sector.GravityRange
            );
            if (body is not null)
            {
                var pull = Vector2.Normalize(body.Position - position);
                var side = new Vector2(-pull.Y, pull.X);
                var tip = position + pull * (radius + 25);
                Raylib.DrawLineEx(tip - pull * 8 + side * 5, tip, 2, Gold);
                Raylib.DrawLineEx(tip - pull * 8 - side * 5, tip, 2, Gold);
                BrowserText.DrawText(
                    p.Throttle > 0 ? "GRAVITY ASSIST" : "GRAVITY",
                    (int)p.X - 24,
                    (int)(p.Y + radius + 25),
                    10,
                    Gold
                );
            }
        }
        if (p.Shield > 0 && (p.Id == ownId || SelectedContact == "player:" + p.Id))
        {
            Raylib.DrawCircleLines((int)p.X, (int)p.Y, radius + 2, Shield);
            Raylib.DrawRectangle(
                (int)p.X - 20,
                (int)(p.Y + radius + 15),
                (int)(40f * p.Shield / Math.Max(1, p.MaxShield)),
                2,
                Shield
            );
        }
        if (p.Protected)
        {
            // Always visible, including reduced FX: protection is gameplay information.
            Raylib.DrawCircleLines((int)p.X, (int)p.Y, radius + 6, NeonRendering.Ink);
            BrowserText.DrawText(
                p.Editing ? "CLOAKED" : "SAFE",
                (int)p.X - 12,
                (int)(p.Y - radius - 29),
                10,
                NeonRendering.Ink
            );
        }
        if (p.Id != ownId && SelectedContact != "player:" + p.Id)
            return;
        string name = p.Name.Length > 18 ? p.Name[..18] : p.Name;
        name += $" L{p.Level}";
        if (friendly)
        {
            name += " [ALLY]";
            Raylib.DrawCircleLines((int)p.X, (int)p.Y, radius + 10, new Color(120, 245, 170, 255));
        }
        if (!p.Connected)
            name += " [LINK]";
        BrowserText.DrawText(
            name,
            (int)p.X - BrowserText.MeasureText(name, 10) / 2,
            (int)(p.Y - radius - 16),
            10,
            color
        );
        Raylib.DrawRectangle(
            (int)p.X - 20,
            (int)(p.Y + radius + 9),
            40,
            3,
            new Color(35, 42, 55, 255)
        );
        Raylib.DrawRectangle(
            (int)p.X - 20,
            (int)(p.Y + radius + 9),
            (int)(40f * p.Hull / Math.Max(1, p.MaxHull)),
            3,
            color
        );
    }

    private static Color PlayerColor(PlayerSnapshot p) =>
        p.Level switch
        {
            < 3 => Cyan,
            < 5 => new(90, 165, 255, 255),
            < 7 => new(178, 110, 255, 255),
            < 10 => new(255, 100, 185, 255),
            < 15 => Gold,
            _ => new(240, 255, 220, 255),
        };

    private static Color ProjectileColor(ShotSnapshot s) =>
        (s.Effects & 16) != 0 ? new(190, 135, 255, 255)
        : (s.Effects & 8) != 0 ? new(185, 255, 85, 255)
        : (s.Effects & 2) != 0 ? new(150, 255, 70, 255)
        : (s.Effects & 1) != 0 ? new(255, 130, 45, 255)
        : (s.Effects & 4) != 0 ? new(125, 200, 255, 255)
        : s.Kind switch
        {
            2 => Cyan,
            3 => new(255, 135, 70, 255),
            4 => new(195, 110, 255, 255),
            _ => Gold,
        };

    private readonly Dictionary<int, (Vector2 Position, float Time, float Thrust)> engineMotion =
    [];

    private void DrawEnemy(EnemySnapshot e, Vector2 origin, float time, bool low)
    {
        if (e.Hull <= 0)
            return;
        Vector2 p = new(e.X, e.Y);
        var thrust = 0f;
        if (engineMotion.TryGetValue(e.Id, out var previous) && time > previous.Time)
            thrust = Math.Clamp(
                Vector2.Distance(p, previous.Position)
                    / (time - previous.Time)
                    / Math.Max(1, EnemyCatalog.Get(e.Kind).Speed),
                0,
                1
            );
        engineMotion[e.Id] = (p, time, thrust);
        if (engineMotion.Count > 256)
            foreach (
                var id in engineMotion
                    .Where(x => time - x.Value.Time > 2)
                    .Select(x => x.Key)
                    .ToArray()
            )
                engineMotion.Remove(id);
        Vector2 d = new(MathF.Cos(e.Aim), MathF.Sin(e.Aim));
        Color color =
            !low && enemyHitFlashes.TryGetValue(e.Id, out var until) && time < until
                ? NeonRendering.Ink
                : EnemyColor(e);
        var radius = e.Radius;
        var gunner =
            e.Kind is "gunner" or "sniper" or "interceptor" or "turret" or "rover" or "relief";
        var reach = e.Kind == "sniper" ? 850 : 650;
        if (e.Telegraph && gunner && SegmentVisible(p, p + d * reach, origin))
            Raylib.DrawLineEx(
                p,
                p + d * reach,
                e.Kind == "sniper" ? 2 : 1,
                Raylib.Fade(color, .7f)
            );
        if (!Visible(p, origin, 80))
            return;
        if (!low)
            Raylib.DrawCircleV(p, radius + 6, Raylib.Fade(color, .12f));
        var sides = e.Kind switch
        {
            "turret" => 4,
            "rover" => 6,
            "relief" => 8,
            "gunner" => 6,
            "sniper" => 4,
            "tender" => 8,
            _ => 3,
        };
        var angle = e.Aim * 180 / MathF.PI;
        if (
            e.SurfacePlanet != 0
            && sector?.Locations.FirstOrDefault(l => l.Id == e.SurfacePlanet) is { } planet
        )
        {
            var normal = Vector2.Normalize(p - planet.Position);
            var tangent = new Vector2(-normal.Y, normal.X);
            angle = MathF.Atan2(normal.Y, normal.X) * 180 / MathF.PI;
            var foot = p - normal * 29;
            Raylib.DrawLineEx(p, foot, e.Kind == "turret" ? 8 : 4, color);
            Raylib.DrawLineEx(foot - tangent * radius, foot + tangent * radius, 5, color);
            if (e.Kind == "rover")
                for (var wheel = -1; wheel <= 1; wheel++)
                    Raylib.DrawCircleV(foot + tangent * radius * wheel, 5, color);
        }
        var structural =
            e.Kind
            is "installation"
                or "turret"
                or "rover"
                or "salvage-core"
                or "salvage-turret"
                or "repair-bot";
        if (!structural)
            DrawChassis(
                e.Chassis,
                p,
                e.Heading,
                e.Aim,
                e.Throttle,
                gunner ? 1 : 0,
                color,
                low,
                EnemyCatalog.Size(e.Level)
            );
        else if (e.Kind is "turret" or "rover" or "salvage-turret")
            DrawPanels(
                e.Kind == "rover" ? StructureCatalog.Rover : StructureCatalog.Turret,
                p,
                e.Aim,
                color,
                EnemyCatalog.Size(e.Level)
            );
        else
        {
            Raylib.DrawPoly(p, sides, radius, angle, Raylib.Fade(color, .4f));
            Raylib.DrawPolyLinesEx(p, sides, radius, angle, 2, color);
            Raylib.DrawRectanglePro(
                new(p.X, p.Y, radius * 1.3f, radius * .7f),
                new(radius * .65f, radius * .35f),
                angle,
                Raylib.Fade(color, .6f)
            );
        }
        if (gunner && structural)
            Raylib.DrawLineEx(p, p + d * (radius + 10), e.Kind == "sniper" ? 3 : 5, color);
        if (e.Kind == "tender")
        {
            Raylib.DrawLineEx(p - new Vector2(7, 0), p + new Vector2(7, 0), 3, color);
            Raylib.DrawLineEx(p - new Vector2(0, 7), p + new Vector2(0, 7), 3, color);
        }

        if (e.MaxShield > 0 && e.Shield > 0)
            Raylib.DrawCircleLines((int)p.X, (int)p.Y, radius + 5, Raylib.Fade(Cyan, .6f));
        if (SelectedContact == "npc:" + e.Id || e.Kind == "relief")
        {
            Raylib.DrawRectangle((int)p.X - 25, (int)(p.Y + radius + 8), 50, 3, MutedMine);
            Raylib.DrawRectangle(
                (int)p.X - 25,
                (int)(p.Y + radius + 8),
                (int)(50f * e.Hull / e.MaxHull),
                3,
                Cyan
            );
        }
        if (e.Effects != 0)
            Raylib.DrawCircleLines((int)p.X, (int)p.Y, radius + 4, Gold);
        if (e.Kind == "contact" && SelectedContact == "npc:" + e.Id)
            BrowserText.DrawText("E / HAIL", (int)p.X - 30, (int)p.Y + 40, 12, Cyan);
        if (e.Kind == "repair-bot")
        {
            var drone = p + new Vector2(MathF.Cos(time), MathF.Sin(time)) * 45;
            Raylib.DrawRectangle((int)drone.X - 3, (int)drone.Y - 3, 6, 6, Cyan);
            if (!low)
                Raylib.DrawLineEx(p, drone, 1, Raylib.Fade(Cyan, .35f));
            BrowserText.DrawText("REPAIR BAY", (int)p.X - 35, (int)p.Y + 40, 12, Cyan);
        }
        if (SelectedContact == "npc:" + e.Id)
            BrowserText.DrawText($"L{e.Level}", (int)p.X - 8, (int)(p.Y - radius - 15), 10, color);
        if (e.Telegraph)
            Raylib.DrawCircleLines((int)p.X, (int)p.Y, radius + 8, Hostile);
    }

    private void DrawThreatWarnings(RoomSnapshot frame, int ownId, Vector2 origin)
    {
        var self = frame.Players.FirstOrDefault(p => p.Id == ownId && p.Hull > 0);
        if (self is null)
            return;
        Vector2 pilot = new(self.X, self.Y);
        foreach (var e in (frame.Enemies ?? []).Take(256))
        {
            Vector2 p = new(e.X, e.Y);
            if (
                e.Hull <= 0
                || e.Faction == "friendly"
                || Visible(p, origin, 0)
                || Vector2.DistanceSquared(p, pilot) > 700 * 700
            )
                continue;
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
                BrowserText.DrawText(
                    "!",
                    (int)marker.X - 4,
                    (int)marker.Y - 10,
                    18,
                    NeonRendering.Ink
                );
        }
    }
}
