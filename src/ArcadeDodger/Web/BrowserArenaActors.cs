using System.Numerics;
using ArcadeDodger.Multiplayer;
using Raylib_cs;

namespace ArcadeDodger.Web;

internal sealed partial class BrowserArenaWorld
{
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
        if (p.Editing)
            color = Raylib.Fade(color, .35f);
        Rectangle box = new(p.X - 14, p.Y - 14, 28, 28);
        if (!low)
            NeonRendering.GlowBox(box, color);
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
            Raylib.DrawText(
                p.Editing ? "CLOAKED" : "SAFE",
                (int)p.X - 12,
                (int)p.Y - 49,
                10,
                NeonRendering.Ink
            );
        }
        string name = p.Name.Length > 18 ? p.Name[..18] : p.Name;
        name += $" L{p.Level}";
        if (friendly)
        {
            name += " [ALLY]";
            Raylib.DrawCircleLines((int)p.X, (int)p.Y, 30, new Color(120, 245, 170, 255));
        }
        if (!p.Connected)
            name += " [LINK]";
        Raylib.DrawText(
            name,
            (int)p.X - Raylib.MeasureText(name, 10) / 2,
            (int)p.Y - 36,
            10,
            color
        );
        for (int i = 0; i < 3; i++)
            Raylib.DrawRectangle(
                (int)p.X - 14 + i * 10,
                (int)p.Y + 29,
                8,
                3,
                i < p.Hull ? color : new Color(35, 42, 55, 255)
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

    private void DrawEnemy(EnemySnapshot e, Vector2 origin, float time, bool low)
    {
        if (e.Hull <= 0)
            return;
        Vector2 p = new(e.X, e.Y);
        Vector2 d = new(MathF.Cos(e.Aim), MathF.Sin(e.Aim));
        Color color =
            !low && enemyHitFlashes.TryGetValue(e.Id, out var until) && time < until
                ? NeonRendering.Ink
                : EnemyColor(e);
        var radius = e.Radius;
        var gunner = e.Kind is "gunner" or "sniper" or "interceptor";
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
            "gunner" => 6,
            "sniper" => 4,
            "tender" => 8,
            _ => 3,
        };
        var angle = e.Aim * 180 / MathF.PI;
        Raylib.DrawPoly(p, sides, radius, angle, Raylib.Fade(color, .4f));
        Raylib.DrawPolyLinesEx(p, sides, radius, angle, 2, color);
        if (gunner)
            Raylib.DrawLineEx(p, p + d * (radius + 10), e.Kind == "sniper" ? 3 : 5, color);
        if (e.Kind == "tender")
        {
            Raylib.DrawLineEx(p - new Vector2(7, 0), p + new Vector2(7, 0), 3, color);
            Raylib.DrawLineEx(p - new Vector2(0, 7), p + new Vector2(0, 7), 3, color);
        }
        if (e.Kind == "interceptor")
            Raylib.DrawLineEx(p - d * radius, p - d * (radius + 12), 3, color);
        if (e.Effects != 0)
            Raylib.DrawCircleLines((int)p.X, (int)p.Y, radius + 4, Gold);
        Raylib.DrawText($"L{e.Level}", (int)p.X - 8, (int)(p.Y - radius - 15), 10, color);
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
                Raylib.DrawText("!", (int)marker.X - 4, (int)marker.Y - 10, 18, NeonRendering.Ink);
        }
    }
}
