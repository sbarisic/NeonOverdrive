using System.Numerics;
using Raylib_cs;

namespace ArcadeDodger.Web;

/// <summary>One antialiased atlas for HUD and world labels, measured with the same font used to draw.</summary>
internal static class BrowserText
{
    private static Font font;
    private static bool loaded;
    private const float Spacing = .35f;

    // TTF sizes include ascenders/descenders; preserve the old labels' visible cap height.
    private static float Size(int size) => size * 1.6f;

    public static void Load()
    {
        if (loaded)
            return;
        using var source =
            typeof(BrowserText).Assembly.GetManifestResourceStream("NeonOverdrive.Rajdhani.ttf")
            ?? throw new InvalidOperationException("Bundled HUD font is missing.");
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        // Include Latin names and the arrows/multiplication signs used by the objective HUD.
        var glyphs = Enumerable
            .Range(32, 224)
            .Concat(new[] { 0x2013, 0x2014, 0x2022, 0x2190, 0x2191, 0x2192, 0x2193 })
            .ToArray();
        font = Raylib.LoadFontFromMemory(".ttf", buffer.ToArray(), 48, glyphs, glyphs.Length);
        if (font.Texture.Id == Raylib.GetFontDefault().Texture.Id)
            throw new InvalidOperationException("Could not rasterize the HUD font.");
        var texture = font.Texture;
        Raylib.GenTextureMipmaps(ref texture);
        font.Texture = texture;
        Raylib.SetTextureFilter(font.Texture, TextureFilter.Trilinear);
        loaded = true;
    }

    public static void Release()
    {
        if (loaded)
            Raylib.UnloadFont(font);
        loaded = false;
    }

    public static void DrawText(string text, int x, int y, int size, Color color) =>
        Raylib.DrawTextEx(font, text, new Vector2(x, y - size * .16f), Size(size), Spacing, color);

    public static int MeasureText(string text, int size) =>
        (int)MathF.Ceiling(Raylib.MeasureTextEx(font, text, Size(size), Spacing).X);
}
