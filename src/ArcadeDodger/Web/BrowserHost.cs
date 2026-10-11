using System.Runtime.InteropServices.JavaScript;
using Raylib_cs;

namespace ArcadeDodger.Web;

public partial class BrowserHost
{
    private static GameApp? app;
    private static BrowserArena? arena;

    [JSExport]
    public static void ActivateArena() => arena = new BrowserArena();

    [JSExport]
    public static void ArenaWelcome(int id) => arena?.Welcome(id);

    [JSExport]
    public static void ArenaSnapshot(string json) => arena?.Receive(json);

    [JSExport]
    public static double ArenaAim(double u, double v) => arena?.Aim(u, v) ?? 0;

    [JSExport]
    public static void ArenaPresentation(bool menuOpen, bool reducedEffects) => arena?.Presentation(menuOpen, reducedEffects);

    public static void Main()
    {
        app = new GameApp();
        app.Initialize();
    }

    [JSExport]
    public static bool UpdateFrame(double displayWidth, double displayHeight)
    {
        // Web mouse events are canvas-relative CSS pixels, not backing-buffer pixels.
        // Refresh on every frame so resizing/zooming also remaps a stationary cursor.
        if (displayWidth <= 0 || displayHeight <= 0) return app is not null;
        Raylib.SetMouseScale((float)(Raylib.GetScreenWidth() / displayWidth),
            (float)(Raylib.GetScreenHeight() / displayHeight));
        return arena?.Frame() ?? app?.UpdateFrame() ?? false;
    }

    [JSExport]
    public static void Close()
    {
        app?.Close();
        app = null;
        arena = null;
    }
}
