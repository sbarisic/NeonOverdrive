using System.Numerics;
using ArcadeDodger.Multiplayer;
using Raylib_cs;

namespace ArcadeDodger.Web;

internal sealed partial class BrowserArenaWorld
{
    private SectorDefinition? sector;
    private readonly Dictionary<int, Texture2D> planets = [];

    public void ReleaseSector()
    {
        foreach (var texture in planets.Values)
            Raylib.UnloadTexture(texture);
        planets.Clear();
    }

    public void SetSector(SectorDefinition definition)
    {
        ReleaseSector();
        sector = definition;
    }

    private Texture2D PlanetTexture(SectorLocation location)
    {
        if (planets.TryGetValue(location.Id, out var texture))
            return texture;
        const int size = 256;
        var image = Raylib.GenImageColor(size, size, new Color(0, 0, 0, 0));
        var phase = (sector!.Seed % 997) * .01f + location.Id;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var nx = (x - 127.5f) / 127.5f;
            var ny = (y - 127.5f) / 127.5f;
            var r = nx * nx + ny * ny;
            if (r > 1)
                continue;
            var z = MathF.Sqrt(1 - r);
            var longitude = MathF.Atan2(nx, z);
            var latitude = MathF.Asin(ny);
            var terrain =
                MathF.Sin(longitude * 7 + phase + MathF.Sin(latitude * 9))
                    * MathF.Cos(latitude * 11 + phase)
                + .35f * MathF.Sin(longitude * 23 + latitude * 17);
            var light = .18f + .62f * MathF.Max(0, -nx * .48f - ny * .35f + z * .7f);
            float red,
                green,
                blue;
            if (location.Biome == "inhabited")
            {
                var land = terrain > .25f;
                red = land ? 40 : 20;
                green = land ? 108 : 62;
                blue = land ? 73 : 109;
                if (MathF.Abs(ny) > .91f)
                {
                    red = 133;
                    green = 160;
                    blue = 172;
                }
                var cloud = MathF.Sin(longitude * 17 + latitude * 21 + terrain * 2);
                if (cloud > .91f)
                {
                    red += 32;
                    green += 37;
                    blue += 40;
                }
            }
            else if (location.Biome == "volcanic")
            {
                var lava = MathF.Abs(terrain) < .10f;
                red = lava ? 235 : 72;
                green = lava ? 86 : 33;
                blue = lava ? 19 : 29;
                if (lava)
                    light = MathF.Max(.65f, light);
            }
            else if (location.Biome == "gas")
            {
                var band = MathF.Sin(latitude * 30 + MathF.Sin(longitude * 5) * .65f);
                red = 100 + band * 25;
                green = 85 + band * 20;
                blue = 140 + band * 28;
            }
            else
            {
                var crack = MathF.Abs(terrain) < .07f;
                red = crack ? 12 : 95 + terrain * 14;
                green = crack ? 17 : 101 + terrain * 14;
                blue = crack ? 28 : 121 + terrain * 14;
            }
            Raylib.ImageDrawPixel(
                ref image,
                x,
                y,
                new Color((int)(red * light), (int)(green * light), (int)(blue * light), 255)
            );
        }
        texture = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);
        Raylib.SetTextureFilter(texture, TextureFilter.Bilinear);
        planets[location.Id] = texture;
        return texture;
    }

    private void DrawSector(RoomSnapshot snapshot, Vector2 origin, float time, bool reduced)
    {
        if (sector is null)
            return;
        foreach (var l in sector.Locations)
        {
            var p = l.Position;
            if (!Visible(p, origin, l.Kind == "planet" ? l.Radius + 200 : 380))
                continue;
            var state = snapshot.Sector?.Locations.FirstOrDefault(s => s.Id == l.Id);
            switch (l.Kind)
            {
                case "cache":
                    var searched = snapshot.Sector?.Services?.ClaimedCaches.Contains(l.Id) == true;
                    Raylib.DrawPolyLinesEx(p, 4, 16, 45, 2, searched ? MutedMine : Gold);
                    BrowserText.DrawText(
                        searched ? "SEARCHED" : "E / SEARCH CACHE",
                        (int)p.X - 60,
                        (int)p.Y - 35,
                        12,
                        searched ? MutedMine : Gold
                    );
                    DrawScanProgress(snapshot, l, p - new Vector2(0, 70));
                    break;
                case "planet":
                    var tint = l.Biome switch
                    {
                        "volcanic" => new Color(180, 66, 30, 255),
                        "gas" => new Color(99, 92, 157, 255),
                        "shattered" => new Color(95, 106, 122, 255),
                        _ => new Color(30, 112, 137, 255),
                    };
                    Raylib.DrawCircleV(p, l.Radius + 18, Raylib.Fade(tint, .12f));
                    if (l.Biome == "gas")
                        for (var i = 0; i < 3; i++)
                            Raylib.DrawEllipseLines(
                                (int)p.X,
                                (int)p.Y,
                                l.Radius + 100 + i * 16,
                                l.Radius * .3f + i * 7,
                                Raylib.Fade(tint, .4f)
                            );
                    Raylib.DrawTexturePro(
                        PlanetTexture(l),
                        new Rectangle(0, 0, 256, 256),
                        new Rectangle(p.X - l.Radius, p.Y - l.Radius, l.Radius * 2, l.Radius * 2),
                        Vector2.Zero,
                        0,
                        new Color(255, 255, 255, 255)
                    );
                    Raylib.DrawCircleLines((int)p.X, (int)p.Y, l.Radius, Raylib.Fade(tint, .65f));
                    BrowserText.DrawText(
                        l.Name.ToUpperInvariant(),
                        (int)(p.X - l.Radius * .5f),
                        (int)(p.Y - l.Radius - 55),
                        26,
                        Raylib.Fade(tint, .85f)
                    );
                    break;
                case "station":
                    var owner = snapshot.Sector?.World?.Territory.FirstOrDefault(t => t.Id == l.Id);
                    var stationColor =
                        owner?.Owner == "hostile" ? Hostile
                        : owner?.Contested == true ? Gold
                        : Cyan;
                    if (owner?.Owner != "hostile" && owner?.Contested != true)
                    {
                        Raylib.DrawCircleV(p, 300, new Color(30, 160, 135, 12));
                        Raylib.DrawCircleLines(
                            (int)p.X,
                            (int)p.Y,
                            300,
                            new Color(40, 130, 120, 100)
                        );
                    }
                    Raylib.DrawCircleLines((int)p.X, (int)p.Y, 180, Raylib.Fade(Cyan, .25f));
                    DrawPanels(StructureCatalog.Station, p, 0, stationColor);
                    DrawStationCore(p, 28, stationColor, time, reduced);
                    BrowserText.DrawText(l.Name, (int)p.X - 95, (int)p.Y - 145, 18, stationColor);
                    BrowserText.DrawText(
                        owner?.Owner == "hostile" ? "HOSTILE CONTROL"
                            : owner?.Contested == true ? "CONTESTED / DOCK CLOSED"
                            : "E / DOCK   SANCTUARY",
                        (int)p.X - 94,
                        (int)p.Y + 130,
                        12,
                        Cyan
                    );
                    break;
                case "outpost":
                case "base":
                    var destroyed = state?.State == "destroyed";
                    var c =
                        destroyed ? MutedMine
                        : l.Faction == "friendly" ? Cyan
                        : Hostile;
                    Raylib.DrawPoly(p, 6, 85, 0, new Color(35, 19, 35, 255));
                    Raylib.DrawPolyLinesEx(p, 6, 85, 0, 4, c);
                    for (var i = 0; i < 3; i++)
                        Raylib.DrawRectangle(
                            (int)p.X - 55 + i * 40,
                            (int)p.Y - 22,
                            24,
                            44,
                            Raylib.Fade(c, .55f)
                        );
                    BrowserText.DrawText(
                        destroyed ? "ARRAY DESTROYED" : l.Name,
                        (int)p.X - 110,
                        (int)p.Y - 110,
                        14,
                        c
                    );
                    if (!destroyed)
                    {
                        DrawStationCore(p, 48, c, time, reduced);
                        Raylib.DrawRectangle((int)p.X - 60, (int)p.Y + 104, 120, 5, MutedMine);
                        Raylib.DrawRectangle(
                            (int)p.X - 60,
                            (int)p.Y + 104,
                            state?.Hull ?? 120,
                            5,
                            Hostile
                        );
                    }
                    break;
                case "corridor":
                    Raylib.DrawCircleLines((int)p.X, (int)p.Y, 110, Hostile);
                    BrowserText.DrawText("HOSTILE ARRIVAL", (int)p.X - 90, (int)p.Y - 140, 16, Hostile);
                    break;
                case "wreck":
                    Raylib.DrawPoly(p, 5, 40, 25, new Color(60, 63, 76, 255));
                    Raylib.DrawLineEx(p - new Vector2(30, 20), p + new Vector2(20, 35), 4, Gold);
                    BrowserText.DrawText("SALVAGE", (int)p.X - 35, (int)p.Y - 62, 12, Gold);
                    DrawScanProgress(snapshot, l, p - new Vector2(0, 92));
                    break;
                case "beacon":
                    Raylib.DrawPoly(p, 4, 18, 0, Cyan);
                    Raylib.DrawCircleLines(
                        (int)p.X,
                        (int)p.Y,
                        reduced ? 40 : 40 + MathF.Sin(time * 2) * 8,
                        Cyan
                    );
                    DrawScanProgress(snapshot, l, p - new Vector2(0, 70));
                    break;
                case "thermal":
                    var phase = snapshot.Tick % 300;
                    if (phase < 180)
                    {
                        var hot = phase >= 60;
                        Raylib.DrawCircleV(p, l.Radius, new Color(250, 80, 25, hot ? 55 : 18));
                        Raylib.DrawCircleLines((int)p.X, (int)p.Y, l.Radius, Gold);
                        BrowserText.DrawText(
                            hot ? "THERMAL ERUPTION" : "THERMAL WARNING",
                            (int)p.X - 80,
                            (int)p.Y,
                            14,
                            Gold
                        );
                    }
                    break;
            }
            if (state?.State == "launching")
            {
                Raylib.DrawCircleLines(
                    (int)p.X + 120,
                    (int)p.Y,
                    32 + (reduced ? 0 : MathF.Sin(time * 8) * 8),
                    Gold
                );
                BrowserText.DrawText(
                    $"LAUNCH {state.LaunchTicks / 30f:0.0}s",
                    (int)p.X - 65,
                    (int)p.Y + 145,
                    16,
                    Gold
                );
            }
        }
        foreach (var rock in sector.Obstacles)
        {
            if (!Visible(rock.Position, origin, rock.Radius + 10))
                continue;
            Raylib.DrawPoly(
                rock.Position,
                7,
                rock.Radius,
                rock.Id * 37,
                new Color(39, 47, 62, 255)
            );
            Raylib.DrawPolyLinesEx(
                rock.Position,
                7,
                rock.Radius,
                rock.Id * 37,
                2,
                new Color(101, 111, 130, 255)
            );
            Raylib.DrawPoly(
                rock.Position - new Vector2(rock.Radius * .2f),
                5,
                rock.Radius * .38f,
                rock.Id * 15,
                new Color(53, 62, 77, 255)
            );
        }
        foreach (var site in snapshot.Sector?.Services?.Outposts ?? [])
        foreach (var socket in site.Sockets)
        {
            Vector2 at = new(socket.X, socket.Y);
            if (socket.Hull > 0 || !Visible(at, origin, 45))
                continue;
            Raylib.DrawCircleLines((int)at.X, (int)at.Y, 26, Raylib.Fade(Cyan, .3f));
            BrowserText.DrawText(
                new[] { "CORE", "TURRET 1", "TURRET 2", "REPAIR" }[socket.Slot],
                (int)at.X - 24,
                (int)at.Y - 5,
                10,
                MutedMine
            );
        }
    }

    private static void DrawScanProgress(RoomSnapshot snapshot, SectorLocation location, Vector2 p)
    {
        var scan = snapshot.Sector?.Pilot;
        if (scan is null || scan.InteractionTarget != location.Id)
            return;
        var progress = Math.Clamp(scan.Interaction, 0, 1);
        const int width = 148;
        var x = (int)p.X - width / 2;
        var y = (int)p.Y;
        Raylib.DrawRectangle(x - 7, y - 24, width + 14, 44, new Color(4, 12, 23, 235));
        var label = $"SCANNING {(int)(progress * 100)}%";
        BrowserText.DrawText(label, (int)p.X - BrowserText.MeasureText(label, 14) / 2, y - 19, 14, Cyan);
        Raylib.DrawRectangle(x, y, width, 10, new Color(33, 52, 66, 255));
        Raylib.DrawRectangle(x, y, (int)(width * progress), 10, Cyan);
        Raylib.DrawRectangleLines(x - 1, y - 1, width + 2, 12, Raylib.Fade(Cyan, .65f));
    }

    private static void DrawStationCore(
        Vector2 p,
        float radius,
        Color color,
        float time,
        bool reduced
    )
    {
        var pulse = reduced ? 1 : 1 + MathF.Sin(time * 2.4f) * .06f;
        Raylib.DrawCircleV(p, radius * 1.25f, Raylib.Fade(color, .09f));
        Raylib.DrawCircleV(p, radius, new Color(9, 20, 33, 235));
        Raylib.DrawCircleV(p, radius * .8f * pulse, Raylib.Fade(color, .23f));
        Raylib.DrawCircleLines((int)p.X, (int)p.Y, radius, Raylib.Fade(color, .65f));
        var angle = reduced ? 30 : time * 18;
        Raylib.DrawPolyLinesEx(p, 6, radius * .7f, angle, 2, color);
        Raylib.DrawPoly(p, 6, radius * .34f * pulse, -angle, Raylib.Fade(color, .85f));
        Raylib.DrawCircleV(p, radius * .14f, NeonRendering.Ink);
    }
}
