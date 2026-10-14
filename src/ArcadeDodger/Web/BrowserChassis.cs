using System.Numerics;
using ArcadeDodger.Multiplayer;
using Raylib_cs;

namespace ArcadeDodger.Web;

internal sealed partial class BrowserArenaWorld
{
    private static void DrawChassis(
        int chassis,
        Vector2 at,
        float heading,
        float aim,
        float throttle,
        int bays,
        Color color,
        bool low,
        float scale = 1
    )
    {
        var design = ChassisCatalog.Get(chassis);
        Vector2 World(Vector2 local) => at + ChassisCatalog.Rotate(local * scale, heading);
        DrawPanels(design.Panels, at, heading, color, scale);
        foreach (var panel in design.Panels)
        {
            if (panel.Material == "engine" && throttle > 0)
            {
                var aft = World(
                    panel.Vertices[0]
                        + new Vector2(0, (panel.Vertices[3].Y - panel.Vertices[0].Y) / 2)
                );
                var phase = (float)Raylib.GetTime() * 24 + at.X * .03f + panel.Vertices[0].Y;
                var flicker = low
                    ? 1
                    : 1 + .12f * MathF.Sin(phase) + .06f * MathF.Sin(phase * 2.7f);
                var length = (4 + 24 * throttle) * flicker * scale;
                var tail = aft + ChassisCatalog.Rotate(new Vector2(-length, 0), heading);
                var side = ChassisCatalog.Rotate(new Vector2(0, (2 + throttle) * scale), heading);
                var cyan = new Color(65, 225, 255, 220);
                var blue = new Color(65, 125, 255, 65);
                if (!low)
                {
                    Raylib.DrawLineEx(
                        aft,
                        tail + ChassisCatalog.Rotate(new(-length * .5f, 0), heading),
                        7 * scale,
                        blue
                    );
                    Raylib.DrawCircleV(aft, 6 * scale, Raylib.Fade(cyan, .15f));
                }
                Raylib.DrawTriangle(aft + side, tail, aft - side, cyan);
                Raylib.DrawLineEx(
                    aft,
                    aft + (tail - aft) * .45f,
                    2 * scale,
                    new Color(235, 255, 255, 255)
                );
            }
        }
        for (var i = 0; i < 4; i++)
            if ((bays & (1 << i)) != 0)
            {
                var mount = World(design.Hardpoints[i]);
                Raylib.DrawCircleV(mount, 3 * scale, color);
                Raylib.DrawLineEx(
                    mount,
                    mount + new Vector2(MathF.Cos(aim), MathF.Sin(aim)) * 11 * scale,
                    2 * scale,
                    NeonRendering.Ink
                );
            }
    }

    private static void DrawPanels(
        HullPanel[] panels,
        Vector2 at,
        float heading,
        Color color,
        float scale = 1
    )
    {
        Vector2 World(Vector2 local) => at + ChassisCatalog.Rotate(local * scale, heading);
        foreach (var panel in panels)
        {
            var fill = panel.Material switch
            {
                "bridge" => Raylib.Fade(NeonRendering.Ink, color.A / 255f),
                "engine" => Raylib.Fade(new Color(90, 200, 230, 220), color.A / 255f),
                "armor" => Raylib.Fade(color, .45f),
                _ => Raylib.Fade(color, .65f),
            };
            var root = World(panel.Vertices[0]);
            for (var i = 1; i < panel.Vertices.Length - 1; i++)
                Raylib.DrawTriangle(
                    root,
                    World(panel.Vertices[i + 1]),
                    World(panel.Vertices[i]),
                    fill
                );
            for (var i = 0; i < panel.Vertices.Length; i++)
                Raylib.DrawLineEx(
                    World(panel.Vertices[i]),
                    World(panel.Vertices[(i + 1) % panel.Vertices.Length]),
                    1,
                    color
                );
        }
    }
}
