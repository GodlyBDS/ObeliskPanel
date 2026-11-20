using System.IO;
using System.Text.Json;

namespace ObeliskPanel;

public enum GameType { ASA, ASE }

public class ServerProfile
{
    public string Name { get; set; } = "New Server";
    public GameType Game { get; set; } = GameType.ASA;
    public string InstallDir { get; set; } = "";
    public string Map { get; set; } = "TheIsland_WP";
    public string SessionName { get; set; } = "My ARK Server";
    public int Port { get; set; } = 7777;
    public int QueryPort { get; set; } = 27015;
    public int RconPort { get; set; } = 27020;
    public string AdminPassword { get; set; } = "changeme";
    public string ServerPassword { get; set; } = "";
    public int MaxPlayers { get; set; } = 32;
    public bool ClusterEnabled { get; set; } = true;
    public string Mods { get; set; } = "";
    public string MapModId { get; set; } = "";

    public List<string> AllModIds()
    {
        var ids = Mods.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (!string.IsNullOrWhiteSpace(MapModId)) { ids.Remove(MapModId.Trim()); ids.Insert(0, MapModId.Trim()); }
        return ids;
    }
    public string ExtraArgs { get; set; } = "";

    public bool AutoFirewallRule { get; set; } = true;
    public bool UseBattlEye { get; set; }
    public bool NoGameAnalytics { get; set; } = true;
    public bool NoTransferFromFiltering { get; set; }
    public bool ForceAllowCaveFlyers { get; set; }
    public bool NoWildBabies { get; set; }
    public bool DestroyWildDinosOnNextStart { get; set; }
    public bool ExclusiveJoin { get; set; }
    public bool PlatformPC { get; set; } = true;
    public bool PlatformPS5 { get; set; } = true;
    public bool PlatformXbox { get; set; } = true;
    public bool PlatformMS { get; set; } = true;
    public bool ExcludeFromMulti { get; set; }

    public bool AutoBackupEnabled { get; set; }
    public int BackupIntervalMinutes { get; set; } = 120;
    public int MaxBackups { get; set; } = 50;
    public bool BackupIncludeCluster { get; set; }

    public bool RestartEnabled { get; set; }
    public bool[] RestartDays { get; set; } = { true, true, true, true, true, true, true };
    public List<string> RestartTimes { get; set; } = new() { "06:00" };
    public bool RestartCountdown { get; set; } = true;
    public int CountdownMinutes { get; set; } = 15;
    public string CountdownMessage { get; set; } = "Server restart in {minutes} minute(s)!";
    public bool SkipCountdownWhenEmpty { get; set; } = true;
    public bool UpdateOnRestart { get; set; } = true;
    public bool BackupBeforeRestart { get; set; } = true;
    public string RconOnCountdownStart { get; set; } = "";
    public string RconOnStopping { get; set; } = "";

    public bool WipeEnabled { get; set; }
    public string WipeDaysOfMonth { get; set; } = "1";
    public bool WipePlayers { get; set; }
    public string LastWipeDate { get; set; } = "";

    public bool WebhooksEnabled { get; set; }
    public Dictionary<string, WebhookEvent> WebhookEvents { get; set; } = WebhookEvent.Defaults();
    public bool WebhookPrependSessionName { get; set; } = true;
    public bool WebhookIncludeCountdown { get; set; } = true;

    public List<ScheduledMessage> ScheduledMessages { get; set; } = new();
    public bool PauseScheduledMessages { get; set; }

    public bool UseServerApi { get; set; }

    public string SaveDir => Game == GameType.ASA
        ? Path.Combine(InstallDir, @"ShooterGame\Saved\SavedArks", Map)
        : Path.Combine(InstallDir, @"ShooterGame\Saved\SavedArks");
    public string BinDir => Path.Combine(InstallDir, @"ShooterGame\Binaries\Win64");

    public override string ToString() => $"{Name} – {Map}";

    public string ExePath => Path.Combine(InstallDir, @"ShooterGame\Binaries\Win64",
        Game == GameType.ASA ? "ArkAscendedServer.exe" : "ShooterGameServer.exe");

    public int SteamAppId => Game == GameType.ASA ? 2430930 : 376030;

    public string IniDir => Path.Combine(InstallDir, @"ShooterGame\Saved\Config\WindowsServer");
    public string GameIniPath => Path.Combine(IniDir, "Game.ini");
    public string GameUserSettingsPath => Path.Combine(IniDir, "GameUserSettings.ini");
    public string EngineIniPath => Path.Combine(IniDir, "Engine.ini");
    public string AdminListPath => Path.Combine(InstallDir, @"ShooterGame\Saved\AllowedCheaterAccountIDs.txt");
    public string WhitelistPath => Path.Combine(InstallDir, @"ShooterGame\Binaries\Win64\PlayersExclusiveJoinList.txt");
}

public class WebhookEvent
{
    public bool Enabled { get; set; }
    public string Text { get; set; } = "";
    public string Webhook { get; set; } = "";

    public static readonly (string Key, string Label, string Text)[] Kinds =
    {
        ("Starting", "Server Starting Up", "is now starting up"),
        ("Running", "Server Running", "is now running – connect: {ip}"),
        ("Stopping", "Server Stopping", "is now shutting down"),
        ("Stopped", "Server Stopped", "is now stopped"),
        ("Updating", "Server Updating", "is now updating"),
        ("Players", "Player Join/Leave", ""),
        ("Admin", "Admin Commands", ""),
        ("Chat", "Global Chat Messages", ""),
    };

    public static Dictionary<string, WebhookEvent> Defaults() =>
        Kinds.ToDictionary(k => k.Key, k => new WebhookEvent { Text = k.Text });
}

public class ScheduledMessage
{
    public string Text { get; set; } = "";
    public bool Interval { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;
    public string DailyTime { get; set; } = "20:00";
    public bool Enabled { get; set; } = true;
}

public class NamedWebhook
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}

public class AppSettings
{
    public List<NamedWebhook> Webhooks { get; set; } = new();
    public bool BotEnabled { get; set; }
    public string BotToken { get; set; } = "";
    public string BotAdminRoleId { get; set; } = "";
    public string BotStatusChannelId { get; set; } = "";
    public string BotStatusMessageId { get; set; } = "";
    public string PublicAddressOverride { get; set; } = "";

    static string AppData => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ObeliskPanel");

    public string SteamCmdDir { get; set; } = Path.Combine(AppData, "SteamCmd");
    public GameType Mode { get; set; } = GameType.ASA;

    public string? ServersDirAsa { get; set; }
    public string? ServersDirAse { get; set; }
    public static string DefaultServersDir(GameType g) => Path.Combine(AppData, g.ToString(), "Servers");

    public string ClusterId { get; set; } = "MyCluster";
    public string ClusterIdAse { get; set; } = "MyClusterASE";

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ClusterDir { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ClusterDirAse { get; set; }

    public string GetServersDir(GameType g) =>
        (g == GameType.ASA ? ServersDirAsa : ServersDirAse) is { Length: > 0 } d ? d : DefaultServersDir(g);
    public void SetServersDir(GameType g, string v) { if (g == GameType.ASA) ServersDirAsa = v; else ServersDirAse = v; }

    public string GetClusterId(GameType g) => g == GameType.ASA ? ClusterId : ClusterIdAse;
    public string GetClusterDir(GameType g) => Path.Combine(GetServersDir(g), "clusters");
    public void SetClusterId(GameType g, string v) { if (g == GameType.ASA) ClusterId = v; else ClusterIdAse = v; }
    public string CurseForgeApiKey { get; set; } = "";
    public string OodleDllPath { get; set; } = "";
    public string SteamApiKey { get; set; } = "";
    public string Theme { get; set; } = "System";
    public List<ServerProfile> Servers { get; set; } = new();
    public Dictionary<string, string> ModNames { get; set; } = new();
    public HashSet<string> CosmeticMods { get; set; } = new();
    public Dictionary<string, List<string>> ModSections { get; set; } = new();
}

public static class ConfigStore
{
    static readonly string Dir = Environment.GetEnvironmentVariable("OBELISKPANEL_DATA") is { Length: > 0 } custom
        ? custom
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ObeliskPanel");
    static readonly string FilePath = Path.Combine(Dir, "settings.json");
    static readonly string OldFilePath = Environment.GetEnvironmentVariable("OBELISKPANEL_DATA") is { Length: > 0 }
        ? ""
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArkManager", "settings.json");
    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath) && File.Exists(OldFilePath))
            {
                Directory.CreateDirectory(Dir);
                File.Copy(OldFilePath, FilePath);
            }
            if (File.Exists(FilePath))
                return Migrate(JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Opts) ?? new());
        }
        catch { }
        return new AppSettings();
    }

    static AppSettings Migrate(AppSettings s)
    {
        foreach (var g in new[] { GameType.ASA, GameType.ASE })
        {
            if ((g == GameType.ASA ? s.ServersDirAsa : s.ServersDirAse) != null) continue;
            string? dir = null;
            var first = s.Servers.FirstOrDefault(p => p.Game == g && p.InstallDir != "");
            if (first != null) dir = Path.GetDirectoryName(Path.GetDirectoryName(first.InstallDir.TrimEnd('\\', '/')));
            var oldCluster = g == GameType.ASA ? s.ClusterDir : s.ClusterDirAse;
            if (dir == null && oldCluster != null && Path.GetFileName(oldCluster.TrimEnd('\\', '/')).Equals("clusters", StringComparison.OrdinalIgnoreCase))
                dir = Path.GetDirectoryName(oldCluster.TrimEnd('\\', '/'));
            s.SetServersDir(g, dir ?? AppSettings.DefaultServersDir(g));
        }
        s.ClusterDir = s.ClusterDirAse = null;
        return s;
    }

    public static void Save(AppSettings s)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(s, Opts));
    }
}
