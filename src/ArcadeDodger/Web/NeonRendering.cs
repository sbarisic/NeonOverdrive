using System.Numerics;
using Raylib_cs;

namespace ArcadeDodger.Web;

// WebAssembly arena presentation helpers.
internal static class NeonRendering
{
    private static readonly Color PlayerColor = new(65, 245, 235, 255);
    internal static readonly Color Ink = new(235, 240, 250, 255);
    private static readonly Color ConsoleFill = new(4, 14, 22, 238);
    private static readonly Color ConsoleBorder = new(40, 112, 132, 255);

    internal static void DrawBackdrop(
        float clock,
        Vector2 cameraOrigin,
        int width,
        int height,
        Vector2 viewport
    )
    {
        var view = viewport;
        for (int i = 0; i < 600; i++)
        {
            float x = (i * 137 + 31) % width;
            float y = (i * 83 + clock * (8 + i % 4 * 7)) % height;
            if (
                x < cameraOrigin.X - 40
                || x > cameraOrigin.X + view.X + 40
                || y < cameraOrigin.Y - 40
                || y > cameraOrigin.Y + view.Y + 40
            )
                continue;
            float brightness = 0.25f + 0.3f * (1 + MathF.Sin(clock * 2 + i));
            Raylib.DrawCircleV(
                new Vector2(x, y),
                i % 5 == 0 ? 1.5f : 0.7f,
                Raylib.Fade(PlayerColor, brightness)
            );
        }
        for (int x = 0; x <= width; x += 60)
            Raylib.DrawLine(x, 0, x, height, new Color(19, 30, 55, 255));
        for (float y = 0; y < height; y += 60)
            Raylib.DrawLine(0, (int)y, width, (int)y, new Color(24, 34, 62, 255));
        Raylib.DrawRectangleGradientV(
            0,
            0,
            width,
            height,
            new Color(30, 10, 65, 45),
            new Color(0, 180, 190, 18)
        );
        Raylib.DrawRectangleLinesEx(new Rectangle(2, 2, width - 4, height - 4), 3, PlayerColor);
    }

    internal static void DrawConsolePanel(int x, int y, int width, int height, Color accent)
    {
        Raylib.DrawRectangle(x, y, width, height, ConsoleFill);
        Raylib.DrawRectangleLinesEx(new Rectangle(x, y, width, height), 1, ConsoleBorder);
        const int corner = 7;
        Raylib.DrawLine(x, y, x + corner, y, accent);
        Raylib.DrawLine(x, y, x, y + corner, accent);
        Raylib.DrawLine(x + width - corner, y, x + width - 1, y, accent);
        Raylib.DrawLine(x + width - 1, y, x + width - 1, y + corner, accent);
        Raylib.DrawLine(x, y + height - 1, x + corner, y + height - 1, accent);
        Raylib.DrawLine(x, y + height - corner, x, y + height - 1, accent);
        Raylib.DrawLine(x + width - corner, y + height - 1, x + width - 1, y + height - 1, accent);
        Raylib.DrawLine(x + width - 1, y + height - corner, x + width - 1, y + height - 1, accent);
    }

    internal static void GlowBox(Rectangle box, Color color)
    {
        for (int padding = 9; padding >= 3; padding -= 3)
            Raylib.DrawRectangleRec(
                new Rectangle(
                    box.X - padding,
                    box.Y - padding,
                    box.Width + padding * 2,
                    box.Height + padding * 2
                ),
                Raylib.Fade(color, 0.055f)
            );
        Raylib.DrawRectangleRec(
            new Rectangle(box.X, box.Y, box.Width, box.Height),
            Raylib.Fade(color, 0.65f)
        );
        Raylib.DrawRectangleLinesEx(new Rectangle(box.X, box.Y, box.Width, box.Height), 2, color);
        Raylib.DrawLine(
            (int)box.X + 4,
            (int)box.Y + 4,
            (int)(box.X + box.Width - 4),
            (int)box.Y + 4,
            Ink
        );
    }

    internal static void DrawProjectile(Vector2 position, Vector2 direction, Color color)
    {
        direction =
            direction.LengthSquared() < 0.001f ? -Vector2.UnitY : Vector2.Normalize(direction);
        var tail = position - direction * 18;
        Raylib.DrawLineEx(tail, position, 9, Raylib.Fade(color, 0.12f));
        Raylib.DrawLineEx(tail, position, 3, color);
        Raylib.DrawCircleV(position, 2, Ink);
    }

    internal static void DrawReticle(Vector2 aim, Color color)
    {
        Raylib.DrawCircleLines((int)aim.X, (int)aim.Y, 9, color);
        Raylib.DrawLine((int)aim.X - 14, (int)aim.Y, (int)aim.X - 5, (int)aim.Y, Ink);
        Raylib.DrawLine((int)aim.X + 5, (int)aim.Y, (int)aim.X + 14, (int)aim.Y, Ink);
    }

    internal static void DrawPlayerCore(Vector2 center, float clock, Color color)
    {
        Raylib.DrawRectangleRec(new Rectangle(center.X - 6, center.Y - 6, 12, 12), Ink);
        Raylib.DrawCircleV(center + new Vector2(0, 21), 3 + MathF.Sin(clock * 24), color);
    }
}
