using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArcadeDodger.Multiplayer;
using Raylib_cs;

namespace ArcadeDodger.Web;

[JsonSerializable(typeof(RoomSnapshot))]
[JsonSerializable(typeof(SectorDefinition))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class SnapshotJson : JsonSerializerContext { }

// WebAssembly presentation of the authoritative multiplayer sector.
internal sealed class BrowserArena
{
    private RoomSnapshot? snapshot;
    private readonly SnapshotPlayback playback = new();
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly LocalPrediction prediction = new();
    private int ownId;
    private Vector2 origin;
    private Vector2 view = new(960, 540);
    private const float WorldZoom = .75f;
    private float scale = 2;
    private readonly BrowserArenaWorld world = new();
    private bool menuOpen, reducedEffects;
    private double receiveMs,playbackMs,renderMs;
    private static double Milliseconds(long start)=>System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    public string Metrics()=>System.FormattableString.Invariant($"{receiveMs},{playbackMs},{renderMs}");
    public void Presentation(bool menu, bool reduced)
    {
        if (menu && !menuOpen)
            prediction.Input(0, 0, snapshot?.Players.FirstOrDefault(p => p.Id == ownId)?.Aim ?? 0,
                true, clock.Elapsed.TotalSeconds);
        menuOpen = menu; reducedEffects = reduced;
    }
    public void Release()=>world.ReleaseSector();
    public void SetSector(string json) {var sector=JsonSerializer.Deserialize(json,SnapshotJson.Default.SectorDefinition);if(sector is not null){prediction.SetSector(sector);world.SetSector(sector);BrowserArenaHud.SetSector(sector);}}
    public void Welcome(int id) { ownId = id; prediction.Reset(); }
    public void Input(double x, double y, double aim, bool menu) =>
        prediction.Input(menuOpen ? 0 : (float)x, menuOpen ? 0 : (float)y, (float)aim,
            menuOpen || menu, clock.Elapsed.TotalSeconds);
    public void SentInput(int sequence) => prediction.SentInput(sequence, clock.Elapsed.TotalSeconds);
    // Actual last-rendered position, not a JS transport estimate.
    public string Position()
    {
        var self = snapshot?.Players.FirstOrDefault(p => p.Id == ownId);
        return self is null ? "" : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{self.X},{self.Y}");
    }
    public void Receive(string json)
    {
        var started=System.Diagnostics.Stopwatch.GetTimestamp();
        Accept(JsonSerializer.Deserialize(json, SnapshotJson.Default.RoomSnapshot));receiveMs=Milliseconds(started);
    }
    public void ReceivePacked(double[] data,string[] labels)
    {
        var started=System.Diagnostics.Stopwatch.GetTimestamp();
        try {Accept(new PackedSnapshot(data,labels).Read());}
        catch(FormatException) {return;}catch(OverflowException){return;}
        receiveMs=Milliseconds(started);
    }
    private void Accept(RoomSnapshot? next)
    {
        if (next is null || next.V != ClientProtocol.Version || next.Type != "snapshot" ||
            next.Players.Length > Room.Capacity || next.Shots.Length > 1024 || next.Enemies?.Length > 256 ||
            next.ModuleDrops?.Length > 128 || next.Credits?.Length > 128 || next.Mines?.Length > 128 || next.Energy?.Length > Room.EnergyCapacity || next.Beams?.Length > Room.BeamCapacity ||
            (playback.Latest is { } latest && next.Tick <= latest.Tick)) return;
        var center=next.Players.FirstOrDefault(p=>p.Id==ownId);
        if(center is not null)next=next with {Shots=next.Shots.Where(s=>Math.Abs(s.X-center.X)<view.X/2+250&&Math.Abs(s.Y-center.Y)<view.Y/2+250).ToArray()};
        var seconds = clock.Elapsed.TotalSeconds;playback.Receive(next, seconds);
        var self = next.Players.FirstOrDefault(p => p.Id == ownId);
        if (self is null) prediction.Reset();else prediction.Receive(self, next.Tick, seconds);
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
        var hudScale = MathF.Min(Raylib.GetScreenWidth() / 960f, Raylib.GetScreenHeight() / 540f);
        var hudView = new Vector2(Raylib.GetScreenWidth() / hudScale, Raylib.GetScreenHeight() / hudScale);
        scale = hudScale * WorldZoom;
        view = new(Raylib.GetScreenWidth() / scale, Raylib.GetScreenHeight() / scale);
        var seconds = clock.Elapsed.TotalSeconds;
        var started=System.Diagnostics.Stopwatch.GetTimestamp();
        snapshot = playback.Sample(seconds);
        if (snapshot is not null && prediction.Sample(seconds) is { } predicted)
            snapshot = snapshot with { Players = snapshot.Players.Select(p =>
                p.Id == ownId ? predicted : p).ToArray() };
        if (snapshot is not null && playback.Latest is { } latest)
        {
            var transforms=new Dictionary<int,(Vector2 From,Vector2 To,Matrix3x2 Rotation)>();
            foreach(var player in snapshot.Players)
            {
                var from=latest.Players.FirstOrDefault(p=>p.Id==player.Id);
                if(from is null||from.X==player.X&&from.Y==player.Y&&from.Aim==player.Aim)continue;
                transforms[player.Id]=(new(from.X,from.Y),new(player.X,player.Y),Matrix3x2.CreateRotation(player.Aim-from.Aim));
            }
            if(transforms.Count>0)
                snapshot=snapshot with {Beams=(snapshot.Beams??[]).Select(beam=>
                {
                    if(!transforms.TryGetValue(beam.Owner,out var transform))return beam;
                    var start=transform.To+Vector2.Transform(new Vector2(beam.X,beam.Y)-transform.From,transform.Rotation);
                    var end=transform.To+Vector2.Transform(new Vector2(beam.EndX,beam.EndY)-transform.From,transform.Rotation);
                    return beam with {X=start.X,Y=start.Y,EndX=end.X,EndY=end.Y};
                }).ToArray()};
        }

        var self = snapshot?.Players.FirstOrDefault(p => p.Id == ownId);
        if (self is not null)
            origin = Vector2.Clamp(new Vector2(self.X, self.Y) - view / 2, Vector2.Zero,
                Vector2.Max(Vector2.Zero, new Vector2(Room.Width, Room.Height) - view));
        playbackMs=Milliseconds(started);started=System.Diagnostics.Stopwatch.GetTimestamp();
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
            BrowserArenaHud.Draw(snapshot, ownId, origin, hudView, view, hudScale, (float)clock.Elapsed.TotalSeconds, menuOpen, reducedEffects);
            Raylib.BeginMode2D(new Camera2D { Zoom = hudScale });
            try
            {
                if (self?.Hull > 0 && !menuOpen)
                    NeonRendering.DrawReticle(Raylib.GetMousePosition() / hudScale, new Color(65, 245, 235, 255));
            }
            finally { Raylib.EndMode2D(); }
        }
        finally { Raylib.EndDrawing();renderMs=Milliseconds(started); }
        return true;
    }
}
