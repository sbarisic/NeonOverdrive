using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using ArcadeDodger.Multiplayer;

namespace ArcadeDodger.Server;

public sealed class Peer
{
    public int PlayerId { get; set; }
    public string RoomId { get; set; } = "";
    public bool Closed => Volatile.Read(ref closed) != 0;
    private int closed;
    private InputMessage? latest;
    private RespawnMessage? respawn;
    public void Submit(RespawnMessage request) => Interlocked.Exchange(ref respawn, request);
    public RespawnMessage? TakeRespawn() => Interlocked.Exchange(ref respawn, null);
    public void Close() => Interlocked.Exchange(ref closed, 1);
    public void Submit(InputMessage input) => Interlocked.Exchange(ref latest, input);
    public InputMessage? TakeInput() => Interlocked.Exchange(ref latest, null);
    public Channel<byte[]> Snapshots { get; } = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1)
    { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = true });
}

/// <summary>Only this service's tick loop writes simulation state. Peers have bounded mailboxes.</summary>
public sealed class ArenaService : BackgroundService
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record JoinRequest(JoinMessage Message, Peer Peer, TaskCompletionSource<string?> Result);
    private sealed class Arena
    {
        public Room Model { get; } = new();
        public List<Peer> Peers { get; } = new();
    }
    private readonly Dictionary<string, Arena> arenas = new();
    private readonly Channel<JoinRequest> joins = Channel.CreateBounded<JoinRequest>(256);

    public async Task<string?> JoinAsync(JoinMessage message, Peer peer, CancellationToken token)
    {
        var result = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!joins.Writer.TryWrite(new(message, peer, result))) return "server_busy";
        return await result.Task.WaitAsync(token);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1d / Room.TickRate));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                // Bound admission work per tick even under a sustained connection flood.
                for (var i = 0; i < 32 && joins.Reader.TryRead(out var request); i++) Admit(request);
                foreach (var (id, arena) in arenas.ToArray())
                {
                    foreach (var peer in arena.Peers.ToArray())
                    {
                        if (peer.Closed)
                        {
                            arena.Model.Disconnect(peer.PlayerId);
                            arena.Peers.Remove(peer);
                            peer.Snapshots.Writer.TryComplete();
                        }
                        else
                        {
                            var input = peer.TakeInput();
                            var respawn = peer.TakeRespawn();
                            if (input is not null && (respawn is null || input.Sequence < respawn.Sequence))
                                arena.Model.Input(peer.PlayerId, input);
                            if (respawn is not null) arena.Model.Respawn(peer.PlayerId, respawn);
                            if (input is not null && respawn is not null && input.Sequence > respawn.Sequence)
                                arena.Model.Input(peer.PlayerId, input);
                        }
                    }
                    arena.Model.Step();
                    if (arena.Model.Tick % 2 == 0 && arena.Peers.Count > 0)
                    {
                        var snapshot = JsonSerializer.SerializeToUtf8Bytes(arena.Model.Snapshot(), Json);
                        foreach (var peer in arena.Peers) peer.Snapshots.Writer.TryWrite(snapshot);
                    }
                    if (arena.Model.PlayerCount == 0) arenas.Remove(id);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            joins.Writer.TryComplete();
            while (joins.Reader.TryRead(out var pending)) pending.Result.TrySetResult("server_stopping");
            foreach (var peer in arenas.Values.SelectMany(a => a.Peers)) peer.Snapshots.Writer.TryComplete();
        }
    }

    private void Admit(JoinRequest request)
    {
        if (request.Peer.Closed) { request.Result.TrySetResult("cancelled"); return; }
        var id = request.Message.Room;
        Arena? arena;
        if (id.Length > 0)
        {
            if (!arenas.TryGetValue(id, out arena)) { request.Result.TrySetResult("room_missing"); return; }
        }
        else
        {
            var available = arenas.FirstOrDefault(a => a.Value.Model.PlayerCount < Room.Capacity);
            id = available.Key;
            arena = available.Value;
            if (arena is null)
            {
                if (arenas.Count >= 8) { request.Result.TrySetResult("server_full"); return; }
                do { id = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant(); }
                while (arenas.ContainsKey(id));
                arena = new Arena();
                arenas.Add(id, arena);
            }
        }
        var player = arena.Model.Join(request.Message.Name);
        if (player is null) { request.Result.TrySetResult("room_full"); return; }
        request.Peer.PlayerId = player.Value;
        request.Peer.RoomId = id!;
        arena.Peers.Add(request.Peer);
        request.Result.TrySetResult(null);
    }
}
