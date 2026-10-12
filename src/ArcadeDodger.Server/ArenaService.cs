using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using ArcadeDodger.Multiplayer;

namespace ArcadeDodger.Server;

public sealed class Peer
{
    public int PlayerId { get; set; }
    public string RoomId { get; set; } = "";
    public string Name { get; set; } = "";
    public string JoinKind { get; set; } = "fresh";
    public SectorDefinition? Sector { get; set; }
    public ContractDefinition[] Contracts { get; set; } = [];
    public bool Closed => Volatile.Read(ref closed) != 0;
    private int closed;
    private InputMessage? latest;
    private RespawnMessage? respawn;
    private EquipMessage? equip;
    private readonly Channel<EquipmentMessage> equipmentCommands =
        Channel.CreateBounded<EquipmentMessage>(16);

    public void Submit(EquipmentMessage request) => equipmentCommands.Writer.TryWrite(request);

    private LeaveMessage? leave;

    public void Submit(LeaveMessage request) => Interlocked.Exchange(ref leave, request);

    public void Disconnect(Room room)
    {
        if (Interlocked.Exchange(ref leave, null) is { } request)
            room.Leave(PlayerId, request);
        room.Disconnect(PlayerId);
    }

    private readonly Channel<SectorActionMessage> sectorActions =
        Channel.CreateBounded<SectorActionMessage>(16);

    public void Submit(SectorActionMessage request) => sectorActions.Writer.TryWrite(request);

    private readonly Channel<BuyMessage> buys = Channel.CreateBounded<BuyMessage>(16);

    public void Submit(BuyMessage request) => buys.Writer.TryWrite(request);

    private readonly Channel<FriendMessage> social = Channel.CreateBounded<FriendMessage>(16);

    public void Submit(FriendMessage request) => social.Writer.TryWrite(request);

    public void Submit(EquipMessage request) => Interlocked.Exchange(ref equip, request);

    private const int ChoiceCapacity = 16;
    private readonly Channel<ChoiceMessage> choices = Channel.CreateBounded<ChoiceMessage>(
        new BoundedChannelOptions(ChoiceCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
        }
    );

    public void Submit(ChoiceMessage request)
    {
        // Overflow is discarded, not blocked; a later snapshot is the purchase receipt.
        choices.Writer.TryWrite(request);
    }

    public void Submit(RespawnMessage request) => Interlocked.Exchange(ref respawn, request);

    public RespawnMessage? TakeRespawn() => Interlocked.Exchange(ref respawn, null);

    public void Close() => Interlocked.Exchange(ref closed, 1);

    public void Submit(InputMessage input) => Interlocked.Exchange(ref latest, input);

    public InputMessage? TakeInput() => Interlocked.Exchange(ref latest, null);

    /// <summary>Called only by the simulation owner; merge mailboxes in shared sequence order.</summary>
    public void ApplyPending(Room room)
    {
        if (Closed)
            return;
        var messages = new List<SequencedMessage>(ChoiceCapacity + 2);
        if (TakeInput() is { } input)
            messages.Add(input);
        if (Interlocked.Exchange(ref equip, null) is { } loadout)
            messages.Add(loadout);
        if (TakeRespawn() is { } request)
            messages.Add(request);
        if (Interlocked.Exchange(ref leave, null) is { } leaveRequest)
            messages.Add(leaveRequest);
        for (var i = 0; i < ChoiceCapacity && choices.Reader.TryRead(out var choice); i++)
            messages.Add(choice);
        for (var i = 0; i < 16 && social.Reader.TryRead(out var friendship); i++)
            messages.Add(friendship);
        for (var i = 0; i < 16 && buys.Reader.TryRead(out var buy); i++)
            messages.Add(buy);
        for (var i = 0; i < 16 && sectorActions.Reader.TryRead(out var sector); i++)
            messages.Add(sector);
        for (var i = 0; i < 16 && equipmentCommands.Reader.TryRead(out var equipmentRequest); i++)
            messages.Add(equipmentRequest);
        messages.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
        foreach (var message in messages)
        {
            switch (message)
            {
                case EquipmentMessage command:
                    room.Equipment(PlayerId, command);
                    break;
                case LeaveMessage leaveCommand:
                    room.Leave(PlayerId, leaveCommand);
                    break;
                case SectorActionMessage sector:
                    room.SectorAction(PlayerId, sector);
                    break;
                case BuyMessage purchaseModule:
                    room.Buy(PlayerId, purchaseModule);
                    break;
                case FriendMessage friendship:
                    room.Friend(PlayerId, friendship);
                    break;
                case InputMessage intent:
                    room.Input(PlayerId, intent);
                    break;
                case RespawnMessage respawnRequest:
                    room.Respawn(PlayerId, respawnRequest);
                    break;
                case EquipMessage equipment:
                    room.Equip(PlayerId, equipment);
                    break;
                case ChoiceMessage purchase:
                    room.Choice(PlayerId, purchase);
                    break;
            }
        }
    }

    public Channel<RoomSnapshot> Snapshots { get; } =
        Channel.CreateBounded<RoomSnapshot>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true,
            }
        );
}

/// <summary>Only this service's tick loop writes simulation state. Peers have bounded mailboxes.</summary>
public sealed class ArenaService : BackgroundService
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record JoinRequest(
        JoinMessage Message,
        Peer Peer,
        TaskCompletionSource<string?> Result
    );

    private sealed class Arena(FriendBook friends, ProfileStore profiles)
    {
        public Room Model { get; } = new(friends: friends, profiles: profiles);
        public List<Peer> Peers { get; } = new();
        public long EmptySince = -1;
    }

    private readonly FriendBook friends;
    private readonly ProfileStore profiles;

    public ArenaService(FriendBook friends, ProfileStore profiles)
    {
        this.friends = friends;
        this.profiles = profiles;
        profiles.BackgroundWrites = true;
    }

    private readonly Dictionary<string, Arena> arenas = new();
    private readonly SemaphoreSlim admissions = new(256);
    private readonly Channel<JoinRequest> joins = Channel.CreateBounded<JoinRequest>(256);
    private readonly Dictionary<string, JoinRequest> waitingJoins = [];

    public async Task<string?> JoinAsync(JoinMessage message, Peer peer, CancellationToken token)
    {
        if (!admissions.Wait(0))
            return "server_busy";
        try
        {
            var ensured = await profiles
                .Schedule(() => profiles.Ensure(message.Name))
                .WaitAsync(token);
            if (ensured != "ok")
                return ensured;
            var result = new TaskCompletionSource<string?>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            if (!joins.Writer.TryWrite(new(message, peer, result)))
                return "server_busy";
            return await result.Task.WaitAsync(token);
        }
        finally
        {
            admissions.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1d / Room.TickRate));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                // Bound admission work per tick even under a sustained connection flood.
                foreach (var pending in waitingJoins.Values.ToArray())
                    Admit(pending);
                for (var i = 0; i < 32 && joins.Reader.TryRead(out var request); i++)
                    Admit(request);
                foreach (var (id, arena) in arenas.ToArray())
                {
                    foreach (var peer in arena.Peers.ToArray())
                    {
                        if (peer.Closed)
                        {
                            peer.Disconnect(arena.Model);
                            arena.Peers.Remove(peer);
                            peer.Snapshots.Writer.TryComplete();
                        }
                        else
                        {
                            peer.ApplyPending(arena.Model);
                        }
                    }
                    arena.Model.Step();
                    if (arena.Model.Tick % 2 == 0 && arena.Peers.Count > 0)
                    {
                        var snapshot = arena.Model.Snapshot();
                        foreach (var peer in arena.Peers)
                            peer.Snapshots.Writer.TryWrite(
                                arena.Model.ForViewer(snapshot, peer.PlayerId) with
                                {
                                    Sector = arena.Model.SectorSnapshot(peer.PlayerId),
                                    Social = arena.Model.Social(peer.PlayerId),
                                    Workshop = arena.Model.Workshop(peer.PlayerId),
                                    SkillReceipt = arena.Model.SkillAction(peer.PlayerId),
                                    CreditReward = arena.Model.LastCreditReward(peer.PlayerId),
                                    LeaveSequence = arena.Model.LeaveSequence(peer.PlayerId),
                                    RunResetResult = arena.Model.ResetResult(peer.PlayerId),
                                }
                            );
                    }
                    if (arena.Peers.Count > 0)
                        arena.EmptySince = -1;
                    else if (arena.EmptySince < 0)
                        arena.EmptySince = arena.Model.Tick;
                    else if (arena.Model.Tick - arena.EmptySince >= 15 * 60 * Room.TickRate)
                        arenas.Remove(id);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            foreach (var arena in arenas.Values)
                arena.Model.SaveRuns();
            await profiles.FlushAsync();
            joins.Writer.TryComplete();
            while (joins.Reader.TryRead(out var pending))
                pending.Result.TrySetResult("server_stopping");
            foreach (var pending in waitingJoins.Values)
                pending.Result.TrySetResult("server_stopping");
            foreach (var peer in arenas.Values.SelectMany(a => a.Peers))
                peer.Snapshots.Writer.TryComplete();
        }
    }

    private void Admit(JoinRequest request)
    {
        var key = FriendBook.Key(request.Message.Name);
        if (waitingJoins.TryGetValue(key, out var waiting) && !ReferenceEquals(waiting, request))
        {
            request.Result.TrySetResult("name_in_use");
            return;
        }
        waitingJoins.Remove(key);
        if (request.Peer.Closed)
        {
            foreach (var candidate in arenas.Values)
                candidate.Model.CancelPendingJoin(request.Message.Name);
            request.Result.TrySetResult("cancelled");
            return;
        }
        if (
            arenas
                .Values.SelectMany(a => a.Peers)
                .Any(p =>
                    !p.Closed && FriendBook.Key(p.Name) == FriendBook.Key(request.Message.Name)
                )
        )
        {
            request.Result.TrySetResult("name_in_use");
            return;
        }
        var ready = profiles.Ensure(request.Message.Name);
        if (ready != "ok")
        {
            request.Result.TrySetResult(ready);
            return;
        }
        // Rejoining can arrive before the tick-loop removes a closed peer.
        foreach (var a in arenas.Values)
        foreach (var p in a.Peers.Where(p => p.Closed).ToArray())
        {
            p.Disconnect(a.Model);
            // Retire the old transport now: later cleanup must not disconnect the
            // replacement session when recovery reuses the same player ID.
            a.Peers.Remove(p);
            p.Snapshots.Writer.TryComplete();
        }
        var id = request.Message.Room;
        var previous = arenas.FirstOrDefault(a => a.Value.Model.ContainsRun(request.Message.Name));
        if (previous.Key is not null)
        {
            if (id.Length > 0 && id != previous.Key)
            {
                request.Result.TrySetResult("run_in_another_room");
                return;
            }
            id = previous.Key;
        }
        Arena? arena;
        if (id.Length > 0)
        {
            if (!arenas.TryGetValue(id, out arena))
            {
                request.Result.TrySetResult("room_missing");
                return;
            }
        }
        else
        {
            var available = arenas.FirstOrDefault(a =>
                a.Value.Model.AdmissionCount < Room.Capacity
            );
            id = available.Key;
            arena = available.Value;
            if (arena is null)
            {
                if (arenas.Count >= 8)
                {
                    request.Result.TrySetResult("server_full");
                    return;
                }
                do
                {
                    id = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
                } while (arenas.ContainsKey(id));
                arena = new Arena(friends, profiles);
                arenas.Add(id, arena);
            }
        }
        var player = arena.Model.TryJoin(request.Message.Name, out var joinError);
        if (player is null)
        {
            if (joinError == "pending")
                waitingJoins[key] = request;
            else
                request.Result.TrySetResult(joinError);
            return;
        }
        request.Peer.Name = request.Message.Name;
        request.Peer.PlayerId = player.Value;
        request.Peer.JoinKind = arena.Model.JoinKind(player.Value);
        request.Peer.RoomId = id!;
        request.Peer.Sector = arena.Model.Sector;
        request.Peer.Contracts = arena.Model.Contracts;
        arena.Peers.Add(request.Peer);
        request.Result.TrySetResult(null);
    }
}
