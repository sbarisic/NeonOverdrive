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
                var glow = ChassisCatalog.Rotate(new Vector2(-6 - 18 * throttle, 0), heading);
                Raylib.DrawLineEx(aft, aft + glow, 3 * scale, color);
                if (!low)
                    Raylib.DrawCircleV(aft, 7 * scale, Raylib.Fade(color, .12f));
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
                "bridge" => NeonRendering.Ink,
                "engine" => Raylib.Fade(color, .8f),
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
