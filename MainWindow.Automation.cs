using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;

namespace ObeliskPanel;

public partial class MainWindow : IBotHost
{
    sealed class Runtime
    {
        public bool WasRunning;
        public bool Announced;
        public bool Busy;
        public DateTime NextPlayersPoll;
        public HashSet<string>? Players;
        public string? PlayerCount;
        public string LastRestartKey = "";
        public DateTime LastBackupAttempt;
        public readonly Dictionary<ScheduledMessage, DateTime> MessageSent = new();
    }

    readonly Dictionary<ServerProfile, Runtime> _rt = new();
    DiscordBot? _bot;
    bool _automationRunning;
    static readonly Regex PlayerLineRx = new(@"^\s*\d+\.\s*(.+?),\s*(\S+)\s*$", RegexOptions.Multiline);

    Runtime Rt(ServerProfile p) => _rt.TryGetValue(p, out var r) ? r : _rt[p] = new Runtime { WasRunning = _launcher.IsRunning(p) };

    IReadOnlyList<ServerProfile> IBotHost.Servers => _s.Servers;
    bool IBotHost.IsRunning(ServerProfile p) => _launcher.IsRunning(p);
    string? IBotHost.Players(ServerProfile p) => Rt(p).PlayerCount;
    string IBotHost.Address(ServerProfile p) => PublicIp.Address(_s, p);
    Task IBotHost.Start(ServerProfile p) => Dispatcher.InvokeAsync(() => StartOne(p)).Task;
    Task IBotHost.Stop(ServerProfile p) => Dispatcher.InvokeAsync(() => StopAndWait(p)).Task.Unwrap();
    Task IBotHost.Restart(ServerProfile p) => Dispatcher.InvokeAsync(() => RestartSequence(p, p.RestartCountdown ? p.CountdownMinutes : 0)).Task.Unwrap();
    async Task<string> IBotHost.Rcon(ServerProfile p, string cmd)
    {
        var r = await Dispatcher.InvokeAsync(() => Rcon(p, cmd)).Task.Unwrap();
        if (!cmd.StartsWith("ListPlayers", StringComparison.OrdinalIgnoreCase)) Hook(p, "Admin", $"admin command via Discord: `{cmd}`");
        return r;
    }

    void Hook(ServerProfile p, string kind, string? text = null) => _ = Webhooks.Send(_s, p, kind, text, Log);

    async void InitAutomation()
    {
        await Task.Delay(500);
        Prerequisites.CheckAndPrompt(_s, this, Log);
        if (_s.Servers.Any(x => x.Game == GameType.ASA) || _s.Mode == GameType.ASA)
        {
            await Task.Delay(800);
            await AmazonCerts.CheckAndPrompt(this, Log);
        }
        await PublicIp.Get(_s);
        UpdateStatus();
        await RestartBot();
    }

    public async Task RestartBot()
    {
        if (_bot != null) { await _bot.DisposeAsync(); _bot = null; }
        if (_s.BotEnabled && !string.IsNullOrWhiteSpace(_s.BotToken))
        {
            _bot = new DiscordBot(_s, this);
            try { await _bot.StartAsync(); }
            catch (Exception ex) { Log("Discord bot could not start: " + ex.Message); }
        }
    }

    async Task AutomationTick()
    {
        if (_automationRunning) return;
        _automationRunning = true;
        try
        {
            _ = PublicIp.Get(_s);
            var now = DateTime.Now;
            foreach (var p in _s.Servers.ToList())
            {
                var rt = Rt(p);
                var running = _launcher.IsRunning(p);
                if (running != rt.WasRunning)
                {
                    rt.WasRunning = running;
                    if (!running)
                    {
                        rt.Announced = false; rt.Players = null; rt.PlayerCount = null;
                        Hook(p, "Stopped");
                    }
                }
                if (!running || rt.Busy) continue;

                if (now >= rt.NextPlayersPoll)
                {
                    rt.NextPlayersPoll = now.AddSeconds(45);
                    await PollPlayers(p, rt);
                }
                if (p.WebhooksEnabled && p.WebhookEvents.TryGetValue("Chat", out var chat) && chat.Enabled && rt.Announced)
                    await PollChat(p);
                if (!p.PauseScheduledMessages && rt.Announced) await SendScheduledMessages(p, rt, now);
                if (p.AutoBackupEnabled) await AutoBackup(p, rt, now);
                if (p.RestartEnabled) CheckRestart(p, rt, now);
            }
        }
        catch (Exception ex) { Log("Automation error: " + ex.Message); }
        finally { _automationRunning = false; }
    }

    async Task PollPlayers(ServerProfile p, Runtime rt)
    {
        string r;
        try { r = await RconClient.Send("127.0.0.1", p.RconPort, p.AdminPassword, "ListPlayers"); }
        catch { return; }
        if (!rt.Announced)
        {
            rt.Announced = true;
            Hook(p, "Running");
        }
        var now = PlayerLineRx.Matches(r).Select(m => m.Groups[1].Value.Trim()).ToHashSet();
        rt.PlayerCount = $"{now.Count}/{p.MaxPlayers}";
        if (p == Sel) PlayersText.Text = rt.PlayerCount;
        if (rt.Players != null)
        {
            foreach (var j in now.Except(rt.Players)) Hook(p, "Players", $"– **{j}** joined");
            foreach (var l in rt.Players.Except(now)) Hook(p, "Players", $"– **{l}** left");
        }
        rt.Players = now;
    }

    async Task PollChat(ServerProfile p)
    {
        string r;
        try { r = await RconClient.Send("127.0.0.1", p.RconPort, p.AdminPassword, "GetChat"); }
        catch { return; }
        if (r == "(no response)" || r.Contains("Server received, But no response", StringComparison.OrdinalIgnoreCase)) return;
        var lines = r.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Where(l => !l.StartsWith("SERVER:", StringComparison.OrdinalIgnoreCase)).ToList();
        if (lines.Count > 0) Hook(p, "Chat", "💬\n" + string.Join("\n", lines));
    }

    async Task SendScheduledMessages(ServerProfile p, Runtime rt, DateTime now)
    {
        foreach (var m in p.ScheduledMessages.Where(m => m.Enabled && !string.IsNullOrWhiteSpace(m.Text)))
        {
            rt.MessageSent.TryGetValue(m, out var last);
            bool due;
            if (m.Interval)
            {
                if (last == default) { rt.MessageSent[m] = now; continue; }
                due = now - last >= TimeSpan.FromMinutes(Math.Max(1, m.IntervalMinutes));
            }
            else
            {
                due = TimeSpan.TryParse(m.DailyTime, CultureInfo.InvariantCulture, out var t)
                      && now.TimeOfDay >= t && now.TimeOfDay < t + TimeSpan.FromMinutes(5) && last.Date != now.Date;
            }
            if (!due) continue;
            rt.MessageSent[m] = now;
            await Rcon(p, "ServerChat " + m.Text, log: false);
        }
    }

    async Task AutoBackup(ServerProfile p, Runtime rt, DateTime now)
    {
        if (now - rt.LastBackupAttempt < TimeSpan.FromMinutes(5)) return;
        var newest = Backups.List(p).FirstOrDefault()?.Time ?? DateTime.MinValue;
        if (now - newest < TimeSpan.FromMinutes(Math.Max(5, p.BackupIntervalMinutes))) return;
        rt.LastBackupAttempt = now;
        await Rcon(p, "SaveWorld", log: false);
        await Task.Delay(5000);
        try { await Task.Run(() => Backups.Create(p, _s, Log)); }
        catch (Exception ex) { Log($"[{p.Name}] Automatic backup failed: {ex.Message}"); }
        if (p == Sel) RefreshAutomationPages();
    }

    static IEnumerable<TimeSpan> RestartTimesOf(ServerProfile p) =>
        p.RestartTimes.Select(t => TimeSpan.TryParse(t, CultureInfo.InvariantCulture, out var ts) ? ts : (TimeSpan?)null).OfType<TimeSpan>();

    public static DateTime? NextRestart(ServerProfile p, DateTime from)
    {
        for (int d = 0; d < 8; d++)
        {
            var day = from.Date.AddDays(d);
            if (!p.RestartDays[(int)day.DayOfWeek]) continue;
            var next = RestartTimesOf(p).Select(t => day + t).Where(t => t > from).OrderBy(t => t).FirstOrDefault();
            if (next != default) return next;
        }
        return null;
    }

    void CheckRestart(ServerProfile p, Runtime rt, DateTime now)
    {
        var lead = p.RestartCountdown ? Math.Max(1, p.CountdownMinutes) : 0;
        var target = NextRestart(p, now.AddMinutes(-1));
        if (target == null) return;
        var key = target.Value.ToString("yyyy-MM-dd HH:mm");
        if (rt.LastRestartKey == key) return;
        var minutesLeft = (target.Value - now).TotalMinutes;
        if (minutesLeft > lead) return;
        rt.LastRestartKey = key;
        Log($"[{p.Name}] Scheduled restart for {target:HH:mm} begins.");
        _ = RestartSequence(p, p.RestartCountdown ? Math.Max(1, (int)Math.Ceiling(minutesLeft)) : 0);
    }

    static readonly int[] CountdownMarks = { 60, 45, 30, 20, 15, 10, 5, 4, 3, 2, 1 };

    public async Task RestartSequence(ServerProfile p, int countdownMinutes)
    {
        var rt = Rt(p);
        if (rt.Busy) { Log($"[{p.Name}] A restart is already in progress."); return; }
        rt.Busy = true;
        try
        {
            if (_launcher.IsRunning(p) && countdownMinutes > 0)
            {
                await PollPlayers(p, rt);
                var empty = rt.Players != null && rt.Players.Count == 0;
                if (!(p.SkipCountdownWhenEmpty && empty))
                {
                    if (!string.IsNullOrWhiteSpace(p.RconOnCountdownStart)) await Rcon(p, p.RconOnCountdownStart);
                    for (int m = countdownMinutes; m > 0; m--)
                    {
                        if (!_launcher.IsRunning(p)) break;
                        if (m == countdownMinutes || CountdownMarks.Contains(m))
                        {
                            var msg = p.CountdownMessage.Replace("{minutes}", m.ToString());
                            await Rcon(p, "ServerChat " + msg, log: false);
                            if (p.WebhookIncludeCountdown) Hook(p, "Stopping", $"– restart in {m} minute(s)");
                        }
                        await Task.Delay(TimeSpan.FromMinutes(1));
                    }
                }
            }

            if (_launcher.IsRunning(p)) await StopAndWait(p);

            if (p.BackupBeforeRestart)
            {
                try { await Task.Run(() => Backups.Create(p, _s, Log)); }
                catch (Exception ex) { Log($"[{p.Name}] Backup before restart failed: {ex.Message}"); }
            }

            if (WipeDue(p))
            {
                try
                {
                    if (!p.BackupBeforeRestart) await Task.Run(() => Backups.Create(p, _s, Log));
                    Backups.Wipe(p, Log);
                    p.LastWipeDate = DateTime.Today.ToString("yyyy-MM-dd");
                    Save();
                }
                catch (Exception ex) { Log($"[{p.Name}] Wipe failed: {ex.Message}"); }
            }

            if (p.UpdateOnRestart)
            {
                Hook(p, "Updating");
                try { await SteamCmd.InstallOrUpdate(_s.SteamCmdDir, p, Log); }
                catch (Exception ex) { Log($"[{p.Name}] Update failed: {ex.Message}"); }
            }

            StartOne(p);
        }
        finally { rt.Busy = false; }
    }

    static bool WipeDue(ServerProfile p)
    {
        if (!p.WipeEnabled) return false;
        var today = DateTime.Today;
        if (p.LastWipeDate == today.ToString("yyyy-MM-dd")) return false;
        return p.WipeDaysOfMonth.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(d => int.TryParse(d, out var n) && n == today.Day);
    }

    public async Task StopAndWait(ServerProfile p)
    {
        if (!_launcher.IsRunning(p)) return;
        Hook(p, "Stopping");
        if (!string.IsNullOrWhiteSpace(p.RconOnStopping)) await Rcon(p, p.RconOnStopping);
        await Rcon(p, "SaveWorld");
        await Task.Delay(3000);
        await Rcon(p, "DoExit");
        for (int i = 0; i < 90 && _launcher.IsRunning(p); i++) await Task.Delay(2000);
        if (_launcher.IsRunning(p))
        {
            Log($"[{p.Name}] Server did not exit in time – killing the process.");
            _launcher.Kill(p);
        }
        UpdateStatus();
    }

    void CopyConnect_Click(object sender, RoutedEventArgs e)
    {
        if (Sel == null) return;
        try { Clipboard.SetText(PublicIp.Address(_s, Sel)); Log($"Copied {PublicIp.Address(_s, Sel)}"); } catch { }
    }

    void RefreshAutomationPages()
    {
        if (Sel == null) { BackupsHost.Content = RestartHost.Content = DiscordHost.Content = MessagesHost.Content = ApiHost.Content = null; return; }
        var p = Sel;
        BackupsHost.Content = AutomationPages.Backups(p, _s, () => _launcher.IsRunning(p), Save, Log, RefreshAutomationPages, this);
        RestartHost.Content = AutomationPages.Restart(p, Save, () => _ = RestartSequence(p, p.RestartCountdown ? p.CountdownMinutes : 0));
        DiscordHost.Content = AutomationPages.Discord(p, _s, Save, Log, this);
        MessagesHost.Content = AutomationPages.Messages(p, Save, msg => _ = Rcon(p, "ServerChat " + msg));
        ApiHost.Content = AutomationPages.Api(p, () => _launcher.IsRunning(p), Save, Log, RefreshAutomationPages);
    }
}
