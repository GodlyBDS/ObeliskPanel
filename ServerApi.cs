using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace ObeliskPanel;

public static class ServerApi
{
    public static string Repo(GameType g) => g == GameType.ASA ? "ServersHub/ServerAPI" : "Michidu/ARK-Server-API";
    public static string Name(GameType g) => g == GameType.ASA ? "AsaApi" : "ArkServerApi";
    public static string Loader(ServerProfile p) => Path.Combine(p.BinDir, "AsaApiLoader.exe");
    static string VersionFile(ServerProfile p) => Path.Combine(p.BinDir, "ObeliskPanel.serverapi.txt");

    public static string? InstalledVersion(ServerProfile p)
    {
        if (File.Exists(VersionFile(p))) return File.ReadAllText(VersionFile(p)).Trim();
        if (p.Game == GameType.ASA && File.Exists(Loader(p))) return "unknown";
        if (p.Game == GameType.ASE && File.Exists(Path.Combine(p.BinDir, "version.dll")) && Directory.Exists(Path.Combine(p.BinDir, "ArkApi"))) return "unknown";
        return null;
    }

    public static bool IsInstalled(ServerProfile p) => InstalledVersion(p) != null;

    static HttpClient Client()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("ObeliskPanel");
        return h;
    }

    public static async Task<(string Tag, string ZipUrl)?> Latest(GameType g)
    {
        using var http = Client();
        var json = await http.GetStringAsync($"https://api.github.com/repos/{Repo(g)}/releases/latest");
        using var doc = JsonDocument.Parse(json);
        var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "?";
        foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            var name = a.GetProperty("name").GetString() ?? "";
            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return (tag, a.GetProperty("browser_download_url").GetString()!);
        }
        return null;
    }

    public static async Task Install(ServerProfile p, Action<string> log)
    {
        var latest = await Latest(p.Game) ?? throw new InvalidOperationException("No download found in the latest release.");
        log($"[{p.Name}] Downloading {Name(p.Game)} {latest.Tag} from github.com/{Repo(p.Game)} …");
        using var http = Client();
        var bytes = await http.GetByteArrayAsync(latest.ZipUrl);
        Directory.CreateDirectory(p.BinDir);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        var root = Path.GetFullPath(p.BinDir);
        foreach (var e in zip.Entries.Where(e => e.Name != ""))
        {
            var target = Path.GetFullPath(Path.Combine(root, e.FullName.Replace('/', '\\')));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
            if (File.Exists(target) && target.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            e.ExtractToFile(target, true);
        }
        File.WriteAllText(VersionFile(p), latest.Tag);
        log($"[{p.Name}] {Name(p.Game)} {latest.Tag} installed into {p.BinDir}. Existing plugin configs were kept.");
    }
}
