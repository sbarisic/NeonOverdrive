using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using ArcadeDodger.Multiplayer;

namespace ArcadeDodger.Server;

public static class Program
{
    public static Task Main(string[] args) => Build(args).RunAsync();

    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSingleton<ArenaService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ArenaService>());
        var app = builder.Build();
        app.UseWebSockets();
        var browserRoot = builder.Configuration["BrowserRoot"];
        if (string.IsNullOrWhiteSpace(browserRoot) && app.Environment.IsDevelopment() &&
            builder.Configuration.GetValue<bool>("UseLocalBrowserBundle"))
        {
            var configuration = typeof(Program).Assembly.GetCustomAttributes(
                typeof(System.Reflection.AssemblyConfigurationAttribute), false)
                .Cast<System.Reflection.AssemblyConfigurationAttribute>().Single().Configuration;
            browserRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath,
                "../ArcadeDodger/Web/bin", configuration, "net10.0/browser-wasm/AppBundle"));
            if (!File.Exists(Path.Combine(browserRoot, "index.html")))
                throw new InvalidOperationException("Browser bundle missing. Build the server in Visual Studio " +
                    "or build with -p:BuildBrowserClient=true before using the local launch profile.");
        }
        if (!string.IsNullOrWhiteSpace(browserRoot))
        {
            var files = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.GetFullPath(browserRoot));
            app.Lifetime.ApplicationStopped.Register(files.Dispose);
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
            var types = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
            types.Mappings[".pdb"] = "application/octet-stream";
            app.UseStaticFiles(new StaticFileOptions { FileProvider = files, ContentTypeProvider = types });
        }
        app.MapGet("/health", () => Results.Json(new { status = "ok", protocol = ClientProtocol.Version }));
        var admission = new ConnectionAdmission();
        app.Map("/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
            var origin = context.Request.Headers.Origin.ToString();
            var expected = $"{context.Request.Scheme}://{context.Request.Host}";
            if (origin.Length > 0 && !string.Equals(origin, expected, StringComparison.OrdinalIgnoreCase))
            { context.Response.StatusCode = 403; return; }
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!admission.Enter(ip)) { context.Response.StatusCode = 429; return; }
            try
            {
                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                await Handle(socket, context.RequestServices.GetRequiredService<ArenaService>(), context.RequestAborted);
            }
            finally { admission.Leave(ip); }
        });
        return app;
    }

    private static async Task Handle(WebSocket socket, ArenaService service, CancellationToken requestAborted)
    {
        var peer = new Peer();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        Task? sender = null;
        try
        {
            var session = new ClientSession();
            var first = await Receive(socket, TimeSpan.FromSeconds(5), lifetime.Token);
            if (first is not JoinMessage join || !session.TryAccept(first)) return;
            using var joinTimeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            joinTimeout.CancelAfter(TimeSpan.FromSeconds(5));
            var error = await service.JoinAsync(join, peer, joinTimeout.Token);
            if (error is not null)
            {
                await Send(socket, JsonSerializer.SerializeToUtf8Bytes(new { v = 1, type = "error", code = error }), lifetime.Token);
                using var closeTimeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                closeTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, error, closeTimeout.Token);
                return;
            }
            await Send(socket, JsonSerializer.SerializeToUtf8Bytes(new
            { v = 1, type = "welcome", id = peer.PlayerId, room = peer.RoomId, tickRate = Room.TickRate }), lifetime.Token);
            sender = SendSnapshots(socket, peer, lifetime);
            var tokens = 90d;
            var last = Stopwatch.GetTimestamp();
            while (!lifetime.IsCancellationRequested)
            {
                var message = await Receive(socket, TimeSpan.FromSeconds(10), lifetime.Token);
                var now = Stopwatch.GetTimestamp();
                tokens = Math.Min(90, tokens + Stopwatch.GetElapsedTime(last, now).TotalSeconds * 60);
                last = now;
                if (tokens < 1 || message is null || !session.TryAccept(message)) break;
                tokens--;
                // Keep unsupported upgrade choices explicit until progression is implemented.
                if (message is RespawnMessage respawn) { peer.Submit(respawn); continue; }
                if (message is not InputMessage input) break;
                peer.Submit(input);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            peer.Close();
            lifetime.Cancel();
            if (socket.State != WebSocketState.Closed) socket.Abort();
            if (sender is not null) await sender;
        }
    }

    private static async Task<ClientMessage?> Receive(WebSocket socket, TimeSpan timeout, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout);
        var bytes = new byte[ClientProtocol.MaxMessageBytes + 1];
        var count = 0;
        for (var fragments = 0; fragments < 32; fragments++)
        {
            var result = await socket.ReceiveAsync(bytes.AsMemory(count), deadline.Token);
            if (result.MessageType != WebSocketMessageType.Text) return null;
            count += result.Count;
            if (count > ClientProtocol.MaxMessageBytes) return null;
            if (result.EndOfMessage)
                return ClientProtocol.TryParse(bytes.AsMemory(0, count), out var message) ? message : null;
        }
        return null;
    }

    private static async Task SendSnapshots(WebSocket socket, Peer peer, CancellationTokenSource lifetime)
    {
        try
        {
            await foreach (var bytes in peer.Snapshots.Reader.ReadAllAsync(lifetime.Token))
                await Send(socket, bytes, lifetime.Token);
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally { lifetime.Cancel(); }
    }

    private static async Task Send(WebSocket socket, byte[] bytes, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(2));
        await socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, deadline.Token);
    }

    private sealed class ConnectionAdmission
    {
        private readonly Dictionary<string, int> counts = new();
        private readonly object gate = new();
        private int total;
        public bool Enter(string ip)
        {
            lock (gate)
            {
                counts.TryGetValue(ip, out var count);
                if (total >= 192 || count >= 8) return false;
                counts[ip] = count + 1;
                total++;
                return true;
            }
        }
        public void Leave(string ip)
        {
            lock (gate)
            {
                if (--counts[ip] == 0) counts.Remove(ip);
                total--;
            }
        }
    }
}
