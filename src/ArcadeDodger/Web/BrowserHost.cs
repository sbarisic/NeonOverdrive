using System.Runtime.InteropServices.JavaScript;
using Raylib_cs;

namespace ArcadeDodger.Web;

public partial class BrowserHost
{
    private static bool initialized;
    private static BrowserArena? arena;

    [JSExport]
    public static void ActivateArena()
    {
        arena?.Release();
        arena = new BrowserArena();
    }

    [JSExport]
    public static void ArenaSector(string json) => arena?.SetSector(json);

    [JSExport]
    public static void ArenaWelcome(int id) => arena?.Welcome(id);

    [JSExport]
    public static void ArenaSnapshotPacked(
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] data,
        [JSMarshalAs<JSType.Array<JSType.String>>] string[] labels
    ) => arena?.ReceivePacked(data, labels);

    [JSExport]
    public static void ArenaSnapshot(string json) => arena?.Receive(json);

    [JSExport]
    public static double ArenaAim(double u, double v) => arena?.Aim(u, v) ?? 0;

    [JSExport]
    public static int ArenaPick(double u, double v) => arena?.Pick(u, v) ?? 0;

    [JSExport]
    public static void ArenaPresentation(bool menuOpen, bool reducedEffects) =>
        arena?.Presentation(menuOpen, reducedEffects);

    [JSExport]
    public static void ArenaRadar(double x, double y, bool active, double zoom) =>
        BrowserArenaHud.Radar(x, y, active, zoom);

    [JSExport]
    public static string ArenaRoute(double x, double y, double tx, double ty) =>
        BrowserArenaHud.Navigation(x, y, tx, ty);

    [JSExport]
    public static bool ArenaRoutePending() => BrowserArenaHud.NavigationPending;

    [JSExport]
    public static string ArenaHudLayout(double width, double height) =>
        BrowserArenaHud.Configure(width, height);

    [JSExport]
    public static void ArenaInput(double x, double y, double aim, bool menu, bool brake) =>
        arena?.Input(x, y, aim, menu, brake);

    [JSExport]
    public static void ArenaSentInput(int sequence) => arena?.SentInput(sequence);

    [JSExport]
    public static string ArenaMetrics() => arena?.Metrics() ?? "";

    [JSExport]
    public static string ArenaPosition() => arena?.Position() ?? "";

    public static void Main()
    {
        Raylib.InitWindow(1920, 1080, "Neon Overdrive");
        if (!Raylib.IsWindowReady())
            throw new InvalidOperationException("Raylib window initialization failed.");
        Raylib.SetWindowSize(1920, 1080); // Synchronize the initial GL viewport before CSS-sized frames.
        Raylib.SetTargetFPS(0); // Yield to requestAnimationFrame instead of blocking the browser.
        BrowserText.Load();
        initialized = true;
        ActivateArena();
    }

    [JSExport]
    public static bool UpdateFrame(double displayWidth, double displayHeight, double pixelRatio)
    {
        // Web mouse events are canvas-relative CSS pixels, not backing-buffer pixels.
        // Refresh on every frame so resizing/zooming also remaps a stationary cursor.
        if (displayWidth <= 0 || displayHeight <= 0)
            return initialized;
        if (arena is not null)
        {
            var density = Math.Clamp(pixelRatio, 1, 2);
            var width = Math.Max(1, (int)Math.Round(displayWidth * density));
            var height = Math.Max(1, (int)Math.Round(displayHeight * density));
            if (width != Raylib.GetScreenWidth() || height != Raylib.GetScreenHeight())
                Raylib.SetWindowSize(width, height);
        }
        Raylib.SetMouseScale(
            (float)(Raylib.GetScreenWidth() / displayWidth),
            (float)(Raylib.GetScreenHeight() / displayHeight)
        );
        return arena?.Frame() ?? false;
    }

    [JSExport]
    public static void Close()
    {
        arena?.Release();
        arena = null;
        if (initialized)
        {
            BrowserText.Release();
            Raylib.CloseWindow();
        }
        initialized = false;
    }
}
