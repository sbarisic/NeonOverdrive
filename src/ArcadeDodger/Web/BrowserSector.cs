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
                    Raylib.DrawText(
                        l.Name.ToUpperInvariant(),
                        (int)(p.X - l.Radius * .5f),
                        (int)(p.Y - l.Radius - 55),
                        26,
                        Raylib.Fade(tint, .85f)
                    );
                    break;
                case "station":
                    Raylib.DrawCircleV(p, 300, new Color(30, 160, 135, 12));
                    Raylib.DrawCircleLines((int)p.X, (int)p.Y, 300, new Color(40, 130, 120, 100));
                    Raylib.DrawCircleLines((int)p.X, (int)p.Y, 180, Raylib.Fade(Cyan, .25f));
                    for (var i = 0; i < 4; i++)
                    {
                        var arm = new Vector2(
                            MathF.Cos(i * MathF.PI / 2),
                            MathF.Sin(i * MathF.PI / 2)
                        );
                        Raylib.DrawLineEx(
                            p + arm * 40,
                            p + arm * 100,
                            25,
                            new Color(35, 58, 79, 255)
                        );
                        Raylib.DrawCircleV(p + arm * 100, 7, Cyan);
                    }
                    Raylib.DrawPoly(p, 6, 58, 30, new Color(22, 38, 56, 255));
                    Raylib.DrawPolyLinesEx(p, 6, 58, 30, 3, Cyan);
                    Raylib.DrawCircleV(p, 20, Raylib.Fade(Cyan, .4f));
                    Raylib.DrawText(l.Name, (int)p.X - 95, (int)p.Y - 145, 18, Cyan);
                    Raylib.DrawText(
                        "E / DOCK   SANCTUARY",
                        (int)p.X - 94,
                        (int)p.Y + 130,
                        12,
                        Cyan
                    );
                    break;
                case "base":
                    var destroyed = state?.State == "destroyed";
                    var c = destroyed ? MutedMine : Hostile;
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
                    Raylib.DrawText(
                        destroyed ? "ARRAY DESTROYED" : l.Name,
                        (int)p.X - 110,
                        (int)p.Y - 110,
                        14,
                        c
                    );
                    if (!destroyed)
                    {
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
                    Raylib.DrawText("HOSTILE ARRIVAL", (int)p.X - 90, (int)p.Y - 140, 16, Hostile);
                    break;
                case "wreck":
                    Raylib.DrawPoly(p, 5, 40, 25, new Color(60, 63, 76, 255));
                    Raylib.DrawLineEx(p - new Vector2(30, 20), p + new Vector2(20, 35), 4, Gold);
                    Raylib.DrawText("SALVAGE", (int)p.X - 35, (int)p.Y - 62, 12, Gold);
                    break;
                case "beacon":
                    Raylib.DrawPoly(p, 4, 18, 0, Cyan);
                    Raylib.DrawCircleLines(
                        (int)p.X,
                        (int)p.Y,
                        reduced ? 40 : 40 + MathF.Sin(time * 2) * 8,
                        Cyan
                    );
                    break;
                case "thermal":
                    var phase = snapshot.Tick % 300;
                    if (phase < 180)
                    {
                        var hot = phase >= 60;
                        Raylib.DrawCircleV(p, l.Radius, new Color(250, 80, 25, hot ? 55 : 18));
                        Raylib.DrawCircleLines((int)p.X, (int)p.Y, l.Radius, Gold);
                        Raylib.DrawText(
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
                Raylib.DrawText(
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
    }
}
