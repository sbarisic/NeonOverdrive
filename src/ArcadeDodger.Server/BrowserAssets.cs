using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArcadeDodger.Server;

internal static class BrowserAssets
{
    // One fingerprint for the complete application module graph prevents mixed cached versions.
    public static string Index(string root)
    {
        var files = Directory.GetFiles(root, "*.js").Order(StringComparer.Ordinal).ToArray();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetFileName(file)));
            hash.AppendData(File.ReadAllBytes(file));
        }
        var version = Convert.ToHexString(hash.GetHashAndReset())[..16];
        var imports = files.ToDictionary(file => "./" + Path.GetFileName(file),
            file => "./" + Path.GetFileName(file) + "?v=" + version);
        var map = JsonSerializer.Serialize(new { imports });
        return File.ReadAllText(Path.Combine(root, "index.html")).Replace(
            "<script type=\"module\" src=\"./main.js\"></script>",
            $"<script type=\"importmap\">{map}</script>\n  <script type=\"module\" src=\"./main.js?v={version}\"></script>",
            StringComparison.Ordinal);
    }
}
