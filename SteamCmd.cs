using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ObeliskPanel;

public static class SteamCmd
{
    const string ZipUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";

    public static async Task EnsureInstalled(string dir, Action<string> log)
    {
        var exe = Path.Combine(dir, "steamcmd.exe");
        if (File.Exists(exe)) return;
        log("SteamCMD not found – downloading the official version from Valve...");
        Directory.CreateDirectory(dir);
        using var http = new HttpClient();
        var zip = Path.Combine(dir, "steamcmd.zip");
        await File.WriteAllBytesAsync(zip, await http.GetByteArrayAsync(ZipUrl));
        ZipFile.ExtractToDirectory(zip, dir, true);
        File.Delete(zip);
        log("SteamCMD installed.");
    }

    public static async Task<int> InstallOrUpdate(string steamDir, ServerProfile p, Action<string> log)
    {
        await EnsureInstalled(steamDir, log);
        Directory.CreateDirectory(p.InstallDir);
        var exe = Path.Combine(steamDir, "steamcmd.exe");
        var args = $"+force_install_dir \"{p.InstallDir}\" +login anonymous +app_update {p.SteamAppId} validate +quit";
        log($"[{p.Name}] steamcmd {args}");

        int code;
        try
        {
            code = await ConPty.Run(exe, args, steamDir, log);
        }
        catch (Exception ex)
        {
            log($"[{p.Name}] Live output not available ({ex.Message}) – showing SteamCMD output when it arrives.");
            code = await RunRedirected(exe, args, steamDir, log);
        }
        UpdateCheck.Forget(p);
        log($"[{p.Name}] SteamCMD finished (code {code}).");
        return code;
    }

    static async Task<int> RunRedirected(string exe, string args, string dir, Action<string> log)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true, WorkingDirectory = dir,
        };
        using var proc = new Process { StartInfo = psi };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) log(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) log(e.Data); };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync();
        return proc.ExitCode;
    }
}

public static class UpdateCheck
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    static readonly Dictionary<int, (string Build, DateTime At)> Latest = new();
    static readonly Dictionary<string, (string? Build, DateTime At)> Installed = new();
    static readonly Regex BuildRx = new("\"buildid\"\\s+\"(\\d+)\"", RegexOptions.IgnoreCase);

    public static string? InstalledBuild(ServerProfile p)
    {
        if (Installed.TryGetValue(p.InstallDir, out var b) && DateTime.Now - b.At < TimeSpan.FromSeconds(60)) return b.Build;
        string? build = null;
        try
        {
            var acf = Path.Combine(p.InstallDir, "steamapps", $"appmanifest_{p.SteamAppId}.acf");
            if (File.Exists(acf))
            {
                var m = BuildRx.Match(File.ReadAllText(acf));
                if (m.Success) build = m.Groups[1].Value;
            }
        }
        catch { }
        Installed[p.InstallDir] = (build, DateTime.Now);
        return build;
    }

    public static void Forget(ServerProfile p) => Installed.Remove(p.InstallDir);

    public static string? LatestBuild(int appId) => Latest.TryGetValue(appId, out var l) ? l.Build : null;

    public static async Task Refresh(IEnumerable<int> appIds, bool force = false)
    {
        foreach (var id in appIds.Distinct())
        {
            if (!force && Latest.TryGetValue(id, out var l) && DateTime.Now - l.At < TimeSpan.FromMinutes(15)) continue;
            try
            {
                using var doc = JsonDocument.Parse(await Http.GetStringAsync($"https://api.steamcmd.net/v1/info/{id}"));
                var build = doc.RootElement.GetProperty("data").GetProperty(id.ToString()).GetProperty("depots")
                    .GetProperty("branches").GetProperty("public").GetProperty("buildid").GetString();
                if (build != null) Latest[id] = (build, DateTime.Now);
            }
            catch { }
        }
    }

    public static bool UpdateAvailable(ServerProfile p, out string? installed, out string? latest)
    {
        installed = InstalledBuild(p);
        latest = LatestBuild(p.SteamAppId);
        return installed != null && latest != null && installed != latest
               && long.TryParse(installed, out var i) && long.TryParse(latest, out var l) && l > i;
    }
}
