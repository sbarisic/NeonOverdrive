using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArcadeDodger.Multiplayer;
using Raylib_cs;

namespace ArcadeDodger.Web;

[JsonSerializable(typeof(RoomSnapshot))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class SnapshotJson : JsonSerializerContext { }

// Isolated network-slice presentation. Single-player GameApp and model remain untouched.
internal sealed class BrowserArena
{
    private RoomSnapshot? snapshot;
    private readonly SnapshotPlayback playback = new();
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    private int ownId;
    private Vector2 origin;
    private readonly BrowserArenaWorld world = new();
    private bool menuOpen, reducedEffects;
    public void Presentation(bool menu, bool reduced) { menuOpen = menu; reducedEffects = reduced; }
    public void Welcome(int id) => ownId = id;
    public void Receive(string json)
    {
        var next = JsonSerializer.Deserialize(json, SnapshotJson.Default.RoomSnapshot);
        if (next is null || next.V != ClientProtocol.Version || next.Type != "snapshot" ||
            next.Players.Length > Room.Capacity || next.Shots.Length > 256 || next.Enemies?.Length > Room.EnemyCapacity ||
            next.Energy?.Length > Room.EnergyCapacity ||
            (playback.Latest is { } latest && next.Tick <= latest.Tick)) return;
        playback.Receive(next, clock.Elapsed.TotalSeconds);
    }

    public double Aim(double u, double v)
    {
        var self = snapshot?.Players.FirstOrDefault(p => p.Id == ownId);
        if (self is null) return 0;
        var target = origin + new Vector2((float)u * 960, (float)v * 540);
        return Math.Atan2(target.Y - self.Y, target.X - self.X);
    }

    public bool Frame()
    {
        snapshot = playback.Sample(clock.Elapsed.TotalSeconds);
        var self = snapshot?.Players.FirstOrDefault(p => p.Id == ownId);
        if (self is not null)
            origin = Vector2.Clamp(new Vector2(self.X - 480, self.Y - 270), Vector2.Zero,
                new Vector2(Room.Width - 960, Room.Height - 540));
        Raylib.BeginDrawing();
        try
        {
            Raylib.ClearBackground(new Color(6, 8, 23, 255));
            Raylib.BeginMode2D(new Camera2D { Zoom = 2, Target = origin });
            try
            {
                world.Draw(snapshot, ownId, origin, (float)clock.Elapsed.TotalSeconds, reducedEffects);
            }
            finally { Raylib.EndMode2D(); }
            Raylib.BeginMode2D(new Camera2D { Zoom = 2 });
            try
            {
                BrowserArenaHud.Draw(snapshot, ownId, origin, (float)clock.Elapsed.TotalSeconds, menuOpen, reducedEffects);
                if (self?.Hull > 0 && !menuOpen)
                    NeonRendering.DrawReticle(Raylib.GetMousePosition() / 2, new Color(65, 245, 235, 255));
            }
            finally { Raylib.EndMode2D(); }
        }
        finally { Raylib.EndDrawing(); }
        return true;
    }
}
