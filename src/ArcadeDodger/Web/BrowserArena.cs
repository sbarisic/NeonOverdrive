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
    private readonly LocalPrediction prediction = new();
    private int ownId;
    private Vector2 origin;
    private Vector2 view = new(960, 540);
    private float scale = 2;
    private readonly BrowserArenaWorld world = new();
    private bool menuOpen, reducedEffects;
    public void Presentation(bool menu, bool reduced)
    {
        if (menu && !menuOpen)
            prediction.Input(0, 0, snapshot?.Players.FirstOrDefault(p => p.Id == ownId)?.Aim ?? 0,
                false, clock.Elapsed.TotalSeconds);
        menuOpen = menu; reducedEffects = reduced;
    }
    public void Welcome(int id) { ownId = id; prediction.Reset(); }
    public void Input(double x, double y, double aim, bool dash) =>
        prediction.Input(menuOpen ? 0 : (float)x, menuOpen ? 0 : (float)y, (float)aim,
            !menuOpen && dash, clock.Elapsed.TotalSeconds);
    public void SentInput(int sequence) => prediction.SentInput(sequence, clock.Elapsed.TotalSeconds);
    // Actual last-rendered position, not a JS transport estimate.
    public string Position()
    {
        var self = snapshot?.Players.FirstOrDefault(p => p.Id == ownId);
        return self is null ? "" : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{self.X},{self.Y}");
    }
    public void Receive(string json)
    {
        var next = JsonSerializer.Deserialize(json, SnapshotJson.Default.RoomSnapshot);
        if (next is null || next.V != ClientProtocol.Version || next.Type != "snapshot" ||
            next.Players.Length > Room.Capacity || next.Shots.Length > 256 || next.Enemies?.Length > Room.EnemyCapacity ||
            next.Credits?.Length > 128 || next.Mines?.Length > 128 || next.Energy?.Length > Room.EnergyCapacity || next.Beams?.Length > Room.BeamCapacity ||
            (playback.Latest is { } latest && next.Tick <= latest.Tick)) return;
        var seconds = clock.Elapsed.TotalSeconds;
        playback.Receive(next, seconds);
        var self = next.Players.FirstOrDefault(p => p.Id == ownId);
        if (self is null) prediction.Reset();
        else prediction.Receive(self, next.Tick, seconds);
    }

    public double Aim(double u, double v)
    {
        var self = snapshot?.Players.FirstOrDefault(p => p.Id == ownId);
        if (self is null) return 0;
        if (!double.IsFinite(u) || !double.IsFinite(v)) return 0;
        var target = origin + new Vector2((float)Math.Clamp(u, 0, 1) * view.X, (float)Math.Clamp(v, 0, 1) * view.Y);
        return Math.Atan2(target.Y - self.Y, target.X - self.X);
    }

    public bool Frame()
    {
        scale = MathF.Min(Raylib.GetScreenWidth() / 960f, Raylib.GetScreenHeight() / 540f);
        view = new(Raylib.GetScreenWidth() / scale, Raylib.GetScreenHeight() / scale);
        var seconds = clock.Elapsed.TotalSeconds;
        snapshot = playback.Sample(seconds);
        if (snapshot is not null && prediction.Sample(seconds) is { } predicted)
            snapshot = snapshot with { Players = snapshot.Players.Select(p =>
                p.Id == ownId ? predicted : p).ToArray() };
        if (snapshot is not null && playback.Latest is { } latest)
        {
            var aligned = (snapshot.Beams ?? []).Select(beam =>
            {
                var from = latest.Players.FirstOrDefault(p => p.Id == beam.Owner);
                var to = snapshot.Players.FirstOrDefault(p => p.Id == beam.Owner);
                if (from is null || to is null) return beam;
                var rotation = Matrix3x2.CreateRotation(to.Aim - from.Aim);
                Vector2 Align(float x, float y) => new Vector2(to.X, to.Y) + Vector2.Transform(new Vector2(x - from.X, y - from.Y), rotation);
                var start = Align(beam.X, beam.Y); var end = Align(beam.EndX, beam.EndY);
                return beam with { X = start.X, Y = start.Y, EndX = end.X, EndY = end.Y };
            }).ToArray();
            snapshot = snapshot with { Beams = aligned };
        }
        var self = snapshot?.Players.FirstOrDefault(p => p.Id == ownId);
        if (self is not null)
            origin = Vector2.Clamp(new Vector2(self.X, self.Y) - view / 2, Vector2.Zero,
                Vector2.Max(Vector2.Zero, new Vector2(Room.Width, Room.Height) - view));
        Raylib.BeginDrawing();
        try
        {
            Raylib.ClearBackground(new Color(6, 8, 23, 255));
            Raylib.BeginMode2D(new Camera2D { Zoom = scale, Target = origin });
            try
            {
                world.Draw(snapshot, ownId, origin, view, (float)clock.Elapsed.TotalSeconds, reducedEffects);
            }
            finally { Raylib.EndMode2D(); }
            BrowserArenaHud.Draw(snapshot, ownId, origin, view, scale, (float)clock.Elapsed.TotalSeconds, menuOpen, reducedEffects);
            Raylib.BeginMode2D(new Camera2D { Zoom = scale });
            try
            {
                if (self?.Hull > 0 && !menuOpen)
                    NeonRendering.DrawReticle(Raylib.GetMousePosition() / scale, new Color(65, 245, 235, 255));
            }
            finally { Raylib.EndMode2D(); }
        }
        finally { Raylib.EndDrawing(); }
        return true;
    }
}
