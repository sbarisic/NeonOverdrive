using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ArcadeDodger.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArcadeDodger.Multiplayer.Tests;

public sealed class ServerTests : IAsyncLifetime
{
    private WebApplication app = null!;
    private Uri address = null!;
    private readonly string storePath = Path.Combine(
        Path.GetTempPath(),
        Guid.NewGuid() + "-server-profiles.json"
    );

    public async Task InitializeAsync()
    {
        app = Program.Build([
            "--urls",
            "http://127.0.0.1:0",
            "--Logging:LogLevel:Default",
            "Warning",
            "--ProfilesFile",
            storePath,
        ]);
        await app.StartAsync();
        address = new Uri(app.Urls.Single().Replace("http:", "ws:") + "/ws");
    }

    public async Task DisposeAsync()
    {
        await app.StopAsync();
        await app.DisposeAsync();
        File.Delete(storePath);
        File.Delete(storePath + ".tmp");
    }

    [Fact]
    public async Task ClosedPeerCleanupCannotDisconnectItsImmediateReplacement()
    {
        var service = app.Services.GetRequiredService<ArenaService>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var peer = new Peer();
        Assert.Null(await service.JoinAsync(new("FastRejoin", ""), peer, timeout.Token));
        for (var i = 0; i < 5; i++)
        {
            var snapshot = await peer.Snapshots.Reader.ReadAsync(timeout.Token);
            Assert.True(snapshot.Players.Single(p => p.Id == peer.PlayerId).Connected);
            var id = peer.PlayerId;
            peer.Close();
            peer = new Peer();
            Assert.Null(await service.JoinAsync(new("FastRejoin", ""), peer, timeout.Token));
            Assert.Equal(id, peer.PlayerId);
            Assert.Equal("recovered", peer.JoinKind);
        }
        var last = await peer.Snapshots.Reader.ReadAsync(timeout.Token);
        Assert.True(last.Players.Single(p => p.Id == peer.PlayerId).Connected);
        peer.Close();
    }

    [Fact]
    public async Task SimultaneousJoinsAdmitOnlyOneNormalizedName()
    {
        using var a = await Connect();
        using var b = await Connect();
        await Task.WhenAll(
            Send(a, """{"v":11,"type":"join","name":"Together","room":""}"""),
            Send(b, """{"v":11,"type":"join","name":"TOGETHER","room":""}""")
        );
        var replies = await Task.WhenAll(Read(a), Read(b));
        Assert.Single(replies, x => x.GetProperty("type").GetString() == "welcome");
        var rejected = Assert.Single(replies, x => x.GetProperty("type").GetString() == "error");
        Assert.Equal("name_in_use", rejected.GetProperty("code").GetString());
        var accepted = replies[0].GetProperty("type").GetString() == "welcome" ? a : b;
        var snapshot = await Until(accepted, x => x.TryGetProperty("workshop", out _));
        Assert.Single(snapshot.GetProperty("players").EnumerateArray());
        Assert.Equal(100, snapshot.GetProperty("workshop").GetProperty("credits").GetInt64());
        Assert.Equal(0, snapshot.GetProperty("workshop").GetProperty("replayCount").GetInt64());
    }

    [Fact]
    public async Task SocketRecoveryPreservesRunAndAcknowledgedLeaveStartsFresh()
    {
        const string join = """{"v":11,"type":"join","name":"ReplayPilot","room":""}""";
        using var first = await Connect();
        await Send(first, join);
        Assert.Equal("fresh", (await Read(first)).GetProperty("joinKind").GetString());
        var initial = await Until(first, x => x.TryGetProperty("workshop", out _));
        var run = initial.GetProperty("players")[0].GetProperty("runId").GetString();
        first.Abort();
        await Task.Delay(100);
        using var recovered = await Connect();
        await Send(recovered, join);
        Assert.Equal("recovered", (await Read(recovered)).GetProperty("joinKind").GetString());
        var resumed = await Until(recovered, x => x.TryGetProperty("workshop", out _));
        Assert.Equal(run, resumed.GetProperty("players")[0].GetProperty("runId").GetString());
        await Send(recovered, """{"v":11,"type":"leave","seq":1}""");
        var acknowledged = await Until(
            recovered,
            x => x.GetProperty("leaveSequence").GetInt32() == 1
        );
        Assert.False(acknowledged.GetProperty("players")[0].GetProperty("connected").GetBoolean());
        recovered.Abort();
        await Task.Delay(100);
        using var fresh = await Connect();
        await Send(fresh, join);
        Assert.Equal("fresh", (await Read(fresh)).GetProperty("joinKind").GetString());
        var restarted = await Until(fresh, x => x.TryGetProperty("workshop", out _));
        Assert.NotEqual(run, restarted.GetProperty("players")[0].GetProperty("runId").GetString());
        Assert.Equal(0, restarted.GetProperty("workshop").GetProperty("replayCount").GetInt64());
        Assert.Equal(
            100,
            restarted.GetProperty("workshop").GetProperty("startingCredits").GetInt64()
        );
    }

    [Fact]
    public async Task RealSocketsJoinSameRoomApplyInputAndRetainDisconnectedShip()
    {
        using var a = await Connect();
        await Send(a, "{\"v\":11,\"type\":\"join\",\"name\":\"A\",\"room\":\"\"}");
        var welcomeA = await Read(a);
        var idA = welcomeA.GetProperty("id").GetInt32();
        var room = welcomeA.GetProperty("room").GetString();
        using var b = await Connect();
        await Send(
            b,
            JsonSerializer.Serialize(
                new
                {
                    v = 11,
                    type = "join",
                    name = "B",
                    room,
                }
            )
        );
        var welcomeB = await Read(b);
        Assert.Equal(room, welcomeB.GetProperty("room").GetString());
        var initial = await Until(a, x => x.GetProperty("players").GetArrayLength() == 2);
        var x0 = initial.GetProperty("players")[0].GetProperty("x").GetSingle();
        await Send(
            a,
            "{\"v\":11,\"type\":\"input\",\"seq\":1,\"x\":0,\"y\":-1,\"aim\":0,\"fire\":true,\"menu\":false}"
        );
        var moved = await Until(
            b,
            x => x.GetProperty("players")[0].GetProperty("ack").GetInt32() == 1
        );
        Assert.True(moved.GetProperty("players")[0].GetProperty("x").GetSingle() > x0);
        Assert.Equal(0, moved.GetProperty("shots").GetArrayLength()); // Sanctuary suppresses firing.
        a.Abort();
        var disconnected = await Until(
            b,
            x => !x.GetProperty("players")[0].GetProperty("connected").GetBoolean()
        );
        Assert.Equal(idA, disconnected.GetProperty("players")[0].GetProperty("id").GetInt32());
    }

    [Theory]
    [InlineData(
        "{\"v\":11,\"type\":\"input\",\"seq\":1,\"x\":0,\"y\":0,\"aim\":0,\"fire\":false,\"menu\":false,\"damage\":100}"
    )]
    [InlineData("{\"v\":11,\"type\":\"join\",\"name\":\"Again\",\"room\":\"\"}")]
    public async Task ForgedAuthorityAndRepeatedJoinDisconnect(string invalid)
    {
        using var socket = await Connect();
        await Send(socket, "{\"v\":11,\"type\":\"join\",\"name\":\"A\",\"room\":\"\"}");
        await Read(socket);
        await Send(socket, invalid);
        await AssertDisconnected(socket);
    }

    [Fact]
    public async Task FragmentedJoinAcceptedOversizedFragmentedInputRejected()
    {
        using var socket = await Connect();
        await socket.SendAsync(
            Encoding.UTF8.GetBytes("{\"v\":11,\"type\":\"join\","),
            WebSocketMessageType.Text,
            false,
            CancellationToken.None
        );
        await Send(socket, "\"name\":\"A\",\"room\":\"\"}");
        Assert.Equal("welcome", (await Read(socket)).GetProperty("type").GetString());
        await socket.SendAsync(
            new byte[800],
            WebSocketMessageType.Text,
            false,
            CancellationToken.None
        );
        await socket.SendAsync(
            new byte[800],
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
        await AssertDisconnected(socket);
    }

    [Fact]
    public async Task WrongOriginRejectedBeforeUpgrade()
    {
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Origin", "https://example.invalid");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<WebSocketException>(() =>
            socket.ConnectAsync(address, timeout.Token)
        );
    }

    [Fact]
    public async Task MissingInviteRoomReportsError()
    {
        using var socket = await Connect();
        await Send(socket, "{\"v\":11,\"type\":\"join\",\"name\":\"A\",\"room\":\"00000000\"}");
        var error = await Read(socket);
        Assert.Equal(ClientProtocol.Version, error.GetProperty("v").GetInt32());
        Assert.Equal("error", error.GetProperty("type").GetString());
        Assert.Equal("room_missing", error.GetProperty("code").GetString());

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var close = await socket.ReceiveAsync(new ArraySegment<byte>(new byte[256]), timeout.Token);
        Assert.Equal(WebSocketMessageType.Close, close.MessageType);
        Assert.Equal(WebSocketCloseStatus.PolicyViolation, close.CloseStatus);
        Assert.Equal("room_missing", close.CloseStatusDescription);
        Assert.Equal(WebSocketState.CloseReceived, socket.State);

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, timeout.Token);
        Assert.Equal(WebSocketState.Closed, socket.State);
    }

    [Fact]
    public async Task LiveRespawnRequestDoesNotDisconnectOrResetShip()
    {
        using var socket = await Connect();
        await Send(socket, "{\"v\":11,\"type\":\"join\",\"name\":\"A\",\"room\":\"\"}");
        await Read(socket);
        await Send(
            socket,
            "{\"v\":11,\"type\":\"input\",\"seq\":1,\"x\":0,\"y\":0,\"aim\":0,\"fire\":true,\"menu\":false}"
        );
        await Until(socket, x => x.GetProperty("players")[0].GetProperty("ack").GetInt32() == 1);
        await Send(socket, "{\"v\":11,\"type\":\"respawn\",\"seq\":2}");
        await Send(
            socket,
            "{\"v\":11,\"type\":\"input\",\"seq\":3,\"x\":0,\"y\":0,\"aim\":0,\"fire\":false,\"menu\":false}"
        );
        var snapshot = await Until(
            socket,
            x => x.GetProperty("players")[0].GetProperty("ack").GetInt32() == 3
        );
        Assert.True(snapshot.GetProperty("players")[0].GetProperty("protected").GetBoolean()); // Still in sanctuary.
        Assert.Equal(3, snapshot.GetProperty("players")[0].GetProperty("hull").GetInt32());
        Assert.Single(snapshot.GetProperty("players").EnumerateArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task SkillPurchaseIntentKeepsSocketAndPilotAlive(int choice)
    {
        using var socket = await Connect();
        await Send(socket, """{"v":11,"type":"join","name":"Buyer","room":""}""");
        var welcome = await Read(socket);
        var id = welcome.GetProperty("id").GetInt32();
        await Send(
            socket,
            JsonSerializer.Serialize(
                new
                {
                    v = 11,
                    type = "choice",
                    seq = 1,
                    choice = SkillCatalog.Items[choice].Id,
                }
            )
        );
        await Send(
            socket,
            """{"v":11,"type":"input","seq":2,"x":0,"y":0,"aim":0,"fire":false,"menu":false}"""
        );
        var snapshot = await Until(
            socket,
            x => x.GetProperty("players")[0].GetProperty("ack").GetInt32() == 2
        );
        var player = snapshot.GetProperty("players")[0];
        Assert.Equal(id, player.GetProperty("id").GetInt32());
        Assert.True(player.GetProperty("connected").GetBoolean());
        // No points yet: reject the purchase, not the connection.
        Assert.Equal(0, player.GetProperty("skills").GetArrayLength());
        Assert.Equal(
            "level_required",
            snapshot.GetProperty("skillReceipt").GetProperty("result").GetString()
        );
        Assert.Equal(
            SkillCatalog.Items[choice].Id,
            snapshot.GetProperty("skillReceipt").GetProperty("skill").GetString()
        );
        Assert.Equal(WebSocketState.Open, socket.State);
    }

    [Fact]
    public async Task WelcomeCatalogUsesCamelCaseAndPurchasesReturnPrivateReceipts()
    {
        using var a = await Connect();
        await Send(a, """{"v":11,"type":"join","name":"Buyer","room":""}""");
        var welcome = await Read(a);
        Assert.Equal(66, welcome.GetProperty("skills").GetArrayLength());
        Assert.Equal(112, welcome.GetProperty("catalog").GetArrayLength());
        Assert.Equal(6, welcome.GetProperty("catalog")[5].GetProperty("id").GetInt32());
        await Send(
            a,
            """{"v":11,"type":"input","seq":1,"x":0,"y":0,"aim":0,"fire":false,"menu":false,"editing":true}"""
        );
        await Send(a, """{"v":11,"type":"buy","seq":2,"module":6}""");
        var rejected = await Until(
            a,
            m =>
                m.GetProperty("workshop").ValueKind == JsonValueKind.Object
                && m.GetProperty("workshop").GetProperty("sequence").GetInt32() == 2
        );
        Assert.Equal(
            "not_docked",
            rejected.GetProperty("workshop").GetProperty("result").GetString()
        );
        await Send(a, """{"v":11,"type":"sector","seq":3,"action":"dock","target":10}""");
        await Send(a, """{"v":11,"type":"buy","seq":4,"module":6}""");
        var receipt = await Until(
            a,
            m =>
                m.GetProperty("workshop").ValueKind == JsonValueKind.Object
                && m.GetProperty("workshop").GetProperty("sequence").GetInt32() == 4
                && m.GetProperty("workshop").GetProperty("result").GetString() != "pending"
        );
        Assert.Equal(40, receipt.GetProperty("workshop").GetProperty("credits").GetInt32());
        Assert.Equal("ok", receipt.GetProperty("workshop").GetProperty("result").GetString());
        using var b = await Connect();
        await Send(b, """{"v":11,"type":"join","name":"Observer","room":""}""");
        await Read(b);
        var other = await Until(b, _ => true);
        Assert.Equal(100, other.GetProperty("workshop").GetProperty("credits").GetInt32());
        using var duplicate = await Connect();
        await Send(duplicate, """{"v":11,"type":"join","name":"buyer","room":""}""");
        Assert.Equal("name_in_use", (await Read(duplicate)).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task OldBrowserGetsRefreshRequiredInsteadOfJoiningWithWrongProtocol(int version)
    {
        using var socket = await Connect();
        await Send(
            socket,
            JsonSerializer.Serialize(
                new
                {
                    v = version,
                    type = "join",
                    name = "Old",
                    room = "",
                }
            )
        );
        Assert.Equal("refresh_required", (await Read(socket)).GetProperty("code").GetString());
    }

    private async Task<ClientWebSocket> Connect()
    {
        var socket = new ClientWebSocket();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.ConnectAsync(address, timeout.Token);
        return socket;
    }

    private static async Task Send(ClientWebSocket socket, string json)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.SendAsync(
            Encoding.UTF8.GetBytes(json),
            WebSocketMessageType.Text,
            true,
            timeout.Token
        );
    }

    private static async Task<JsonElement> Read(ClientWebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[262144];
        var count = 0;
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer, count, buffer.Length - count),
                timeout.Token
            );
            if (result.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException("closed");
            count += result.Count;
        } while (!result.EndOfMessage);
        using var document = JsonDocument.Parse(buffer.AsMemory(0, count));
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement> Until(
        ClientWebSocket socket,
        Func<JsonElement, bool> predicate
    )
    {
        for (var i = 0; i < 60; i++)
        {
            var message = await Read(socket);
            if (message.GetProperty("type").GetString() == "snapshot" && predicate(message))
                return message;
        }
        throw new TimeoutException("Snapshot condition not observed");
    }

    private static async Task AssertDisconnected(ClientWebSocket socket)
    {
        try
        {
            for (var i = 0; i < 60; i++)
                await Read(socket);
        }
        catch (WebSocketException)
        {
            return;
        }
        Assert.Fail("Server did not disconnect invalid client");
    }
}
