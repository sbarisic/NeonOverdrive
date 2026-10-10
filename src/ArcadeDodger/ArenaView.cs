using System.Numerics;
namespace ArcadeDodger;

/// <summary>Display-independent, north-up camera and radar transforms.</summary>
public static class ArenaView
{
    public static Vector2 CameraOrigin(Box player) => new(
        Math.Clamp(player.X + player.Width / 2 - GameModel.Width / 2f, 0, GameModel.ArenaWidth - GameModel.Width),
        Math.Clamp(player.Y + player.Height / 2 - GameModel.Height / 2f, 0, GameModel.ArenaHeight - GameModel.Height));
    public static Vector2 ScreenToWorld(Vector2 pixels, Vector2 origin, Vector2 pixelShake) => (pixels - pixelShake) / 2 + origin;
    public static Vector2 WorldToScreen(Vector2 world, Vector2 origin, Vector2 pixelShake) => (world - origin) * 2 + pixelShake;
    public static Vector2 Map(Vector2 world, Box radar) => new(radar.X + world.X / GameModel.ArenaWidth * radar.Width, radar.Y + world.Y / GameModel.ArenaHeight * radar.Height);
    public static bool Visible(Vector2 world, Vector2 origin, float margin = 40) => world.X >= origin.X - margin && world.Y >= origin.Y - margin && world.X <= origin.X + GameModel.Width + margin && world.Y <= origin.Y + GameModel.Height + margin;
}
