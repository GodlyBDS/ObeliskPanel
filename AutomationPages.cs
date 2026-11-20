using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ObeliskPanel;

public static class AutomationPages
{
    static ScrollViewer Page(out StackPanel sp)
    {
        sp = new StackPanel { Margin = new Thickness(14, 10, 14, 14), MaxWidth = 980, HorizontalAlignment = HorizontalAlignment.Left };
        return new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    static TextBlock H(string t, double top = 14) => new() { Text = t, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, top, 0, 6) };
    static TextBlock Hint(string t) => new() { Text = t, Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) };

    static Button Btn(string t, Action a, double minWidth = 150)
    {
        var b = new Button { Content = t, MinWidth = minWidth, Margin = new Thickness(0, 0, 0, 6), HorizontalContentAlignment = HorizontalAlignment.Center };
        b.Click += (_, _) => a();
        return b;
    }

    static CheckBox Check(string t, bool v, Action<bool> set, Action save)
    {
        var c = new CheckBox { Content = t, IsChecked = v, MinWidth = 0, Margin = new Thickness(0, 4, 18, 4), VerticalAlignment = VerticalAlignment.Center };
        c.Click += (_, _) => { set(c.IsChecked == true); save(); };
        return c;
    }

    static StackPanel Row(params UIElement[] items)
    {
        var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
        foreach (var i in items) r.Children.Add(i);
        return r;
    }

    static TextBlock L(string t, double width = 0) => new()
    {
        Text = t, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Width = width > 0 ? width : double.NaN,
    };

    static TextBox Num(int v, Action<int> set, Action save, int min = 0, int max = 100000)
    {
        var tb = new TextBox { Text = v.ToString(), Width = 80, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 0, 8, 0) };
        tb.LostFocus += (_, _) =>
        {
            if (int.TryParse(tb.Text.Trim(), out var n)) { n = Math.Clamp(n, min, max); tb.Text = n.ToString(); set(n); save(); tb.ClearValue(Control.BorderBrushProperty); }
            else tb.BorderBrush = Theme.Bad;
        };
        return tb;
    }

    static TextBox Text(string v, Action<string> set, Action save, double width = 420)
    {
        var tb = new TextBox { Text = v, Width = width, Margin = new Thickness(0, 0, 8, 0) };
        tb.LostFocus += (_, _) => { set(tb.Text.Trim()); save(); };
        return tb;
    }

    static Border Card(UIElement child) => new()
    {
        Background = Theme.Card, BorderBrush = Theme.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
        Padding = new Thickness(12), Margin = new Thickness(0, 4, 0, 8), Child = child,
    };

    public static FrameworkElement Backups(ServerProfile p, AppSettings s, Func<bool> running, Action save, Action<string> log, Action refresh, Window owner)
    {
        var page = Page(out var sp);
        sp.Children.Add(H("Ark Save Files", 0));
        sp.Children.Add(Hint($"Backups are stored next to the save in {p.SaveDir} as {p.Map}_DD.MM.YYYY_HH.MM.SS.zip. Existing backups with this naming in that folder show up here too."));

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var list = new ListBox { Height = 260, Margin = new Thickness(0, 0, 12, 0), BorderBrush = Theme.Border, BorderThickness = new Thickness(1) };
        var files = global::ObeliskPanel.Backups.List(p);
        foreach (var b in files)
        {
            var d = new DockPanel { LastChildFill = true };
            var del = new Button { Content = "🗑", MinWidth = 0, Padding = new Thickness(6, 0, 6, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), ToolTip = "Delete this backup" };
            del.Click += (_, _) =>
            {
                if (MessageBox.Show($"Delete {b.Name}?", "Backups", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                try { File.Delete(b.Path); log($"[{p.Name}] Backup deleted: {b.Name}"); } catch (Exception ex) { log(ex.Message); }
                refresh();
            };
            DockPanel.SetDock(del, Dock.Right);
            d.Children.Add(del);
            d.Children.Add(new TextBlock
            {
                Text = $"{b.Time:dd.MM.yyyy  HH:mm:ss}      {b.Size / 1048576.0:0.0} MB      {b.Name}", VerticalAlignment = VerticalAlignment.Center,
            });
            list.Items.Add(new ListBoxItem { Content = d, Tag = b, HorizontalContentAlignment = HorizontalAlignment.Stretch });
        }
        if (files.Count == 0) list.Items.Add(new ListBoxItem { Content = "No backups yet.", IsEnabled = false });
        grid.Children.Add(list);

        BackupFile? Selected() => (list.SelectedItem as ListBoxItem)?.Tag as BackupFile;
        var side = new StackPanel();
        Grid.SetColumn(side, 1);
        side.Children.Add(Btn("Create Backup", async () =>
        {
            try
            {
                if (running()) { try { await RconClient.Send("127.0.0.1", p.RconPort, p.AdminPassword, "SaveWorld"); await Task.Delay(4000); } catch { } }
                await Task.Run(() => global::ObeliskPanel.Backups.Create(p, s, log));
            }
            catch (Exception ex) { log($"[{p.Name}] Backup failed: {ex.Message}"); }
            refresh();
        }));
        side.Children.Add(Btn("Restore Backup", () =>
        {
            var b = Selected();
            if (b == null) { MessageBox.Show("Select a backup first.", "Restore"); return; }
            if (running()) { MessageBox.Show("Stop the server before restoring a backup.", "Restore"); return; }
            if (MessageBox.Show($"Restore {b.Name}?\n\nThe current save is kept and can be brought back with 'Undo Last Restore'.", "Restore",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try { global::ObeliskPanel.Backups.Restore(p, s, b, log); } catch (Exception ex) { log($"[{p.Name}] Restore failed: {ex.Message}"); }
            refresh();
        }));
        side.Children.Add(Btn("Open Backup File", () =>
        {
            var b = Selected();
            if (b != null) Process.Start(new ProcessStartInfo(b.Path) { UseShellExecute = true });
        }));
        side.Children.Add(new Border { Height = 10 });
        side.Children.Add(Btn("Backups Folder…", () => { Directory.CreateDirectory(p.SaveDir); Process.Start("explorer.exe", p.SaveDir); }));
        var undo = Btn("Undo Last Restore", () =>
        {
            if (running()) { MessageBox.Show("Stop the server first.", "Undo"); return; }
            try { global::ObeliskPanel.Backups.UndoRestore(p, log); } catch (Exception ex) { log(ex.Message); }
            refresh();
        });
        undo.IsEnabled = global::ObeliskPanel.Backups.CanUndo(p);
        side.Children.Add(undo);
        grid.Children.Add(side);
        sp.Children.Add(grid);

        sp.Children.Add(H("Automatic Backups"));
        var auto = new StackPanel();
        auto.Children.Add(Check("Create backups automatically while the server is running", p.AutoBackupEnabled, v => p.AutoBackupEnabled = v, save));
        auto.Children.Add(Row(L("Create a backup every", 160), Num(p.BackupIntervalMinutes, v => p.BackupIntervalMinutes = v, save, 5), L("minutes")));
        auto.Children.Add(Row(L("Keep at most", 160), Num(p.MaxBackups, v => p.MaxBackups = v, save, 0), L("backups (0 = keep all, oldest are deleted first)")));
        auto.Children.Add(Check("Include the cluster folder (transferred characters, dinos and items)", p.BackupIncludeCluster, v => p.BackupIncludeCluster = v, save));
        sp.Children.Add(Card(auto));
        return page;
    }

    public static FrameworkElement Restart(ServerProfile p, Action save, Action restartNow)
    {
        var page = Page(out var sp);
        var next = new TextBlock { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) };
        void UpdateNext()
        {
            var n = MainWindow.NextRestart(p, DateTime.Now);
            next.Text = !p.RestartEnabled ? "Restart schedule disabled." : n == null ? "No restart time set." : $"Next restart: {n:dddd, dd.MM.yyyy HH:mm}";
            next.Foreground = p.RestartEnabled ? Theme.Good : Theme.Muted;
        }
        void SaveAndUpdate() { save(); UpdateNext(); }

        sp.Children.Add(Row(Check("Enabled", p.RestartEnabled, v => p.RestartEnabled = v, SaveAndUpdate),
            Btn("Restart now (with countdown)", () =>
            {
                if (MessageBox.Show($"Restart {p.Name} now?", "Restart", MessageBoxButton.YesNo) == MessageBoxResult.Yes) restartNow();
            }, 220)));
        sp.Children.Add(next);

        sp.Children.Add(H("Schedule"));
        var sched = new StackPanel();
        var days = new StackPanel { Orientation = Orientation.Horizontal };
        string[] names = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        for (int i = 0; i < 7; i++)
        {
            int d = i;
            days.Children.Add(Check(names[i], p.RestartDays[d], v => p.RestartDays[d] = v, SaveAndUpdate));
        }
        sched.Children.Add(days);
        var times = Text(string.Join(", ", p.RestartTimes), v =>
        {
            p.RestartTimes = v.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => TimeSpan.TryParse(t, CultureInfo.InvariantCulture, out _)).ToList();
        }, SaveAndUpdate, 300);
        sched.Children.Add(Row(L("Restart times (HH:mm)", 170), times, L("comma separated, e.g. 06:00, 18:00")));
        sp.Children.Add(Card(sched));

        sp.Children.Add(H("Restart Options"));
        var opt = new StackPanel();
        opt.Children.Add(Row(Check("Do countdown", p.RestartCountdown, v => p.RestartCountdown = v, SaveAndUpdate),
            L("Duration"), Num(p.CountdownMinutes, v => p.CountdownMinutes = v, SaveAndUpdate, 1, 120), L("minutes")));
        opt.Children.Add(Row(L("Countdown message", 170), Text(p.CountdownMessage, v => p.CountdownMessage = v, save, 420), L("{minutes} = minutes left")));
        opt.Children.Add(Check("Skip the countdown when nobody is online", p.SkipCountdownWhenEmpty, v => p.SkipCountdownWhenEmpty = v, save));
        opt.Children.Add(Check("Create a backup before starting again", p.BackupBeforeRestart, v => p.BackupBeforeRestart = v, save));
        opt.Children.Add(Check("Update the server files (SteamCMD) before starting again", p.UpdateOnRestart, v => p.UpdateOnRestart = v, save));
        opt.Children.Add(Row(L("RCON when countdown starts", 200), Text(p.RconOnCountdownStart, v => p.RconOnCountdownStart = v, save, 380)));
        opt.Children.Add(Row(L("RCON when server is stopping", 200), Text(p.RconOnStopping, v => p.RconOnStopping = v, save, 380)));
        opt.Children.Add(Hint("Every restart saves the world (SaveWorld) and shuts down cleanly (DoExit). If the server does not exit within 3 minutes the process is ended."));
        sp.Children.Add(Card(opt));

        sp.Children.Add(H("Server Wipe Settings"));
        var wipe = new StackPanel();
        wipe.Children.Add(Check("Wipe the server during the scheduled restart on these days", p.WipeEnabled, v => p.WipeEnabled = v, save));
        wipe.Children.Add(Row(L("Days of the month", 170), Text(p.WipeDaysOfMonth, v => p.WipeDaysOfMonth = v, save, 160), L("e.g. 1 or 1,15")));
        wipe.Children.Add(Check("Also wipe players and tribes (otherwise only the world: dinos and structures)", p.WipePlayers, v => p.WipePlayers = v, save));
        wipe.Children.Add(Hint("A backup is always created right before a wipe." + (p.LastWipeDate != "" ? $"  Last wipe: {p.LastWipeDate}" : "")));
        sp.Children.Add(Card(wipe));

        UpdateNext();
        return page;
    }

    public static FrameworkElement Discord(ServerProfile p, AppSettings s, Action save, Action<string> log, Window owner)
    {
        var page = Page(out var sp);
        sp.Children.Add(Row(Check("Webhooks enabled for this server", p.WebhooksEnabled, v => p.WebhooksEnabled = v, save),
            Btn("Manage Webhooks & Bot…", () => new SettingsWindow(s, save, log) { Owner = owner }.ShowDialog(), 200)));
        if (s.Webhooks.Count == 0)
            sp.Children.Add(Hint("No webhooks yet – add one under Settings (⚙) → Discord. In Discord: Channel settings → Integrations → Webhooks → New Webhook → Copy URL."));

        var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        int row = 0;
        void Add(UIElement e, int col)
        {
            while (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(e, row); Grid.SetColumn(e, col);
            if (e is FrameworkElement fe) fe.Margin = new Thickness(0, 3, 8, 3);
            grid.Children.Add(e);
        }
        Add(new TextBlock { Text = "Message Type", FontWeight = FontWeights.Bold }, 0);
        Add(new TextBlock { Text = "Message Text", FontWeight = FontWeights.Bold }, 1);
        Add(new TextBlock { Text = "Webhook", FontWeight = FontWeights.Bold }, 2);
        row++;
        foreach (var (key, label, def) in WebhookEvent.Kinds)
        {
            if (!p.WebhookEvents.TryGetValue(key, out var ev)) p.WebhookEvents[key] = ev = new WebhookEvent { Text = def };
            Add(Check(label, ev.Enabled, v => ev.Enabled = v, save), 0);
            if (def == "")
                Add(new TextBlock { Text = key switch { "Players" => "\"<name> joined / left\"", "Admin" => "RCON commands sent from ObeliskPanel or Discord", _ => "In-game global chat" }, Foreground = Theme.Muted, VerticalAlignment = VerticalAlignment.Center }, 1);
            else
            {
                var tb = new TextBox { Text = ev.Text };
                tb.LostFocus += (_, _) => { ev.Text = tb.Text; save(); };
                Add(tb, 1);
            }
            var combo = new ComboBox { ItemsSource = s.Webhooks.Select(w => w.Name).ToList(), SelectedItem = ev.Webhook };
            if (combo.SelectedItem == null && s.Webhooks.Count > 0) combo.SelectedIndex = 0;
            combo.SelectionChanged += (_, _) => { ev.Webhook = combo.SelectedItem as string ?? ""; save(); };
            Add(combo, 2);
            row++;
        }
        sp.Children.Add(grid);
        sp.Children.Add(Hint("Placeholders: {ip} = public address with port, {map}, {name}."));

        sp.Children.Add(H("Message Options"));
        var opt = new StackPanel();
        var a = new RadioButton { Content = "Prepend session name", IsChecked = p.WebhookPrependSessionName, Margin = new Thickness(0, 3, 0, 3) };
        var b = new RadioButton { Content = "Prepend profile name", IsChecked = !p.WebhookPrependSessionName, Margin = new Thickness(0, 3, 0, 3) };
        a.Checked += (_, _) => { p.WebhookPrependSessionName = true; save(); };
        b.Checked += (_, _) => { p.WebhookPrependSessionName = false; save(); };
        opt.Children.Add(a); opt.Children.Add(b);
        opt.Children.Add(Check("Include restart countdown messages", p.WebhookIncludeCountdown, v => p.WebhookIncludeCountdown = v, save));
        opt.Children.Add(Btn("Send test message", () => _ = Webhooks.Send(s, p, "Running", "– test message from ObeliskPanel ({ip})", log), 170));
        sp.Children.Add(Card(opt));

        sp.Children.Add(H("Discord Bot"));
        sp.Children.Add(Hint(s.BotEnabled
            ? "The bot is enabled. Slash commands: /status, /ip, /players, /start, /stop, /restart, /rcon. Start/stop/restart/rcon need Discord administrator rights or the configured admin role."
            : "Set up a Discord bot under Settings (⚙) → Discord to control your servers with slash commands and show a live status message."));
        return page;
    }

    public static FrameworkElement Messages(ServerProfile p, Action save, Action<string> sendNow)
    {
        var page = Page(out var sp);
        sp.Children.Add(Hint("Messages are broadcast in-game (ServerChat) at a fixed interval or every day at a fixed time, while the server is running."));
        sp.Children.Add(Check("Pause scheduled messages for this server", p.PauseScheduledMessages, v => p.PauseScheduledMessages = v, save));
        var list = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        void Render()
        {
            list.Children.Clear();
            foreach (var m in p.ScheduledMessages.ToList())
            {
                var box = new StackPanel();
                box.Children.Add(Row(Check("Enabled", m.Enabled, v => m.Enabled = v, save), Text(m.Text, v => m.Text = v, save, 560)));
                var every = new RadioButton { Content = "Every", IsChecked = m.Interval, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
                var daily = new RadioButton { Content = "Daily at", IsChecked = !m.Interval, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 6, 0) };
                every.Checked += (_, _) => { m.Interval = true; save(); };
                daily.Checked += (_, _) => { m.Interval = false; save(); };
                var del = Btn("Remove", () => { p.ScheduledMessages.Remove(m); save(); Render(); }, 90);
                var send = Btn("Send now", () => sendNow(m.Text), 90);
                del.Margin = send.Margin = new Thickness(8, 0, 0, 0);
                box.Children.Add(Row(every, Num(m.IntervalMinutes, v => m.IntervalMinutes = v, save, 1, 10080), L("minutes"),
                    daily, Text(m.DailyTime, v => m.DailyTime = TimeSpan.TryParse(v, CultureInfo.InvariantCulture, out _) ? v : m.DailyTime, save, 80), send, del));
                list.Children.Add(Card(box));
            }
            if (p.ScheduledMessages.Count == 0) list.Children.Add(Hint("No scheduled messages yet."));
        }
        Render();
        sp.Children.Add(list);
        sp.Children.Add(Btn("＋ Add Message", () => { p.ScheduledMessages.Add(new ScheduledMessage { Text = "Welcome! Join our Discord." }); save(); Render(); }, 160));
        return page;
    }

    public static FrameworkElement Api(ServerProfile p, Func<bool> running, Action save, Action<string> log, Action refresh)
    {
        var page = Page(out var sp);
        var name = ServerApi.Name(p.Game);
        sp.Children.Add(H(p.Game == GameType.ASA ? "AsaApi (ARK: Survival Ascended Server API)" : "ArkServerApi (ARK: Survival Evolved)", 0));
        sp.Children.Add(Hint($"A plugin framework for dedicated servers, downloaded from github.com/{ServerApi.Repo(p.Game)}. Plugins go into ShooterGame\\Binaries\\Win64\\ArkApi\\Plugins."));
        var installed = ServerApi.InstalledVersion(p);
        var info = new TextBlock { Text = $"Installed version: {installed ?? "not installed"}", Margin = new Thickness(0, 6, 0, 2) };
        var latest = new TextBlock { Text = "Latest version: checking…", Foreground = Theme.Muted };
        sp.Children.Add(info); sp.Children.Add(latest);
        _ = Task.Run(async () =>
        {
            string t;
            try { t = (await ServerApi.Latest(p.Game))?.Tag ?? "?"; } catch (Exception ex) { t = "could not check (" + ex.Message + ")"; }
            latest.Dispatcher.Invoke(() => latest.Text = "Latest version: " + t);
        });
        sp.Children.Add(Row(Btn(installed == null ? "Install" : "Update / Reinstall", async () =>
        {
            if (running()) { MessageBox.Show("Stop the server first.", name); return; }
            try { await ServerApi.Install(p, log); } catch (Exception ex) { log($"[{p.Name}] {name} install failed: {ex.Message}"); }
            refresh();
        }), new Border { Width = 8 }, Btn("Open Plugins Folder", () =>
        {
            var dir = Path.Combine(p.BinDir, "ArkApi", "Plugins");
            Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", dir);
        })));
        if (p.Game == GameType.ASA)
            sp.Children.Add(Check("Use AsaApi – start the server through AsaApiLoader.exe", p.UseServerApi, v => p.UseServerApi = v, save));
        else
            sp.Children.Add(Hint("ArkServerApi loads itself through version.dll as soon as it is installed – no extra start option needed."));
        sp.Children.Add(Hint("CPU affinity and similar options can be added under Server Settings → Main → Command Line Options."));
        return page;
    }
}
