using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ObeliskPanel;

public partial class MainWindow : Window
{
    readonly AppSettings _s = ConfigStore.Load();
    readonly ServerLauncher _launcher = new();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    IniContext? _ctx;
    ServerProfile? _loaded;
    int _tick;

    static readonly string[] Pages =
    {
        "Main", "General", "Dinos General and Taming", "Rates", "Breeding", "Stat Multipliers",
        "Day and Night Cycle", "Decay and Spoil", "Config Files",
    };
    static readonly string[] RconPresets =
    {
        "SaveWorld", "ListPlayers", "GetChat", "DestroyWildDinos", "SetTimeOfDay 08:00",
        "ServerChat ", "KickPlayer ", "BanPlayer ", "UnbanPlayer ", "AllowPlayerToJoinNoCheck ", "DoExit",
    };
    static readonly Regex PlayerRx = new(@"^\s*\d+\.\s*.+,\s*\S+\s*$", RegexOptions.Multiline);

    ServerProfile? Sel => ServerTabs.SelectedItem as ServerProfile;

    public MainWindow()
    {
        InitializeComponent();
        NavList.ItemsSource = Pages;
        NavList.SelectedIndex = 0;
        RconPreset.ItemsSource = RconPresets;
        UpdateThemeButton();
        (_s.Mode == GameType.ASA ? ModeAsa : ModeAse).IsChecked = true;
        ApplyThemeBrushes();
        RefreshTabs(ModeServers.FirstOrDefault());
        _timer.Tick += Timer_Tick;
        _timer.Start();
        InitAutomation();
        _ = CheckCosmeticMods();
    }

    async Task CheckCosmeticMods()
    {
        if (string.IsNullOrWhiteSpace(_s.CurseForgeApiKey)) return;
        var ids = _s.Servers.Where(p => p.Game == GameType.ASA).SelectMany(p => p.AllModIds())
                            .Where(i => int.TryParse(i, out _)).Distinct().ToList();
        if (ids.Count == 0) return;
        try
        {
            using var cf = new CurseForge(_s.CurseForgeApiKey);
            var mods = await cf.GetMods(ids.Select(int.Parse));
            foreach (var m in mods)
            {
                var id = m.Id.ToString();
                var cos = m.Categories.Split(',', StringSplitOptions.TrimEntries).Any(c => c.Equals("Custom Cosmetics", StringComparison.OrdinalIgnoreCase));
                if (cos && _s.CosmeticMods.Add(id))
                    Log($"'{m.Name}' ({id}) is a Custom Cosmetic – it only works in the client and would make the server's mod install fail. ObeliskPanel leaves it out when starting servers.");
                else if (!cos) _s.CosmeticMods.Remove(id);
            }
            Save();
        }
        catch (Exception ex) { Log("Cosmetic mod check on CurseForge failed: " + ex.Message); }
    }

    static readonly string[] ThemeCycle = { "System", "Light", "Dark" };

    void UpdateThemeButton()
    {
        ThemeBtn.Content = _s.Theme switch { "Light" => "☀", "Dark" => "🌙", _ => "🖥" };
        ThemeBtn.ToolTip = $"Appearance: {_s.Theme} (click to change)";
    }

    void ThemeBtn_Click(object sender, RoutedEventArgs e)
    {
        _s.Theme = ThemeCycle[(Array.IndexOf(ThemeCycle, _s.Theme) + 1) % ThemeCycle.Length];
        UpdateThemeButton();
        Save();
        Theme.Apply(_s.Theme);
        ApplyThemeBrushes();
        if (Sel != null) { _loaded = null; LoadServer(); _loaded = Sel; }
    }

    List<ServerProfile> ModeServers => _s.Servers.Where(x => x.Game == _s.Mode).ToList();

    void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var mode = ModeAsa.IsChecked == true ? GameType.ASA : GameType.ASE;
        if (mode == _s.Mode) return;
        _s.Mode = mode;
        Save();
        _loaded = null;
        ApplyThemeBrushes();
        RefreshTabs(ModeServers.FirstOrDefault());
    }

    void ApplyThemeBrushes()
    {
        var modeBrush = Theme.ModeOnDark(_s.Mode);
        Resources["ModeAccent"] = modeBrush;
        ModeTitle.Text = _s.Mode == GameType.ASA ? "ARK: Survival Ascended" : "ARK: Survival Evolved";
        ModeTitle.Foreground = modeBrush;
        ModeStripe.Background = modeBrush;
        foreach (var card in new[] { StatusCard, PageCard })
        {
            card.Background = Theme.Card;
            card.BorderBrush = Theme.Border;
        }
        UpdateStatus();
    }

    public void Log(string msg) => Dispatcher.Invoke(() =>
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
        LogBox.ScrollToEnd();
    });

    void Save() => ConfigStore.Save(_s);

    void RefreshTabs(ServerProfile? sel)
    {
        ServerTabs.ItemsSource = null;
        ServerTabs.ItemsSource = ModeServers;
        ServerTabs.SelectedItem = sel;
        if (sel == null) { ServerPanel.IsEnabled = false; ShowEmpty(); }
    }

    void ServerTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ServerPanel.IsEnabled = Sel != null;
        if (Sel == null) { _loaded = null; ShowEmpty(); return; }
        if (Sel == _loaded) return;
        _loaded = Sel;
        LoadServer();
    }

    void ShowEmpty()
    {
        Title = $"ObeliskPanel – {_s.Mode} mode" + AdminNote;
        PageHost.Content = new TextBlock
        {
            Text = $"No {_s.Mode} servers yet.\nClick \"＋ New Server\" to create one.\n\n" +
                   $"New servers are created in:\n{_s.GetServersDir(_s.Mode)}\n(change it with the ⚙ Settings button at the top right)",
            Margin = new Thickness(16), FontSize = 15, Foreground = Theme.Muted,
        };
        ModsHost.Content = null;
        RefreshAutomationPages();
        ConnectText.Text = "–";
        PlayersText.Text = PidText.Text = MapText.Text = "–";
    }

    void LoadServer()
    {
        var p = Sel!;
        _ctx = new IniContext(p, () => _launcher.IsRunning(p), Log);
        UpdateTitle();
        ShowPage();
        ModsHost.Content = new ModsPanel(p, _s, () => _ctx!, OnProfileChanged, Log, this);
        RefreshAutomationPages();
        PlayersText.Text = "–";
        UpdateStatus();
    }

    static string AdminNote => App.IsAdmin ? "" : "   (not running as administrator)";

    void UpdateTitle()
    {
        if (Sel != null) Title = $"ObeliskPanel – {Sel.Game} [{Sel.Map} - {Sel.SessionName}] - [{Sel.Name}]" + AdminNote;
    }

    void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e) => ShowPage();

    void ShowPage()
    {
        if (Sel == null || _ctx == null) return;
        var page = NavList.SelectedItem as string ?? "Main";
        PageHost.Content = page switch
        {
            "Main" => MainPage.Build(Sel, _ctx, _s, OnProfileChanged, Log, this),
            "Stat Multipliers" => SettingsUi.StatsPage(_ctx, ShowPage),
            "Config Files" => ConfigFilesPage(Sel),
            _ => SettingsUi.BuildPage(_ctx, SettingDefs.All.Where(d => d.Page == page && d.AppliesTo(Sel.Game)), ShowPage),
        };
    }

    void OnProfileChanged()
    {
        Save();
        ServerTabs.Items.Refresh();
        UpdateTitle();
    }

    FrameworkElement ConfigFilesPage(ServerProfile p)
    {
        var sp = new StackPanel { Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Left };
        void B(string t, Action a)
        {
            var b = new Button { Content = t, Width = 280, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 3, 0, 3) };
            b.Click += (_, _) => a();
            sp.Children.Add(b);
        }
        void Reload() { _ctx = new IniContext(p, () => _launcher.IsRunning(p), Log); }
        B("Edit Game.ini / GameUserSettings.ini", () => { Save(); new IniEditorWindow(p) { Owner = this }.ShowDialog(); Reload(); });
        B("Open INI Folder", () => { Directory.CreateDirectory(p.IniDir); Process.Start("explorer.exe", p.IniDir); });
        B("Open Server Folder", () => { Directory.CreateDirectory(p.InstallDir); Process.Start("explorer.exe", p.InstallDir); });
        B("Edit Whitelist", () => new TextFileWindow(p.WhitelistPath, "Whitelist (one EOS ID per line)") { Owner = this }.ShowDialog());
        B("Edit Admin List (AllowedCheaterAccountIDs)", () => new TextFileWindow(p.AdminListPath, "Admins (one EOS ID per line)") { Owner = this }.ShowDialog());
        return sp;
    }

    int _autoTick;

    async void Timer_Tick(object? sender, EventArgs e)
    {
        UpdateStatus();
        if (_autoTick % 3 == 0) ProbeStarting();
        if (++_autoTick % 10 == 0) await AutomationTick();
        if (Sel != null && ++_tick % 15 == 0 && _launcher.IsRunning(Sel))
        {
            var r = await Rcon(Sel, "ListPlayers", log: false);
            PlayersText.Text = r == "" ? "–" : $"{PlayerRx.Matches(r).Count}/{Sel.MaxPlayers}";
        }
    }

    void UpdateStatus()
    {
        if (Sel == null) return;
        var proc = _launcher.Find(Sel);
        var state = StateOf(Sel);
        var color = state switch { ServerState.Running => Theme.Good, ServerState.Stopped => Theme.Bad, _ => Theme.Warn };
        StateText.Text = state switch
        {
            ServerState.Starting => "Starting up…",
            ServerState.Stopping => "Stopping…",
            _ => state.ToString(),
        };
        StateText.Foreground = color;
        StateDot.Fill = color;
        StateBadge.BorderBrush = color;
        StateBadge.BorderThickness = new Thickness(1);
        StateBadge.Background = Theme.RowAlt;
        PidText.Text = proc != null ? proc.Id.ToString() : "–";
        MapText.Text = $"{Sel.Map.Replace("_WP", "")} · {Sel.Port}";
        ConnectText.Text = PublicIp.Address(_s, Sel);
        StartBtn.IsEnabled = proc == null;
        StopBtn.IsEnabled = KillBtn.IsEnabled = proc != null;
    }

    void Settings_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow(_s, Save, Log) { Owner = this }.ShowDialog();
        Save();
        if (Sel == null) ShowEmpty(); else { ShowPage(); RefreshAutomationPages(); }
        UpdateStatus();
        _ = RestartBot();
    }

    ServerProfile NewProfile()
    {
        int n = 1;
        var game = _s.Mode;
        string Dir(int i) => Path.Combine(_s.GetServersDir(game), $"Server {i}");
        while (Directory.Exists(Dir(n)) || _s.Servers.Any(x => x.Name == $"Server {n}")) n++;
        return new ServerProfile
        {
            Game = game,
            Map = game == GameType.ASA ? "TheIsland_WP" : "TheIsland",
            Name = $"Server {n}",
            InstallDir = Path.Combine(Dir(n), "Server Files"),
            Port = _s.Servers.Count == 0 ? 7777 : _s.Servers.Max(x => x.Port) + 1,
            QueryPort = _s.Servers.Count == 0 ? 27015 : _s.Servers.Max(x => x.QueryPort) + 1,
            RconPort = _s.Servers.Count == 0 ? 27020 : _s.Servers.Max(x => x.RconPort) + 1,
        };
    }

    void Add_Click(object sender, RoutedEventArgs e) => CreateServer(Sel);

    void Duplicate_Click(object sender, RoutedEventArgs e) => CreateServer(Sel);

    void CreateServer(ServerProfile? preselect)
    {
        var p = NewProfile();
        var dlg = new NewServerWindow(p.Name, _s.Mode, _s.Servers, preselect) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        p.Name = dlg.ServerName;
        p.Map = dlg.Map;
        p.SessionName = dlg.SessionName;
        var src = dlg.Source;

        if (src != null && dlg.CopySettings)
        {
            p.MaxPlayers = src.MaxPlayers; p.AdminPassword = src.AdminPassword; p.ServerPassword = src.ServerPassword;
            p.ClusterEnabled = src.ClusterEnabled; p.ExtraArgs = src.ExtraArgs; p.AutoFirewallRule = src.AutoFirewallRule;
            p.UseBattlEye = src.UseBattlEye; p.ExclusiveJoin = src.ExclusiveJoin; p.ExcludeFromMulti = src.ExcludeFromMulti;
            p.PlatformPC = src.PlatformPC; p.PlatformPS5 = src.PlatformPS5; p.PlatformXbox = src.PlatformXbox; p.PlatformMS = src.PlatformMS;

            Directory.CreateDirectory(p.IniDir);
            foreach (var (from, to) in new[] { (src.GameIniPath, p.GameIniPath), (src.GameUserSettingsPath, p.GameUserSettingsPath), (src.EngineIniPath, p.EngineIniPath) })
                if (File.Exists(from)) File.Copy(from, to, true);
            if (File.Exists(src.WhitelistPath)) { Directory.CreateDirectory(Path.GetDirectoryName(p.WhitelistPath)!); File.Copy(src.WhitelistPath, p.WhitelistPath, true); }
            if (File.Exists(src.AdminListPath)) { Directory.CreateDirectory(Path.GetDirectoryName(p.AdminListPath)!); File.Copy(src.AdminListPath, p.AdminListPath, true); }
        }

        if (src != null && dlg.CopyMods)
        {
            p.Mods = src.Mods;
            if (!dlg.CopySettings)
            {
                var ids = src.Mods.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var sections = ids.Select(id => ModsPanel.Key(src.Game, id)).Where(_s.ModSections.ContainsKey).SelectMany(k => _s.ModSections[k]);
                var srcGus = IniFile.Load(src.GameUserSettingsPath); var srcGame = IniFile.Load(src.GameIniPath);
                var dstGus = IniFile.Load(p.GameUserSettingsPath); var dstGame = IniFile.Load(p.GameIniPath);
                foreach (var entry in sections)
                {
                    var bar = entry.IndexOf('|');
                    bool game = entry[..bar] == "Game";
                    var sec = entry[(bar + 1)..];
                    foreach (var (k, v) in (game ? srcGame : srcGus).Entries(sec)) (game ? dstGame : dstGus).Set(sec, k, v);
                }
                dstGus.Save(); dstGame.Save();
            }
        }

        var gus = IniFile.Load(p.GameUserSettingsPath);
        gus.Set("SessionSettings", "SessionName", p.SessionName);
        gus.Set(SettingDefs.SS, "RCONPort", p.RconPort.ToString());
        gus.Set(SettingDefs.SS, "ServerAdminPassword", p.AdminPassword);
        gus.Save();

        _s.Servers.Add(p); Save(); RefreshTabs(p); _ = _bot?.RefreshCommands();
        var what = src == null ? "" : $" – copied from '{src.Name}': " +
            string.Join(" + ", new[] { dlg.CopySettings ? "settings" : null, dlg.CopyMods ? "mods" : null }.OfType<string>());
        Log($"{p.Name} created ({p.Map}, Port {p.Port}, RCON {p.RconPort}){what}. Click 'Update' to install the server files.");
    }

    void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (Sel == null) return;
        if (MessageBox.Show($"Remove profile '{Sel.Name}'? The server files stay on disk.", "Remove", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        _s.Servers.Remove(Sel); Save(); _loaded = null; RefreshTabs(ModeServers.FirstOrDefault()); _ = _bot?.RefreshCommands();
    }

    async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (Sel == null) return;
        var p = Sel; Save();
        if (_launcher.IsRunning(p)) { Log("Stop the server before updating."); return; }
        MainTabs.SelectedIndex = 1;
        Hook(p, "Updating");
        try { await SteamCmd.InstallOrUpdate(_s.SteamCmdDir, p, Log); }
        catch (Exception ex) { Log("Error: " + ex.Message); }
    }

    void Start_Click(object sender, RoutedEventArgs e) { if (Sel != null) { Save(); StartOne(Sel); } }

    void StartOne(ServerProfile p)
    {
        try
        {
            CheckPortConflicts(p);
            var gus = p == Sel && _ctx != null ? _ctx.Gus : IniFile.Load(p.GameUserSettingsPath);
            gus.Set("SessionSettings", "SessionName", p.SessionName);
            gus.Set(SettingDefs.SS, "RCONEnabled", "True");
            gus.Set(SettingDefs.SS, "RCONPort", p.RconPort.ToString());
            gus.Set(SettingDefs.SS, "ServerAdminPassword", p.AdminPassword);
            if (string.IsNullOrWhiteSpace(p.ServerPassword)) gus.Remove(SettingDefs.SS, "ServerPassword");
            else gus.Set(SettingDefs.SS, "ServerPassword", p.ServerPassword);
            var allMods = p.AllModIds();
            if (p.Game == GameType.ASE)
            {
                if (allMods.Count == 0) gus.Remove(SettingDefs.SS, "ActiveMods");
                else gus.Set(SettingDefs.SS, "ActiveMods", string.Join(",", allMods));
                var gameIni = p == Sel && _ctx != null ? _ctx.Game : IniFile.Load(p.GameIniPath);
                gameIni.SetAll("ModInstaller", "ModIDS", allMods);
                gameIni.Save();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(p.MapModId)) gus.Remove(SettingDefs.SS, "ActiveMapMod");
                else gus.Set(SettingDefs.SS, "ActiveMapMod", p.MapModId.Trim());
            }
            gus.Save();
            if (p.Game == GameType.ASA)
            {
                var skipped = allMods.Where(_s.CosmeticMods.Contains).ToList();
                if (skipped.Count > 0)
                    Log($"[{p.Name}] Skipping Custom Cosmetic mods (client-only): " +
                        string.Join(", ", skipped.Select(i => _s.ModNames.TryGetValue(i, out var n) ? $"{n} ({i})" : i)));
            }
            if (p.AutoFirewallRule) Firewall.EnsureRule(p, Log);
            var proc = _launcher.Start(p, _s);
            var rt = Rt(p);
            rt.WasRunning = true; rt.Announced = false; rt.Players = null; rt.NextPlayersPoll = DateTime.Now.AddSeconds(60);
            Hook(p, "Starting");
            Log($"[{p.Name}] started (PID {proc.Id}): {p.Map} on port {p.Port}" + (p.ClusterEnabled ? $", cluster '{_s.GetClusterId(p.Game)}'" : ""));
            if (p.DestroyWildDinosOnNextStart) { p.DestroyWildDinosOnNextStart = false; Save(); if (p == Sel) ShowPage(); }
        }
        catch (Exception ex) { Log($"[{p.Name}] Start failed: {ex.Message}"); }
        UpdateStatus();
    }

    void CheckPortConflicts(ServerProfile p)
    {
        foreach (var o in _s.Servers.Where(o => o != p && _launcher.IsRunning(o)))
        {
            var mine = new[] { p.Port, p.QueryPort, p.RconPort };
            var theirs = new[] { o.Port, o.QueryPort, o.RconPort };
            if (mine.Intersect(theirs).Any())
                throw new InvalidOperationException($"Port conflict with running server '{o.Name}'.");
        }
    }

    async void StartAll_Click(object sender, RoutedEventArgs e)
    {
        Save();
        foreach (var p in ModeServers.Where(x => !x.ExcludeFromMulti && !_launcher.IsRunning(x)))
        {
            StartOne(p);
            await Task.Delay(10000);
        }
    }

    Task StopOne(ServerProfile p) => StopAndWait(p);

    async void Stop_Click(object sender, RoutedEventArgs e) { if (Sel != null) await StopOne(Sel); }

    async void StopAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in ModeServers.Where(x => !x.ExcludeFromMulti && _launcher.IsRunning(x)))
            await StopOne(p);
    }

    void Kill_Click(object sender, RoutedEventArgs e)
    {
        if (Sel == null) return;
        if (MessageBox.Show("Kill the process without saving?", "Kill", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        _launcher.Kill(Sel); Log($"[{Sel.Name}] Process killed."); UpdateStatus();
    }

    void ShowArgs_Click(object sender, RoutedEventArgs e)
    {
        if (Sel == null) return;
        MainTabs.SelectedIndex = 1;
        Log($"\"{Sel.ExePath}\" {ServerLauncher.BuildArgs(Sel, _s)}");
    }

    public async Task<string> Rcon(ServerProfile p, string cmd, bool log = true)
    {
        try
        {
            if (log) Log($"[{p.Name}] > {cmd}");
            var r = await RconClient.Send("127.0.0.1", p.RconPort, p.AdminPassword, cmd);
            if (log) Log($"[{p.Name}] {r}");
            return r;
        }
        catch (Exception ex)
        {
            if (log) Log($"[{p.Name}] RCON error: {ex.Message}");
            return "";
        }
    }

    async void RconSend_Click(object sender, RoutedEventArgs e)
    {
        if (Sel == null || string.IsNullOrWhiteSpace(RconInput.Text)) return;
        var cmd = RconInput.Text.Trim();
        RconInput.Clear();
        Hook(Sel, "Admin", $"admin command: `{cmd}`");
        await Rcon(Sel, cmd);
    }

    void RconInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) RconSend_Click(sender, e); }

    void RconPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RconPreset.SelectedItem is string s) { RconInput.Text = s; RconInput.Focus(); RconInput.CaretIndex = s.Length; }
    }

    void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    void Close_Click(object sender, RoutedEventArgs e) => Close();

    void Window_StateChanged(object? sender, EventArgs e)
    {
        bool max = WindowState == WindowState.Maximized;
        RootPanel.Margin = max ? new Thickness(7) : new Thickness(0);
        MaxBtn.Content = max ? "" : "";
        MaxBtn.ToolTip = max ? "Restore" : "Maximize";
    }

    void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        Save();
        if (_bot != null) _ = _bot.DisposeAsync().AsTask();
    }
}
