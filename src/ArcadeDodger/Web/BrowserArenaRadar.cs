using System.Numerics;
using ArcadeDodger.Multiplayer;
using Raylib_cs;

namespace ArcadeDodger.Web;

internal static partial class BrowserArenaHud
{
    private static void DrawRadar(
        RoomSnapshot? snapshot,
        int ownId,
        Vector2 origin,
        Vector2 view,
        float time,
        bool reducedEffects
    )
    {
        var pilot = snapshot?.Players.FirstOrDefault(p => p.Id == ownId);
        radarCenter = pilot is null ? Vector2.Zero : new(pilot.X, pilot.Y);
        var density = Raylib.GetScreenWidth() / width;
        Raylib.BeginScissorMode(
            (int)(map.X * density),
            (int)(map.Y * density),
            (int)(map.Width * density),
            (int)(map.Height * density)
        );
        Raylib.DrawRectangleRec(map, new Color(5, 12, 17, 255));
        for (var i = 1; i < 4; i++)
            Raylib.DrawLine(
                (int)(map.X + i * map.Width / 4),
                (int)map.Y,
                (int)(map.X + i * map.Width / 4),
                (int)(map.Y + map.Height),
                Raylib.Fade(Border, .4f)
            );
        Raylib.DrawRectangleLinesEx(map, 1, Border);
        foreach (var location in sector?.Locations ?? [])
        {
            var at = Map(new(location.X, location.Y), map);
            if (location.Kind == "planet")
            {
                var color = location.Biome switch
                {
                    "volcanic" => new Color(171, 88, 54, 255),
                    "gas" => new Color(139, 117, 183, 255),
                    "shattered" => Muted,
                    _ => new Color(64, 133, 158, 255),
                };
                var radius = Math.Max(3, location.Radius / radarWidth * map.Width);
                Raylib.DrawCircleV(at, radius, Raylib.Fade(color, .5f));
                Raylib.DrawCircleLines((int)at.X, (int)at.Y, radius, color);
            }
            else if (location.Kind == "station")
            {
                Raylib.DrawRectangle((int)at.X - 3, (int)at.Y - 3, 6, 6, Cyan);
                Raylib.DrawRectangleLines((int)at.X - 5, (int)at.Y - 5, 10, 10, Ink);
            }
            else if (location.Kind is "base" or "corridor" or "wreck" or "beacon")
                Raylib.DrawCircleV(at, 2, location.Kind is "base" or "corridor" ? Amber : Muted);
        }
        if (hasWaypoint && pilot is not null)
        {
            var start = Map(radarCenter, map);
            var delta = Map(waypoint, map) - start;
            var fraction = Math.Min(
                1,
                Math.Min(
                    (map.Width / 2 - 9) / Math.Max(.001f, Math.Abs(delta.X)),
                    (map.Height / 2 - 9) / Math.Max(.001f, Math.Abs(delta.Y))
                )
            );
            var end = start + delta * fraction;
            Raylib.DrawLineEx(start, end, 2, Amber);
            Raylib.DrawCircleLines((int)end.X, (int)end.Y, 5, Amber);
            if (delta.LengthSquared() > 1)
            {
                var dir = Vector2.Normalize(delta);
                var side = new Vector2(-dir.Y, dir.X);
                Raylib.DrawLineEx(end, end - dir * 8 + side * 4, 2, Amber);
                Raylib.DrawLineEx(end, end - dir * 8 - side * 4, 2, Amber);
            }
        }
        if (snapshot is not null)
        {
            foreach (var drop in snapshot.Energy ?? [])
                Raylib.DrawCircleV(Map(new(drop.X, drop.Y), map), 1.5f, Cyan);
            var self = snapshot.Players.FirstOrDefault(p => p.Id == ownId);
            foreach (var enemy in snapshot.Enemies ?? [])
                if (
                    enemy.Hull > 0
                    && self is not null
                    && Vector2.DistanceSquared(new(self.X, self.Y), new(enemy.X, enemy.Y))
                        <= 1200 * 1200
                )
                    Raylib.DrawCircleV(Map(new(enemy.X, enemy.Y), map), 2, Enemy);
            foreach (var player in snapshot.Players)
            {
                if (player.Hull <= 0)
                    continue;
                var at = Map(new(player.X, player.Y), map);
                if (player.Id == ownId)
                {
                    Raylib.DrawCircleLines((int)at.X, (int)at.Y, 4, Cyan);
                    Raylib.DrawCircleV(at, 2.5f, Ink);
                }
                else
                    Raylib.DrawRectangle(
                        (int)at.X - 2,
                        (int)at.Y - 2,
                        4,
                        4,
                        player.Connected ? Pink : Muted
                    );
            }
        }
        Raylib.EndScissorMode();
        var y = (int)(map.Y + map.Height - 12);
        var x = (int)(map.X + (map.Width - 180) / 2);
        Raylib.DrawRectangle(x - 4, y - 2, 188, 14, new Color(5, 12, 17, 225));
        DotLabel(x + 3, y, Ink, "YOU");
        DotLabel(x + 48, y, Pink, "PILOT");
        DotLabel(x + 104, y, Enemy, "NPC");
        DotLabel(x + 150, y, Cyan, "XP");
    }

    private static Vector2 Map(Vector2 world, Rectangle map)
    {
        return new Vector2(map.X + map.Width / 2, map.Y + map.Height / 2)
            + (world - radarCenter) * (map.Width / radarWidth);
    }
}
