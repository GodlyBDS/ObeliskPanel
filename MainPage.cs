using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ObeliskPanel;

public static class MainPage
{
    public static readonly string[] AsaMaps = { "TheIsland_WP", "TheCenter_WP", "ScorchedEarth_WP", "Ragnarok_WP", "Aberration_WP", "Extinction_WP", "Valguero_WP", "Genesis_WP", "Astraeos_WP", "LostColony_WP", "BobsMissions_WP" };
    public static readonly string[] AseMaps = { "TheIsland", "TheCenter", "ScorchedEarth_P", "Ragnarok", "Aberration_P", "Extinction", "Valguero_P", "Genesis", "CrystalIsles", "Gen2", "LostIsland", "Fjordur", "Aquatica", "PGARK" };

    const string SS = SettingDefs.SS;

    static string? DetectMapName(ServerProfile p, AppSettings s)
    {
        try
        {
            var folders = ModFileScanner.FindModFolders(s.Servers, p.Game, p.MapModId.Trim());
            return folders.Count == 0 ? null : ModFileScanner.DetectMapName(folders[0], p.Game, s.OodleDllPath);
        }
        catch { return null; }
    }

    public static FrameworkElement Build(ServerProfile p, IniContext c, AppSettings s, Action changed, Action<string> log, Window owner)
    {
        var g = new Grid { Margin = new Thickness(8) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        int row = 0;
        void Put(UIElement e, int col, int span = 1)
        {
            while (g.RowDefinitions.Count <= row) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(e, row); Grid.SetColumn(e, col); Grid.SetColumnSpan(e, span); g.Children.Add(e);
        }
        void Label(string t, int col) => Put(new TextBlock { Text = t, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 8, 2) }, col);
        void Text(int col, string label, string val, Action<string> set, int span = 1)
        {
            Label(label, col);
            var tb = new TextBox { Text = val, Margin = new Thickness(0, 2, 12, 2) };
            tb.LostFocus += (_, _) => { set(tb.Text.Trim()); changed(); };
            Put(tb, col + 1, span);
        }
        void Int(int col, string label, int val, Action<int> set) =>
            Text(col, label, val.ToString(), v => { if (int.TryParse(v, out var i)) set(i); });
        void GusSet(string sec, string key, string v) { c.Gus.Set(sec, key, v); c.Changed(c.Gus); }

        Text(0, "Profile Name", p.Name, v => p.Name = v);
        Label("Game", 2);
        Put(new TextBlock
        {
            Text = p.Game == GameType.ASA ? "ARK: Survival Ascended" : "ARK: Survival Evolved",
            FontWeight = FontWeights.SemiBold, Foreground = Theme.ModeBrush(p.Game), VerticalAlignment = VerticalAlignment.Center,
        }, 3);
        row++;
        Text(0, "Install Folder", p.InstallDir, v => p.InstallDir = v, 3); row++;

        Label("Map", 0);
        var map = new ComboBox { IsEditable = true, ItemsSource = p.Game == GameType.ASA ? AsaMaps : AseMaps, Text = p.Map, Margin = new Thickness(0, 2, 12, 2) };
        map.LostFocus += (_, _) => { if (map.Text.Trim() != p.Map) { p.Map = map.Text.Trim(); changed(); } };
        map.SelectionChanged += (_, _) => { if (map.SelectedItem is string m) { p.Map = m; changed(); } };
        Put(map, 1);
        Int(2, "Max Players", p.MaxPlayers, v =>
        {
            p.MaxPlayers = v;
            if (p.Game == GameType.ASE) GusSet("/Script/Engine.GameSession", "MaxPlayers", v.ToString());
        });
        row++;

        Label("Mod Map", 0);
        var mapRow = new DockPanel { Margin = new Thickness(0, 2, 12, 2) };
        var mapId = new TextBox
        {
            Text = p.MapModId, Width = 130, VerticalAlignment = VerticalAlignment.Center,
            ToolTip = p.Game == GameType.ASA ? "CurseForge Project ID of the map mod" : "Steam Workshop ID of the map mod",
        };
        var mapInfo = new TextBlock { Foreground = Theme.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        void ShowMapInfo() => mapInfo.Text = string.IsNullOrWhiteSpace(p.MapModId)
            ? "empty = official map"
            : (s.ModNames.TryGetValue(ModsPanel.Key(p.Game, p.MapModId), out var n) ? n : "map mod " + p.MapModId) + " – loaded first";
        void SetMapMod(string id, string? title)
        {
            p.MapModId = id.Trim();
            mapId.Text = p.MapModId;
            if (title != null && p.MapModId != "") s.ModNames[ModsPanel.Key(p.Game, p.MapModId)] = title;
            if (p.MapModId != "")
            {
                var name = DetectMapName(p, s);
                if (name != null) { p.Map = name; map.Text = name; log($"[{p.Name}] Map name detected from the mod files: {name}"); }
                else log($"[{p.Name}] Map mod set. Start the server once (it downloads the mod), then click 'Detect' – or type the map's level name from its mod page into 'Map'.");
            }
            ShowMapInfo();
            changed();
        }
        mapId.LostFocus += (_, _) => { if (mapId.Text.Trim() != p.MapModId) SetMapMod(mapId.Text, null); };
        var browse = new Button { Content = "Browse Maps…", MinWidth = 0, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(6, 0, 0, 0) };
        browse.Click += (_, _) =>
        {
            var current = new List<string> { p.MapModId };
            if (p.Game == GameType.ASA)
            {
                if (string.IsNullOrWhiteSpace(s.CurseForgeApiKey)) { MessageBox.Show("Please enter a CurseForge API key first (Server Settings → Cluster & App).", "Map Browser"); return; }
                new ModBrowserWindow(s.CurseForgeApiKey, current, m => { SetMapMod(m.Id.ToString(), m.Name); return true; }, mapsOnly: true) { Owner = owner }.ShowDialog();
            }
            else
                new WorkshopBrowserWindow(s.SteamApiKey, current, w => { SetMapMod(w.Id, w.Title); return true; }, mapsOnly: true) { Owner = owner }.ShowDialog();
        };
        var detect = new Button { Content = "Detect", MinWidth = 0, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(6, 0, 0, 0), ToolTip = "Read the map's level name from the installed mod files" };
        detect.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(p.MapModId)) return;
            var name = DetectMapName(p, s);
            if (name == null) { log($"[{p.Name}] Map name not found – is the map mod installed yet? (start the server once)"); return; }
            p.Map = name; map.Text = name; changed();
            log($"[{p.Name}] Map name detected: {name}");
        };
        var clear = new Button { Content = "✕", MinWidth = 0, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(6, 0, 0, 0), ToolTip = "Back to an official map" };
        clear.Click += (_, _) => { SetMapMod("", null); };
        DockPanel.SetDock(mapId, Dock.Left); DockPanel.SetDock(browse, Dock.Right); DockPanel.SetDock(detect, Dock.Right); DockPanel.SetDock(clear, Dock.Right);
        mapRow.Children.Add(mapId); mapRow.Children.Add(clear); mapRow.Children.Add(browse); mapRow.Children.Add(detect); mapRow.Children.Add(mapInfo);
        ShowMapInfo();
        Put(mapRow, 1, 3); row++;

        Text(0, "Session Name", p.SessionName, v => { p.SessionName = v; GusSet("SessionSettings", "SessionName", v); }, 3); row++;
        Int(0, "Server Port", p.Port, v => p.Port = v);
        Text(2, "Server Password", p.ServerPassword, v =>
        {
            p.ServerPassword = v;
            if (v == "") { c.Gus.Remove(SS, "ServerPassword"); c.Changed(c.Gus); } else GusSet(SS, "ServerPassword", v);
        });
        row++;
        Int(0, "Rcon Port", p.RconPort, v => { p.RconPort = v; GusSet(SS, "RCONPort", v.ToString()); });
        Text(2, "Admin Password", p.AdminPassword, v => { p.AdminPassword = v; GusSet(SS, "ServerAdminPassword", v); });
        row++;
        if (p.Game == GameType.ASE) { Int(0, "Query Port", p.QueryPort, v => p.QueryPort = v); row++; }

        var cl = new GroupBox { Header = "Command Line Options", Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(6) };
        var clp = new StackPanel();
        var clBox = new TextBox { Text = p.ExtraArgs, ToolTip = "Additional launch options, comma separated" };
        clBox.LostFocus += (_, _) => { p.ExtraArgs = clBox.Text.Trim(); changed(); };
        clp.Children.Add(new TextBlock { Text = "Command Line (comma separated, e.g. -ForceAllowCaveFlyers,-NoHangDetection)", Foreground = Theme.Muted });
        clp.Children.Add(clBox);
        var opts = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        CheckBox Check(string t, bool v, Action<bool> set)
        {
            var cb = new CheckBox { Content = t, IsChecked = v, Margin = new Thickness(0, 3, 22, 3) };
            cb.Click += (_, _) => { set(cb.IsChecked == true); changed(); };
            opts.Children.Add(cb);
            return cb;
        }
        Check("Auto Add Firewall Rule", p.AutoFirewallRule, v => p.AutoFirewallRule = v);
        Check("Use BattlEye", p.UseBattlEye, v => p.UseBattlEye = v);
        Check("Destroy Wild Dinos (next start)", p.DestroyWildDinosOnNextStart, v => p.DestroyWildDinosOnNextStart = v);
        Check("Enable Exclusive Join", p.ExclusiveJoin, v => p.ExclusiveJoin = v);
        Check("Flyers Ridable In Caves", p.ForceAllowCaveFlyers, v => p.ForceAllowCaveFlyers = v);
        if (p.Game == GameType.ASA) Check("No Game Analytics", p.NoGameAnalytics, v => p.NoGameAnalytics = v);
        Check("No Transfer From Filtering", p.NoTransferFromFiltering, v => p.NoTransferFromFiltering = v);
        if (p.Game == GameType.ASA) Check("No Wild Babies", p.NoWildBabies, v => p.NoWildBabies = v);
        Check("In Cluster (allow travel)", p.ClusterEnabled, v => p.ClusterEnabled = v);
        var (pve, _) = SettingsUi.Field(c, IniKind.GUS, SS, "ServerPVE", SType.Bool, "False");
        ((CheckBox)pve).Content = "PVE Enabled";
        pve.Margin = new Thickness(0, 3, 22, 3);
        opts.Children.Add(pve);
        clp.Children.Add(opts);
        var wl = new Button { Content = "Edit Whitelist", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0) };
        wl.Click += (_, _) => new TextFileWindow(p.WhitelistPath, "Whitelist (one EOS ID per line)") { Owner = owner }.ShowDialog();
        clp.Children.Add(wl);
        cl.Content = clp;
        Put(cl, 0, 4); row++;

        var bottom = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        bottom.ColumnDefinitions.Add(new ColumnDefinition());
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        var plat = new StackPanel();
        CheckBox PCheck(string t, bool v, Action<bool> set)
        {
            var cb = new CheckBox { Content = t, IsChecked = v, Margin = new Thickness(0, 3, 0, 3) };
            cb.Click += (_, _) => { set(cb.IsChecked == true); changed(); };
            plat.Children.Add(cb); return cb;
        }
        PCheck("PC", p.PlatformPC, v => p.PlatformPC = v);
        PCheck("PS5", p.PlatformPS5, v => p.PlatformPS5 = v);
        PCheck("Xbox", p.PlatformXbox, v => p.PlatformXbox = v);
        PCheck("Microsoft Store", p.PlatformMS, v => p.PlatformMS = v);
        var platBox = new GroupBox { Header = "Platforms (Crossplay)", Content = plat, Padding = new Thickness(6), Margin = new Thickness(0, 0, 10, 0) };
        Grid.SetColumn(platBox, 0);
        if (p.Game == GameType.ASA) bottom.Children.Add(platBox);
        else bottom.ColumnDefinitions[0].Width = new GridLength(0);

        var misc = new StackPanel();
        var ex = new CheckBox { Content = "Exclude From Multi-Server Commands (Start All/Stop All)", IsChecked = p.ExcludeFromMulti, Margin = new Thickness(0, 3, 0, 8) };
        ex.Click += (_, _) => { p.ExcludeFromMulti = ex.IsChecked == true; changed(); };
        misc.Children.Add(ex);
        misc.Children.Add(new TextBlock { Text = "MOTD (message on join)" });
        var (motd, _) = SettingsUi.Field(c, IniKind.GUS, "MessageOfTheDay", "Message", SType.Text, null, 420);
        motd.HorizontalAlignment = HorizontalAlignment.Left;
        misc.Children.Add(motd);
        var durRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        durRow.Children.Add(new TextBlock { Text = "Duration (sec.)  ", VerticalAlignment = VerticalAlignment.Center });
        var (dur, durX) = SettingsUi.Field(c, IniKind.GUS, "MessageOfTheDay", "Duration", SType.Int, "20", 60);
        durRow.Children.Add(dur); durRow.Children.Add(durX);
        misc.Children.Add(durRow);
        var miscBox = new GroupBox { Header = "Miscellaneous", Content = misc, Padding = new Thickness(6) };
        Grid.SetColumn(miscBox, 1); bottom.Children.Add(miscBox);
        Put(bottom, 0, 4);

        return new ScrollViewer { Content = g, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
