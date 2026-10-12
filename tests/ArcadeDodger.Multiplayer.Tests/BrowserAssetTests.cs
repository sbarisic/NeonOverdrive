using System.Text.Json;
using ArcadeDodger.Server;
using Xunit;

namespace ArcadeDodger.Multiplayer.Tests;

public sealed class BrowserAssetTests
{
    [Fact]
    public async Task ChangingOneModuleInvalidatesEntireGraphAndIndexIsNeverCached()
    {
        var root = Path.Combine(Path.GetTempPath(), "neon-assets-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, "index.html"),
            "<script type=\"module\" src=\"./main.js\"></script>"
        );
        await File.WriteAllTextAsync(Path.Combine(root, "main.js"), "import './skills.js';");
        await File.WriteAllTextAsync(Path.Combine(root, "skills.js"), "export const version = 1;");
        await using var app = Program.Build([
            "--urls",
            "http://127.0.0.1:0",
            "--BrowserRoot",
            root,
            "--ProfilesFile",
            Path.Combine(root, "profiles.json"),
            "--FriendsFile",
            Path.Combine(root, "friends.json"),
            "--Logging:LogLevel:Default",
            "Warning",
        ]);
        try
        {
            await app.StartAsync();
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            var response = await client.GetAsync("/?multiplayer=1");
            Assert.True(response.Headers.CacheControl!.NoStore);
            var first = await response.Content.ReadAsStringAsync();
            var imports = Imports(first);
            Assert.Equal(2, imports.Count);
            Assert.Single(imports.Values.Select(x => x.Split("?v=")[1]).Distinct());
            Assert.Contains($"src=\"{imports["./main.js"]}\"", first);
            using var head = await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, "/index.html")
            );
            Assert.True(head.Headers.CacheControl!.NoStore);
            Assert.Equal("", await head.Content.ReadAsStringAsync());
            Assert.Equal(first, await client.GetStringAsync("/index.html"));

            await File.WriteAllTextAsync(
                Path.Combine(root, "skills.js"),
                "export const version = 2;"
            );
            var updated = Imports(await client.GetStringAsync("/"));
            foreach (var module in imports.Keys)
                Assert.NotEqual(imports[module], updated[module]);
            using var asset = await client.GetAsync(updated["./skills.js"]);
            Assert.True(asset.Headers.CacheControl!.NoCache);
            Assert.Equal("export const version = 2;", await asset.Content.ReadAsStringAsync());
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            Directory.Delete(root, true);
        }
    }

    private static Dictionary<string, string> Imports(string html)
    {
        var json = html.Split("<script type=\"importmap\">")[1].Split("</script>")[0];
        using var document = JsonDocument.Parse(json);
        return document
            .RootElement.GetProperty("imports")
            .Deserialize<Dictionary<string, string>>()!;
    }
}
