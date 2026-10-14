using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using ArcadeDodger.Multiplayer;

namespace ArcadeDodger.Server;

internal static class WebSocketSession
{
    public static async Task Handle(
        WebSocket socket,
        ArenaService service,
        CancellationToken requestAborted
    )
    {
        var peer = new Peer();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        Task? sender = null;
        try
        {
            var session = new ClientSession();
            var first = await Receive(socket, TimeSpan.FromSeconds(5), lifetime.Token);
            if (first is IncompatibleMessage)
            {
                await Send(
                    socket,
                    JsonSerializer.SerializeToUtf8Bytes(
                        new
                        {
                            v = ClientProtocol.Version,
                            type = "error",
                            code = "refresh_required",
                        }
                    ),
                    lifetime.Token
                );
                using var refreshTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await socket.CloseAsync(
                    WebSocketCloseStatus.PolicyViolation,
                    "refresh_required",
                    refreshTimeout.Token
                );
                return;
            }
            if (first is not JoinMessage join || !session.TryAccept(first))
                return;
            using var joinTimeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            joinTimeout.CancelAfter(TimeSpan.FromSeconds(5));
            var error = await service.JoinAsync(join, peer, joinTimeout.Token);
            if (error is not null)
            {
                await Send(
                    socket,
                    JsonSerializer.SerializeToUtf8Bytes(
                        new
                        {
                            v = ClientProtocol.Version,
                            type = "error",
                            code = error,
                        }
                    ),
                    lifetime.Token
                );
                using var closeTimeout = CancellationTokenSource.CreateLinkedTokenSource(
                    lifetime.Token
                );
                closeTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                await socket.CloseAsync(
                    WebSocketCloseStatus.PolicyViolation,
                    error,
                    closeTimeout.Token
                );
                return;
            }
            await Send(
                socket,
                JsonSerializer.SerializeToUtf8Bytes(
                    new
                    {
                        v = ClientProtocol.Version,
                        type = "welcome",
                        id = peer.PlayerId,
                        room = peer.RoomId,
                        joinKind = peer.JoinKind,
                        tickRate = Room.TickRate,
                        skills = SkillCatalog.Items,
                        catalog = ModuleLevels.Catalog,
                        shipCatalog = EquipmentCatalog.Items,
                        chassisCatalog = ChassisCatalog.Items,
                        unlockLevels = ModuleCatalog.UnlockLevels,
                        sector = peer.Sector,
                        contracts = peer.Contracts,
                        enemies = EnemyCatalog.Items,
                    },
                    ArenaService.Json
                ),
                lifetime.Token
            );
            sender = SendSnapshots(socket, peer, lifetime);
            var tokens = 90d;
            var last = Stopwatch.GetTimestamp();
            while (!lifetime.IsCancellationRequested)
            {
                var message = await Receive(socket, TimeSpan.FromSeconds(10), lifetime.Token);
                var now = Stopwatch.GetTimestamp();
                tokens = Math.Min(
                    90,
                    tokens + Stopwatch.GetElapsedTime(last, now).TotalSeconds * 60
                );
                last = now;
                if (tokens < 1 || message is null || !session.TryAccept(message))
                    break;
                tokens--;
                if (message is LeaveMessage leave)
                {
                    peer.Submit(leave);
                    continue;
                }
                if (message is SectorActionMessage sector)
                {
                    await peer.SubmitAsync(sector, lifetime.Token);
                    continue;
                }
                if (message is BuyMessage buy)
                {
                    peer.Submit(buy);
                    continue;
                }
                if (message is FriendMessage friendship)
                {
                    peer.Submit(friendship);
                    continue;
                }
                if (message is EquipmentMessage command)
                {
                    peer.Submit(command);
                    continue;
                }
                if (message is EquipMessage equip)
                {
                    peer.Submit(equip);
                    continue;
                }
                if (message is ChoiceMessage choice)
                {
                    peer.Submit(choice);
                    continue;
                }
                if (message is RespawnMessage respawn)
                {
                    peer.Submit(respawn);
                    continue;
                }
                if (message is not InputMessage input)
                    break;
                peer.Submit(input);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            peer.Close();
            lifetime.Cancel();
            if (socket.State != WebSocketState.Closed)
                socket.Abort();
            if (sender is not null)
                await sender;
        }
    }

    private static async Task<ClientMessage?> Receive(
        WebSocket socket,
        TimeSpan timeout,
        CancellationToken token
    )
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout);
        var bytes = new byte[ClientProtocol.MaxMessageBytes + 1];
        var count = 0;
        for (var fragments = 0; fragments < 32; fragments++)
        {
            var result = await socket.ReceiveAsync(bytes.AsMemory(count), deadline.Token);
            if (result.MessageType != WebSocketMessageType.Text)
                return null;
            count += result.Count;
            if (count > ClientProtocol.MaxMessageBytes)
                return null;
            if (result.EndOfMessage)
            {
                try
                {
                    using var doc = JsonDocument.Parse(bytes.AsMemory(0, count));
                    if (
                        doc.RootElement.ValueKind == JsonValueKind.Object
                        && doc.RootElement.TryGetProperty("v", out var v)
                        && v.TryGetInt32(out var version)
                        && version != ClientProtocol.Version
                    )
                        return new IncompatibleMessage();
                }
                catch (JsonException)
                {
                    return null;
                }
                return ClientProtocol.TryParse(bytes.AsMemory(0, count), out var message)
                    ? message
                    : null;
            }
        }
        return null;
    }

    private static async Task SendSnapshots(
        WebSocket socket,
        Peer peer,
        CancellationTokenSource lifetime
    )
    {
        try
        {
            var encoder = new SnapshotEncoder();
            await foreach (var frame in peer.Snapshots.Reader.ReadAllAsync(lifetime.Token))
                await Send(socket, encoder.Encode(frame), lifetime.Token);
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            lifetime.Cancel();
        }
    }

    private static async Task Send(WebSocket socket, byte[] bytes, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(2));
        await socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, deadline.Token);
    }
}
