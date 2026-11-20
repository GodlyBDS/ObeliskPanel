using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ObeliskPanel;

public class SettingsWindow : Window
{
    readonly AppSettings _s;
    readonly Action _save;
    readonly Action<string> _log;
    readonly StackPanel _sp = new() { Margin = new Thickness(20, 14, 20, 20) };

    public SettingsWindow(AppSettings s, Action save, Action<string> log)
    {
        _s = s; _save = save; _log = log;
        Title = "ObeliskPanel – Settings";
        Width = 760; Height = 760; MinWidth = 560; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = new ScrollViewer { Content = _sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        Header("Server Folders");
        Hint("New servers are created here as \"Server N\\Server Files\". The cluster data of each game lives in the same " +
             "folder under \"clusters\". Existing servers keep their folder – changing this only affects new servers and the cluster location.");
        foreach (var g in new[] { GameType.ASA, GameType.ASE }) GameSection(g);

        Header("Tools");
        FolderRow("SteamCMD Folder", _s.SteamCmdDir, v => _s.SteamCmdDir = v,
            "SteamCMD is downloaded here automatically the first time you click 'Update'.");
        FileRow("Oodle DLL (for reading ASA mod files)", _s.OodleDllPath, v => _s.OodleDllPath = v,
            "oo2core_9_win64.dll – many Unreal games and tools ship it. Leave empty to search automatically. Found: " +
            (IoStore.FindOodle(_s.OodleDllPath) ?? "none"));

        Header("Requirements");
        Requirements();

        Header("Network");
        TextRow("Public address override", _s.PublicAddressOverride, v => _s.PublicAddressOverride = v,
            $"Leave empty to detect your public IP automatically (currently: {PublicIp.Current ?? "not detected yet"}). Enter a domain or IP if players should use that instead.");

        Header("Discord");
        WebhookList();
        var bot = new CheckBox { Content = "Enable Discord bot", IsChecked = _s.BotEnabled, Margin = new Thickness(0, 14, 0, 0) };
        bot.Click += (_, _) => { _s.BotEnabled = bot.IsChecked == true; _save(); };
        _sp.Children.Add(bot);
        MaskedKey("Bot Token", _s.BotToken, v => _s.BotToken = v,
            "discord.com/developers/applications → New Application → Bot → Reset Token. Invite it with OAuth2 → URL Generator → scopes 'bot' + 'applications.commands'.");
        TextRow("Admin Role ID (optional)", _s.BotAdminRoleId, v => _s.BotAdminRoleId = v,
            "Members with this role may start/stop/restart servers and send RCON. Discord administrators always can. Right-click a role → Copy Role ID (developer mode).");
        TextRow("Status Channel ID (optional)", _s.BotStatusChannelId, v => { if (v != _s.BotStatusChannelId) _s.BotStatusMessageId = ""; _s.BotStatusChannelId = v; },
            "The bot posts a status message there and keeps it updated every minute (online/offline, players, connect address).");

        Header("API Keys");
        MaskedKey("CurseForge API Key (ASA mods)", _s.CurseForgeApiKey, v => _s.CurseForgeApiKey = v,
            "Free at console.curseforge.com. Needed for the mod browser, mod names and mod settings.");
        MaskedKey("Steam Web API Key (ASE Workshop search)", _s.SteamApiKey, v => _s.SteamApiKey = v,
            "Free at steamcommunity.com/dev/apikey. Only needed to search the Workshop – adding ASE mods by ID or link works without it.");

        var close = new Button { Content = "Close", MinWidth = 110, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0), IsDefault = true };
        close.Click += (_, _) => Close();
        _sp.Children.Add(close);
    }

    void Requirements()
    {
        void Line(string name, bool ok, string detail, string? url)
        {
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
            var mark = new TextBlock { Text = ok ? "✔" : "✖", Width = 22, FontWeight = FontWeights.Bold, Foreground = ok ? Theme.Good : Theme.Bad, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(mark, Dock.Left);
            row.Children.Add(mark);
            if (url != null)
            {
                var b = new Button { Content = ok ? "Download page" : "Install…", MinWidth = 120, Margin = new Thickness(8, 0, 0, 0) };
                b.Click += (_, _) => Prerequisites.OpenPage(url);
                DockPanel.SetDock(b, Dock.Right);
                row.Children.Add(b);
            }
            var text = new StackPanel();
            text.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold });
            text.Children.Add(new TextBlock { Text = detail, Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap });
            row.Children.Add(text);
            _sp.Children.Add(row);
        }
        Line("Windows 10 / 11 (64-bit)", Environment.Is64BitOperatingSystem && Environment.OSVersion.Version.Major >= 10,
             "ARK dedicated servers only run on 64-bit Windows.", null);
        Line(".NET 10 Desktop Runtime", true, "Installed – ObeliskPanel is running. If it is missing, Windows offers the official download when you start ObeliskPanel.", null);
        foreach (var p in Prerequisites.All)
            Line(p.Name, p.IsInstalled(), p.Why + (p.Needed(_s) ? "" : "  (not needed for your current servers)"), p.Url);
        Line("Administrator rights", App.IsAdmin, "Needed for firewall rules, certificates and controlling servers started as administrator.", null);
        Line("Amazon Root CA 1 + Amazon RSA 2048 M02", !AmazonCerts.Missing, "Needed by ARK: Survival Ascended servers for the server list and online services.", null);
        Line("SteamCMD", File.Exists(Path.Combine(_s.SteamCmdDir, "steamcmd.exe")), "Downloaded automatically from Valve the first time you click 'Update'.", null);
    }

    void WebhookList()
    {
        Label("Webhooks");
        Hint("Discord: Channel settings → Integrations → Webhooks → New Webhook → Copy Webhook URL. Give each one a name and pick it per message type in the server's Discord tab.");
        var list = new StackPanel();
        _sp.Children.Add(list);
        void Render()
        {
            list.Children.Clear();
            foreach (var w in _s.Webhooks.ToList())
            {
                var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
                var name = new TextBox { Text = w.Name, Width = 160, Margin = new Thickness(0, 0, 8, 0), ToolTip = "Name" };
                name.LostFocus += (_, _) => { w.Name = name.Text.Trim(); _save(); };
                var url = new PasswordBox { Password = w.Url, ToolTip = "Webhook URL (hidden)" };
                url.LostFocus += (_, _) => { w.Url = url.Password.Trim(); _save(); };
                var test = new Button { Content = "Test", MinWidth = 60, Margin = new Thickness(8, 0, 0, 0) };
                test.Click += (_, _) => _ = Webhooks.Post(w.Url, $"✅ Test message from ObeliskPanel ({w.Name})", _log);
                var del = new Button { Content = "Remove", MinWidth = 70, Margin = new Thickness(6, 0, 0, 0) };
                del.Click += (_, _) => { _s.Webhooks.Remove(w); _save(); Render(); };
                DockPanel.SetDock(name, Dock.Left); DockPanel.SetDock(del, Dock.Right); DockPanel.SetDock(test, Dock.Right);
                row.Children.Add(name); row.Children.Add(del); row.Children.Add(test); row.Children.Add(url);
                list.Children.Add(row);
            }
            var add = new Button { Content = "＋ Add Webhook", MinWidth = 130, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
            add.Click += (_, _) => { _s.Webhooks.Add(new NamedWebhook { Name = _s.Webhooks.Count == 0 ? "Main" : $"Webhook {_s.Webhooks.Count + 1}" }); _save(); Render(); };
            list.Children.Add(add);
        }
        Render();
    }

    void GameSection(GameType g)
    {
        var name = g == GameType.ASA ? "ARK: Survival Ascended (ASA)" : "ARK: Survival Evolved (ASE)";
        _sp.Children.Add(new TextBlock
        {
            Text = name, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Theme.ModeBrush(g), Margin = new Thickness(0, 14, 0, 0),
        });
        var clusterInfo = new TextBlock { Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap };
        void UpdateInfo()
        {
            var n = _s.Servers.Count(p => p.Game == g);
            clusterInfo.Text = $"Cluster data: {_s.GetClusterDir(g)}   ·   {n} {g} server(s) set up";
        }
        FolderRow("Servers Folder", _s.GetServersDir(g), v => { _s.SetServersDir(g, v); UpdateInfo(); }, null);
        _sp.Children.Add(clusterInfo);
        UpdateInfo();
        TextRow("Cluster ID", _s.GetClusterId(g), v => _s.SetClusterId(g, v),
            $"All {g} servers with \"In Cluster\" enabled and this cluster ID can transfer characters and dinos between each other.");
    }

    void Header(string t) => _sp.Children.Add(new TextBlock
    {
        Text = t, FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, _sp.Children.Count == 0 ? 0 : 22, 0, 4),
    });

    void Hint(string? t)
    {
        if (string.IsNullOrEmpty(t)) return;
        _sp.Children.Add(new TextBlock { Text = t, Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
    }

    void Label(string t) => _sp.Children.Add(new TextBlock { Text = t, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 3) });

    void Commit(Action<string> set, string v) { set(v.Trim()); _save(); }

    void TextRow(string label, string val, Action<string> set, string? hint)
    {
        Label(label);
        var tb = new TextBox { Text = val, Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
        tb.LostFocus += (_, _) => Commit(set, tb.Text);
        _sp.Children.Add(tb);
        Hint(hint);
    }

    DockPanel PathRow(string label, string val, Action<string> set, out TextBox tb)
    {
        Label(label);
        var row = new DockPanel();
        var box = new TextBox { Text = val };
        tb = box;
        box.LostFocus += (_, _) => Commit(set, box.Text);
        var browse = new Button { Content = "Browse…", MinWidth = 90, Margin = new Thickness(8, 0, 0, 0) };
        var open = new Button { Content = "Open", MinWidth = 70, Margin = new Thickness(6, 0, 0, 0) };
        open.Click += (_, _) =>
        {
            var p = box.Text.Trim();
            var dir = Directory.Exists(p) ? p : Path.GetDirectoryName(p);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) System.Diagnostics.Process.Start("explorer.exe", dir);
            else _log($"Folder does not exist yet: {p}");
        };
        DockPanel.SetDock(open, Dock.Right); DockPanel.SetDock(browse, Dock.Right);
        row.Children.Add(open); row.Children.Add(browse); row.Children.Add(box);
        _sp.Children.Add(row);
        browse.Tag = box;
        return row;
    }

    void FolderRow(string label, string val, Action<string> set, string? hint)
    {
        var row = PathRow(label, val, set, out var box);
        var browse = row.Children.OfType<Button>().First(b => (string)b.Content == "Browse…");
        browse.Click += (_, _) =>
        {
            var dlg = new OpenFolderDialog { Title = label };
            if (Directory.Exists(box.Text.Trim())) dlg.InitialDirectory = box.Text.Trim();
            if (dlg.ShowDialog(this) == true) { box.Text = dlg.FolderName; Commit(set, box.Text); }
        };
        Hint(hint);
    }

    void FileRow(string label, string val, Action<string> set, string? hint)
    {
        var row = PathRow(label, val, set, out var box);
        var browse = row.Children.OfType<Button>().First(b => (string)b.Content == "Browse…");
        browse.Click += (_, _) =>
        {
            var dlg = new OpenFileDialog { Title = label, Filter = "DLL (*.dll)|*.dll|All files|*.*" };
            if (dlg.ShowDialog(this) == true) { box.Text = dlg.FileName; Commit(set, box.Text); }
        };
        Hint(hint);
    }

    void MaskedKey(string label, string value, Action<string> set, string hint)
    {
        Label(label);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var pw = new PasswordBox { Password = value, Width = 520 };
        var plain = new TextBox { Text = value, Width = 520, Visibility = Visibility.Collapsed };
        pw.LostFocus += (_, _) => { Commit(set, pw.Password); plain.Text = pw.Password; };
        plain.LostFocus += (_, _) => { Commit(set, plain.Text); pw.Password = plain.Text; };
        var show = new Button { Content = "Show", MinWidth = 70, Margin = new Thickness(8, 0, 0, 0) };
        show.Click += (_, _) =>
        {
            bool reveal = plain.Visibility != Visibility.Visible;
            plain.Visibility = reveal ? Visibility.Visible : Visibility.Collapsed;
            pw.Visibility = reveal ? Visibility.Collapsed : Visibility.Visible;
            show.Content = reveal ? "Hide" : "Show";
        };
        row.Children.Add(pw); row.Children.Add(plain); row.Children.Add(show);
        _sp.Children.Add(row);
        Hint(hint);
    }
}
