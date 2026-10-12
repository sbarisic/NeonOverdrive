using System;
using System.Numerics;
using ArcadeDodger.Multiplayer;
using Raylib_cs;

namespace ArcadeDodger.Web;

// Screen-space presentation only. The host owns input, menus and respawn requests.
// Uniformly scaled logical HUD, anchored to the current viewport edges.
internal static class BrowserArenaHud
{
    private static readonly Color Cyan = new(65, 245, 235, 255);
    private static readonly Color Pink = new(255, 65, 130, 255);
    private static readonly Color Amber = new(255, 210, 80, 255);
    private static readonly Color Muted = new(125, 166, 184, 255);
    private static readonly Color Border = new(40, 112, 132, 255);
    private static readonly Color Enemy = new(255, 100, 95, 255);
    private static readonly Color Ink = NeonRendering.Ink;

    public static void Draw(RoomSnapshot? snapshot, int ownId, Vector2 origin, Vector2 view, float scale, float time,
        bool menuOpen, bool reducedEffects)
    {
        PlayerSnapshot? self = null;
        var connected = 0;
        if (snapshot is not null)
            foreach (var player in snapshot.Players)
            {
                if (player.Id == ownId) self = player;
                if (player.Connected) connected++;
            }

        var extra = view - new Vector2(960, 540);
        Raylib.BeginMode2D(new Camera2D { Zoom = scale });
        Panel(12, 8, (int)view.X - 24, 36, Cyan);
        Text("LIVE SECTOR", 24, 14, 10, Muted);
        Text(snapshot is null ? "AWAITING SNAPSHOT" : $"PILOTS {connected}/{Room.Capacity}", 24, 28, 10, Ink);
        Raylib.DrawLine(310, 14, 310, 37, Border);
        Center("NEON OVERDRIVE", 19, 16, Cyan, (int)view.X);
        Raylib.DrawLine((int)view.X - 312, 14, (int)view.X - 312, 37, Border);
        Text("PvPvE / SERVER AUTHORITATIVE", (int)view.X - 294, 14, 10, Muted);
        Text("LIVE ROOM - NEVER PAUSED", (int)view.X - 294, 28, 10, Amber);

        Panel(12, 50, 340, 55, Cyan);
        if(snapshot?.Workshop is {} workshop) Text($"CREDITS {workshop.Credits}",24,112,10,Amber);
        if (self is null)
        {
            Text("PILOT TELEMETRY / WAITING", 24, 61, 10, Muted);
            Text("Hull, shield and energy arrive with your pilot.", 24, 82, 10, Ink);
        }
        else
        {
            Text($"HULL {self.Hull}/{self.MaxHull}", 24, 59, 10, self.Hull > 0 ? Ink : Pink);
            Meter(94, 61, 36, 4, self.Hull / (float)self.MaxHull, Cyan);
            Text($"SHIELD {self.Shield}/{self.MaxShield}", 139, 59, 10, Ink);
            Meter(229, 61, 68, 4, self.Shield / (float)self.MaxShield, new Color(100, 165, 255, 255));
            Text(self.Protected ? "SAFE" : self.Connected ? "ON" : "OFF", 308, 59, 10,
                self.Protected ? Amber : self.Connected ? Cyan : Pink);
            var level = Math.Clamp(self.Level, 1, 100);
            var floor = Room.ExperienceForLevel(level);
            var ceiling = Room.ExperienceForLevel(level + 1);
            var progress = level >= 100 ? 1f : Math.Clamp((self.Experience - floor) / (float)(ceiling - floor), 0, 1);
            Text($"LEVEL {self.Level} / ENERGY XP {self.Experience}", 24, 77, 10, Cyan);
            Right(level >= 100 ? "MAX LEVEL" : $"NEXT {ceiling}", 340, 77, 10, Muted);
            Meter(24, 94, 316, 3, progress, Cyan);
        }

        Raylib.EndMode2D();
        // Keep the compact console contents centered, with its frame spanning the window.
        Raylib.BeginMode2D(new Camera2D { Zoom = scale, Offset = new Vector2(extra.X / 2, extra.Y) * scale });
        Panel(12 - (int)(extra.X / 2), 466, (int)view.X - 24, 66, Cyan);
        // Leave the former primary-control section blank.
        Raylib.DrawLine(151, 476, 151, 522, Border);
        Text(self?.Ability is "phase" or "long-phase" ? "SHIFT / PHASE" : "SHIFT / DASH", 165, 477, 10, Muted);
        var cooldown = self?.DashCooldown ?? 0;
        Text(self is null ? "WAITING" : self.Hull <= 0 ? "OFFLINE" : cooldown > 0
            ? $"{cooldown / (float)Room.TickRate:0.0}s" : "READY", 165, 491, 16,
            self?.Hull > 0 && cooldown == 0 ? Cyan : Ink);
        Meter(165, 513, 112, 4, self?.Hull > 0 ? 1 - Math.Clamp(cooldown / (float)self.DashCooldownMax, 0, 1) : 0, Cyan);
        Raylib.DrawLine(294, 476, 294, 522, Border);
        DrawRadar(snapshot, ownId, origin, view, time, reducedEffects);
        Raylib.DrawLine(570, 476, 570, 522, Border);
        Text($"WEAPON / {self?.Weapon.ToUpperInvariant() ?? "BLASTER"}", 590, 480, 10, Cyan);
        Text(self?.SkillPoints > 0 ? $"TAB / {self.SkillPoints} SKILL POINTS" : "TAB / SKILLS   I / WEAPONS", 590, 501, 10,
            self?.SkillPoints > 0 ? Amber : Muted);

        Raylib.EndMode2D();
        Raylib.BeginMode2D(new Camera2D { Zoom = scale });
        if (menuOpen || self is null || self.Hull <= 0)
            Raylib.DrawRectangle(0, 108, (int)view.X, (int)view.Y - 186, new Color(4, 12, 22, 165));
        Raylib.EndMode2D();
        Raylib.BeginMode2D(new Camera2D { Zoom = scale, Offset = extra / 2 * scale });
        // The accessible DOM shell owns menu text and controls; do not draw a
        // second panel underneath it. Keep only world dimming in the canvas.
        if (!menuOpen && self is null)
            DrawOverlay("JOINING ARENA", "Waiting for your pilot in the room snapshot.",
                "Connection and join controls are in the browser.",
                "ESC menu / R effects toggle", Cyan);
        else if (!menuOpen && self?.Hull <= 0)
            DrawOverlay("RUN ENDED", $"LEVEL {self.Level} / ENERGY XP {self.Experience}",
                "Use the browser Respawn button to retry.",
                "A new run resets hull, shield and energy. The room stays live.", Pink);
        Raylib.EndMode2D();
    }

    private static void DrawRadar(RoomSnapshot? snapshot, int ownId, Vector2 origin, Vector2 view, float time, bool reducedEffects)
    {
        var map = new Rectangle(307, 477, 160, 45);
        Raylib.DrawRectangleRec(map, new Color(5, 12, 17, 255));
        for (var i = 1; i < 4; i++)
            Raylib.DrawLine(307 + i * 40, 477, 307 + i * 40, 522, Raylib.Fade(Border, .4f));
        Raylib.DrawLine(307, 499, 467, 499, Raylib.Fade(Border, .4f));
        Raylib.DrawRectangleLinesEx(map, 1, Border);
        var top = Map(origin, map);
        var bottom = Map(origin + view, map);
        Raylib.DrawRectangleLinesEx(new Rectangle(top.X, top.Y, bottom.X - top.X, bottom.Y - top.Y), 1, Muted);
        if (snapshot is not null)
        {
            foreach (var drop in snapshot.Energy ?? [])
                Raylib.DrawCircleV(Map(new Vector2(drop.X, drop.Y), map), 1.5f, Cyan);
            foreach (var enemy in snapshot.Enemies ?? [])
                if (enemy.Hull > 0)
                    Raylib.DrawCircleV(Map(new Vector2(enemy.X, enemy.Y), map), 2, Enemy);
            foreach (var player in snapshot.Players)
            {
                if (player.Hull <= 0) continue;
                var point = Map(new Vector2(player.X, player.Y), map);
                if (player.Id == ownId)
                {
                    if (!reducedEffects)
                        Raylib.DrawCircleLines((int)point.X, (int)point.Y, 4 + MathF.Sin(time * 3), Cyan);
                    Raylib.DrawCircleV(point, 2.5f, Ink);
                }
                else Raylib.DrawRectangle((int)point.X - 2, (int)point.Y - 2, 4, 4,
                    player.Connected ? Pink : Muted);
            }
        }
        DotLabel(479, 481, Ink, "YOU");
        DotLabel(519, 481, Pink, "PILOT");
        DotLabel(479, 499, Enemy, "ALIEN");
        DotLabel(519, 499, Cyan, "XP");
        Text("RADAR / VIEW", 479, 514, 8, Muted);
    }

    private static Vector2 Map(Vector2 world, Rectangle map)
    {
        // Inset markers so edge spawns stay inside the radar border.
        return new Vector2(map.X + 3 + Math.Clamp(world.X / Room.Width, 0, 1) * (map.Width - 6),
            map.Y + 3 + Math.Clamp(world.Y / Room.Height, 0, 1) * (map.Height - 6));
    }

    private static void DrawOverlay(string title, string summary, string action, string hint, Color accent)
    {
        Panel(180, 167, 600, 205, accent);
        Center(title, 190, 28, accent);
        Raylib.DrawLine(204, 232, 756, 232, Border);
        Center(summary, 251, 14, Ink);
        Center(action, 290, 14, accent);
        Center(hint, 337, 10, Muted);
    }

    private static void Pips(int x, int y, int value, int max, Color color)
    {
        for (var i = 0; i < max; i++) Raylib.DrawRectangle(x + i * 11, y, 8, 7, i < value ? color : Border);
    }

    private static void Meter(int x, int y, int width, int height, float progress, Color color)
    {
        Raylib.DrawRectangle(x, y, width, height, Border);
        Raylib.DrawRectangle(x, y, (int)(width * Math.Clamp(progress, 0, 1)), height, color);
    }

    private static void DotLabel(int x, int y, Color color, string label)
    {
        Raylib.DrawCircle(x, y + 3, 2, color);
        Text(label, x + 6, y, 8, Muted);
    }

    private static void Panel(int x, int y, int width, int height, Color accent) =>
        NeonRendering.DrawConsolePanel(x, y, width, height, accent);
    private static void Text(string text, int x, int y, int size, Color color) =>
        Raylib.DrawText(text, x, y, size, color);
    private static void Right(string text, int x, int y, int size, Color color) =>
        Text(text, x - Raylib.MeasureText(text, size), y, size, color);
    private static void Center(string text, int y, int size, Color color, int width = 960) =>
        Text(text, (width - Raylib.MeasureText(text, size)) / 2, y, size, color);
}
