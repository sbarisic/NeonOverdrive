using System.Runtime.InteropServices.JavaScript;
using Raylib_cs;

namespace ArcadeDodger.Web;

public partial class BrowserHost
{
    private static GameApp? app;
    private static BrowserArena? arena;

    [JSExport]
    public static void ActivateArena() {arena?.Release();arena = new BrowserArena();}

    [JSExport]
    public static void ArenaSector(string json)=>arena?.SetSector(json);

    [JSExport]
    public static void ArenaWelcome(int id) => arena?.Welcome(id);

    [JSExport]
    public static void ArenaSnapshotPacked([JSMarshalAs<JSType.Array<JSType.Number>>] double[] data,[JSMarshalAs<JSType.Array<JSType.String>>] string[] labels)=>arena?.ReceivePacked(data,labels);

    [JSExport]
    public static void ArenaSnapshot(string json) => arena?.Receive(json);

    [JSExport]
    public static double ArenaAim(double u, double v) => arena?.Aim(u, v) ?? 0;

    [JSExport]
    public static void ArenaPresentation(bool menuOpen, bool reducedEffects) => arena?.Presentation(menuOpen, reducedEffects);

    [JSExport]
    public static void ArenaInput(double x, double y, double aim, bool dash) => arena?.Input(x, y, aim, dash);

    [JSExport]
    public static void ArenaSentInput(int sequence) => arena?.SentInput(sequence);

    [JSExport]
    public static string ArenaMetrics()=>arena?.Metrics()??"";

    [JSExport]
    public static string ArenaPosition() => arena?.Position() ?? "";

    public static void Main()
    {
        app = new GameApp();
        app.Initialize();
    }

    [JSExport]
    public static bool UpdateFrame(double displayWidth, double displayHeight, double pixelRatio)
    {
        // Web mouse events are canvas-relative CSS pixels, not backing-buffer pixels.
        // Refresh on every frame so resizing/zooming also remaps a stationary cursor.
        if (displayWidth <= 0 || displayHeight <= 0) return app is not null;
        if (arena is not null)
        {
            var density = Math.Clamp(pixelRatio, 1, 2);
            var width = Math.Max(1, (int)Math.Round(displayWidth * density));
            var height = Math.Max(1, (int)Math.Round(displayHeight * density));
            if (width != Raylib.GetScreenWidth() || height != Raylib.GetScreenHeight())
                Raylib.SetWindowSize(width, height);
        }
        Raylib.SetMouseScale((float)(Raylib.GetScreenWidth() / displayWidth),
            (float)(Raylib.GetScreenHeight() / displayHeight));
        return arena?.Frame() ?? app?.UpdateFrame() ?? false;
    }

    [JSExport]
    public static void Close()
    {
        app?.Close();
        app = null;
        arena?.Release();arena = null;
    }
}
