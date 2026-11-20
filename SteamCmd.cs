using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

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
        var args = $"+force_install_dir \"{p.InstallDir}\" +login anonymous +app_update {p.SteamAppId} validate +quit";
        log($"[{p.Name}] steamcmd {args}");

        var psi = new ProcessStartInfo(Path.Combine(steamDir, "steamcmd.exe"), args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var proc = new Process { StartInfo = psi };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) log(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) log(e.Data); };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync();
        log($"[{p.Name}] SteamCMD finished (code {proc.ExitCode}).");
        return proc.ExitCode;
    }
}
