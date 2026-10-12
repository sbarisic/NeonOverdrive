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
        builder.Services.AddSingleton(
            new FriendBook(
                builder.Configuration["FriendsFile"]
                    ?? Path.Combine(AppContext.BaseDirectory, "data", "friends.json")
            )
        );
        builder.Services.AddSingleton(
            new ProfileStore(
                builder.Configuration["ProfilesFile"]
                    ?? Path.Combine(AppContext.BaseDirectory, "data", "profiles.json")
            )
        );
        builder.Services.AddSingleton<ArenaService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ArenaService>());
        var app = builder.Build();
        app.UseWebSockets();
        var browserRoot = builder.Configuration["BrowserRoot"];
        if (
            string.IsNullOrWhiteSpace(browserRoot)
            && app.Environment.IsDevelopment()
            && builder.Configuration.GetValue<bool>("UseLocalBrowserBundle")
        )
        {
            var configuration = typeof(Program)
                .Assembly.GetCustomAttributes(
                    typeof(System.Reflection.AssemblyConfigurationAttribute),
                    false
                )
                .Cast<System.Reflection.AssemblyConfigurationAttribute>()
                .Single()
                .Configuration;
            browserRoot = Path.GetFullPath(
                Path.Combine(
                    app.Environment.ContentRootPath,
                    "../ArcadeDodger/Web/bin",
                    configuration,
                    "net10.0/browser-wasm/AppBundle"
                )
            );
            if (!File.Exists(Path.Combine(browserRoot, "index.html")))
                throw new InvalidOperationException(
                    "Browser bundle missing. Build the server in Visual Studio "
                        + "or build with -p:BuildBrowserClient=true before using the local launch profile."
                );
        }
        if (!string.IsNullOrWhiteSpace(browserRoot))
        {
            browserRoot = Path.GetFullPath(browserRoot);
            app.Use(
                async (context, next) =>
                {
                    if (
                        (
                            HttpMethods.IsGet(context.Request.Method)
                            || HttpMethods.IsHead(context.Request.Method)
                        ) && context.Request.Path.Value is "/" or "/index.html"
                    )
                    {
                        context.Response.ContentType = "text/html; charset=utf-8";
                        context.Response.Headers.CacheControl = "no-store";
                        if (!HttpMethods.IsHead(context.Request.Method))
                            await context.Response.WriteAsync(BrowserAssets.Index(browserRoot));
                        return;
                    }
                    await next(context);
                }
            );
            var files = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
                Path.GetFullPath(browserRoot)
            );
            app.Lifetime.ApplicationStopped.Register(files.Dispose);
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
            var types = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
            types.Mappings[".pdb"] = "application/octet-stream";
            app.UseStaticFiles(
                new StaticFileOptions
                {
                    FileProvider = files,
                    ContentTypeProvider = types,
                    OnPrepareResponse = context =>
                        context.Context.Response.Headers.CacheControl = "no-cache",
                }
            );
        }
        app.MapGet(
            "/health",
            () => Results.Json(new { status = "ok", protocol = ClientProtocol.Version })
        );
        var admission = new ConnectionAdmission();
        app.Map(
            "/ws",
            async context =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    return;
                }
                var origin = context.Request.Headers.Origin.ToString();
                var expected = $"{context.Request.Scheme}://{context.Request.Host}";
                if (
                    origin.Length > 0
                    && !string.Equals(origin, expected, StringComparison.OrdinalIgnoreCase)
                )
                {
                    context.Response.StatusCode = 403;
                    return;
                }
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                if (!admission.Enter(ip))
                {
                    context.Response.StatusCode = 429;
                    return;
                }
                try
                {
                    using var socket = await context.WebSockets.AcceptWebSocketAsync();
                    await WebSocketSession.Handle(
                        socket,
                        context.RequestServices.GetRequiredService<ArenaService>(),
                        context.RequestAborted
                    );
                }
                finally
                {
                    admission.Leave(ip);
                }
            }
        );
        return app;
    }
}
