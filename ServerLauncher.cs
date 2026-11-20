using System.Diagnostics;
using System.IO;

namespace ObeliskPanel;

public class ServerLauncher
{
    readonly Dictionary<ServerProfile, Process> _running = new();

    public Process? Find(ServerProfile p)
    {
        if (_running.TryGetValue(p, out var pr) && !pr.HasExited) return pr;
        _running.Remove(p);
        foreach (var x in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(p.ExePath)))
        {
            try
            {
                if (string.Equals(x.MainModule?.FileName, p.ExePath, StringComparison.OrdinalIgnoreCase))
                {
                    _running[p] = x;
                    return x;
                }
            }
            catch { }
        }
        return null;
    }

    public bool IsRunning(ServerProfile p) => Find(p) != null;

    public static string ClusterArgDir(ServerProfile p, AppSettings s)
    {
        var dir = s.GetClusterDir(p.Game).TrimEnd('\\', '/');
        if (p.Game == GameType.ASA && Path.GetFileName(dir).Equals("clusters", StringComparison.OrdinalIgnoreCase))
            dir = Path.GetDirectoryName(dir) ?? dir;
        return dir;
    }

    static string Quote(string path) => path.Contains(' ') ? $"\"{path}\"" : path;

    public static string BuildArgs(ServerProfile p, AppSettings s) =>
        p.Game == GameType.ASA ? BuildAsaArgs(p, s) : BuildAseArgs(p, s);

    public static List<string> ServerMods(ServerProfile p, AppSettings s) =>
        p.AllModIds().Where(id => !s.CosmeticMods.Contains(id)).ToList();

    static string BuildAsaArgs(ServerProfile p, AppSettings s)
    {
        var args = new List<string> { $"{p.Map}?Port={p.Port}?RCONEnabled=True", "-servergamelog" };
        if (!p.UseBattlEye) args.Add("-NoBattlEye");
        var mods = ServerMods(p, s);
        if (mods.Count > 0) { args.Add("-automanagedmods"); args.Add("-mods=" + string.Join(",", mods)); }
        args.Add($"-WinLiveMaxPlayers={p.MaxPlayers}");
        var plat = new List<string>();
        if (p.PlatformPC) plat.Add("PC");
        if (p.PlatformPS5) plat.Add("PS5");
        if (p.PlatformXbox) plat.Add("XSX");
        if (p.PlatformMS) plat.Add("WINGDK");
        if (plat.Count > 0) args.Add("-ServerPlatform=" + string.Join('+', plat));
        if (p.ClusterEnabled)
        {
            args.Add($"-clusterID={s.GetClusterId(p.Game)}");
            args.Add($"-ClusterDirOverride={Quote(ClusterArgDir(p, s))}");
            if (p.NoTransferFromFiltering) args.Add("-NoTransferFromFiltering");
        }
        if (p.NoGameAnalytics) args.Add("-NoGameAnalytics");
        if (p.ExclusiveJoin) args.Add("-exclusivejoin");
        if (p.DestroyWildDinosOnNextStart) args.Add("-ForceRespawnDinos");
        if (p.ForceAllowCaveFlyers) args.Add("-ForceAllowCaveFlyers");
        if (p.NoWildBabies) args.Add("-NoWildBabies");
        if (!string.IsNullOrWhiteSpace(p.ExtraArgs))
            args.AddRange(p.ExtraArgs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return string.Join(' ', args);
    }

    static string BuildAseArgs(ServerProfile p, AppSettings s)
    {
        var url = $"{p.Map}?listen?Port={p.Port}" + (p.Game == GameType.ASE ? $"?QueryPort={p.QueryPort}" : "") +
                  $"?RCONEnabled=True?RCONPort={p.RconPort}?ServerAdminPassword={p.AdminPassword}";
        if (!string.IsNullOrWhiteSpace(p.ServerPassword)) url += $"?ServerPassword={p.ServerPassword}";
        if (p.Game == GameType.ASE) url += $"?MaxPlayers={p.MaxPlayers}";

        var args = new List<string> { url };
        if (p.Game == GameType.ASA)
        {
            args.Add($"-WinLiveMaxPlayers={p.MaxPlayers}");
            var plat = new List<string>();
            if (p.PlatformPC) plat.Add("PC");
            if (p.PlatformPS5) plat.Add("PS5");
            if (p.PlatformXbox) plat.Add("XSX");
            if (p.PlatformMS) plat.Add("WINGDK");
            if (plat.Count > 0) args.Add("-ServerPlatform=" + string.Join('+', plat));
        }
        if (!p.UseBattlEye) args.Add("-NoBattlEye");
        if (p.ExclusiveJoin) args.Add("-exclusivejoin");
        if (p.DestroyWildDinosOnNextStart) args.Add("-ForceRespawnDinos");
        if (p.ForceAllowCaveFlyers) args.Add("-ForceAllowCaveFlyers");
        if (p.NoWildBabies && p.Game == GameType.ASA) args.Add("-NoWildBabies");
        if (p.ClusterEnabled)
        {
            args.Add($"-clusterid={s.GetClusterId(p.Game)}");
            args.Add($"-ClusterDirOverride={Quote(ClusterArgDir(p, s))}");
            if (p.NoTransferFromFiltering) args.Add("-NoTransferFromFiltering");
        }
        var mods = p.AllModIds();
        if (mods.Count > 0)
            args.Add(p.Game == GameType.ASA ? "-mods=" + string.Join(",", mods) : "-automanagedmods");
        args.Add("-servergamelog");
        if (!string.IsNullOrWhiteSpace(p.ExtraArgs))
            args.AddRange(p.ExtraArgs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return string.Join(' ', args);
    }

    public Process Start(ServerProfile p, AppSettings s)
    {
        if (IsRunning(p)) throw new InvalidOperationException($"{p.Name} is already running.");
        if (!File.Exists(p.ExePath)) throw new FileNotFoundException("Server executable not found. Install/update it first.", p.ExePath);
        if (p.ClusterEnabled) Directory.CreateDirectory(s.GetClusterDir(p.Game));

        var useLoader = p.UseServerApi && p.Game == GameType.ASA && File.Exists(ServerApi.Loader(p));
        var exe = useLoader ? ServerApi.Loader(p) : p.ExePath;
        var proc = Process.Start(new ProcessStartInfo(exe, BuildArgs(p, s))
        {
            WorkingDirectory = Path.GetDirectoryName(p.ExePath)!,
            UseShellExecute = false,
        })!;
        if (!useLoader) _running[p] = proc;
        return proc;
    }

    public void Kill(ServerProfile p)
    {
        Find(p)?.Kill(true);
        _running.Remove(p);
    }
}
