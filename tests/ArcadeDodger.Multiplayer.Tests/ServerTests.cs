using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ArcadeDodger.Server;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace ArcadeDodger.Multiplayer.Tests;

public sealed class ServerTests : IAsyncLifetime
{
    private WebApplication app = null!;
    private Uri address = null!;
    public async Task InitializeAsync()
    {
        app = Program.Build(["--urls", "http://127.0.0.1:0", "--Logging:LogLevel:Default", "Warning"]);
        await app.StartAsync();
        address = new Uri(app.Urls.Single().Replace("http:", "ws:") + "/ws");
    }
    public async Task DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); }

    [Fact]
    public async Task RealSocketsJoinSameRoomApplyInputAndRetainDisconnectedShip()
    {
        using var a = await Connect();
        await Send(a, "{\"v\":1,\"type\":\"join\",\"name\":\"A\",\"room\":\"\"}");
        var welcomeA = await Read(a);
        var idA = welcomeA.GetProperty("id").GetInt32();
        var room = welcomeA.GetProperty("room").GetString();
        using var b = await Connect();
        await Send(b, JsonSerializer.Serialize(new { v = 1, type = "join", name = "B", room }));
        var welcomeB = await Read(b);
        Assert.Equal(room, welcomeB.GetProperty("room").GetString());
        var initial = await Until(a, x => x.GetProperty("players").GetArrayLength() == 2);
        var x0 = initial.GetProperty("players")[0].GetProperty("x").GetSingle();
        await Send(a, "{\"v\":1,\"type\":\"input\",\"seq\":1,\"x\":-1,\"y\":0,\"aim\":0,\"fire\":true,\"dash\":false}");
        var moved = await Until(b, x => x.GetProperty("players")[0].GetProperty("ack").GetInt32() == 1);
        Assert.True(moved.GetProperty("players")[0].GetProperty("x").GetSingle() < x0);
        Assert.True(moved.GetProperty("shots").GetArrayLength() > 0);
        a.Abort();
        var disconnected = await Until(b, x => !x.GetProperty("players")[0].GetProperty("connected").GetBoolean());
        Assert.Equal(idA, disconnected.GetProperty("players")[0].GetProperty("id").GetInt32());
    }

    [Theory]
    [InlineData("{\"v\":1,\"type\":\"input\",\"seq\":1,\"x\":0,\"y\":0,\"aim\":0,\"fire\":false,\"dash\":false,\"damage\":100}")]
    [InlineData("{\"v\":1,\"type\":\"join\",\"name\":\"Again\",\"room\":\"\"}")]
    public async Task ForgedAuthorityAndRepeatedJoinDisconnect(string invalid)
    {
        using var socket = await Connect();
        await Send(socket, "{\"v\":1,\"type\":\"join\",\"name\":\"A\",\"room\":\"\"}");
        await Read(socket);
        await Send(socket, invalid);
        await AssertDisconnected(socket);
    }

    [Fact]
    public async Task FragmentedJoinAcceptedOversizedFragmentedInputRejected()
    {
        using var socket = await Connect();
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"v\":1,\"type\":\"join\","), WebSocketMessageType.Text, false, CancellationToken.None);
        await Send(socket, "\"name\":\"A\",\"room\":\"\"}");
        Assert.Equal("welcome", (await Read(socket)).GetProperty("type").GetString());
        await socket.SendAsync(new byte[800], WebSocketMessageType.Text, false, CancellationToken.None);
        await socket.SendAsync(new byte[800], WebSocketMessageType.Text, true, CancellationToken.None);
        await AssertDisconnected(socket);
    }

    [Fact]
    public async Task WrongOriginRejectedBeforeUpgrade()
    {
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Origin", "https://example.invalid");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<WebSocketException>(() => socket.ConnectAsync(address, timeout.Token));
    }

    [Fact]
    public async Task MissingInviteRoomReportsError()
    {
        using var socket = await Connect();
        await Send(socket, "{\"v\":1,\"type\":\"join\",\"name\":\"A\",\"room\":\"00000000\"}");
        var error = await Read(socket);
        Assert.Equal(1, error.GetProperty("v").GetInt32());
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
        await Send(socket, "{\"v\":1,\"type\":\"join\",\"name\":\"A\",\"room\":\"\"}");
        await Read(socket);
        await Send(socket, "{\"v\":1,\"type\":\"input\",\"seq\":1,\"x\":0,\"y\":0,\"aim\":0,\"fire\":true,\"dash\":false}");
        await Until(socket, x => x.GetProperty("players")[0].GetProperty("ack").GetInt32() == 1);
        await Send(socket, "{\"v\":1,\"type\":\"respawn\",\"seq\":2}");
        await Send(socket, "{\"v\":1,\"type\":\"input\",\"seq\":3,\"x\":0,\"y\":0,\"aim\":0,\"fire\":false,\"dash\":false}");
        var snapshot = await Until(socket, x => x.GetProperty("players")[0].GetProperty("ack").GetInt32() == 3);
        Assert.False(snapshot.GetProperty("players")[0].GetProperty("protected").GetBoolean());
        Assert.Equal(3, snapshot.GetProperty("players")[0].GetProperty("hull").GetInt32());
        Assert.Single(snapshot.GetProperty("players").EnumerateArray());
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
        await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, timeout.Token);
    }
    private static async Task<JsonElement> Read(ClientWebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[65536];
        var count = 0;
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, count, buffer.Length - count), timeout.Token);
            if (result.MessageType == WebSocketMessageType.Close) throw new WebSocketException("closed");
            count += result.Count;
        } while (!result.EndOfMessage);
        using var document = JsonDocument.Parse(buffer.AsMemory(0, count));
        return document.RootElement.Clone();
    }
    private static async Task<JsonElement> Until(ClientWebSocket socket, Func<JsonElement, bool> predicate)
    {
        for (var i = 0; i < 60; i++)
        {
            var message = await Read(socket);
            if (message.GetProperty("type").GetString() == "snapshot" && predicate(message)) return message;
        }
        throw new TimeoutException("Snapshot condition not observed");
    }
    private static async Task AssertDisconnected(ClientWebSocket socket)
    {
        try { for (var i = 0; i < 60; i++) await Read(socket); }
        catch (WebSocketException) { return; }
        Assert.Fail("Server did not disconnect invalid client");
    }
}
