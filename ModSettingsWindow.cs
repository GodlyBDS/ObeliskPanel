using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ObeliskPanel;

public class ModSettingsWindow : Window
{
    readonly string _id;
    readonly GameType _game;
    readonly IniContext _c;
    readonly AppSettings _s;
    readonly Action _changed;
    readonly StackPanel _body = new() { Margin = new Thickness(10) };
    readonly TextBlock _status = new() { Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10, 10, 10, 0) };
    readonly Dictionary<string, List<(string key, string def)>> _suggest = new(StringComparer.OrdinalIgnoreCase);

    static readonly Regex SecRx = new(@"^\[([^\]\[]{2,80})\]$");
    static readonly Regex KvRx = new(@"^([A-Za-z_][\w\.\[\]\(\)]*)\s*=\s*(.*)$");

    public ModSettingsWindow(string id, string name, GameType game, IniContext c, AppSettings s, Action changed, Action<string> log)
    {
        _id = id; _game = game; _c = c; _s = s; _changed = changed;
        Title = $"{game} Mod Settings – {name} ({id})"; Width = 860; Height = 680;

        var dock = new DockPanel();
        DockPanel.SetDock(_status, Dock.Top); dock.Children.Add(_status);
        var add = AddSectionBar();
        DockPanel.SetDock(add, Dock.Bottom); dock.Children.Add(add);
        dock.Children.Add(new ScrollViewer { Content = _body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = dock;

        Loaded += async (_, _) =>
        {
            var desc = await LoadSuggestions();
            var files = await ScanModFiles();
            _status.Text = string.Join("\n", new[] { desc, files }.Where(x => x != ""));
            Render();
        };
    }

    List<string> Map
    {
        get
        {
            var key = ModsPanel.Key(_game, _id);
            if (!_s.ModSections.TryGetValue(key, out var l)) _s.ModSections[key] = l = new();
            return l;
        }
    }

    static (IniKind kind, string sec) Split(string entry)
    {
        var i = entry.IndexOf('|');
        return (entry[..i] == "Game" ? IniKind.Game : IniKind.GUS, entry[(i + 1)..]);
    }

    static bool IsVanilla(string sec) =>
        sec.StartsWith("/script/", StringComparison.OrdinalIgnoreCase) ||
        sec.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase) ||
        new[] { "ServerSettings", "SessionSettings", "MessageOfTheDay", "ScalabilityGroups", "Startup", "Core.System" }
            .Contains(sec, StringComparer.OrdinalIgnoreCase);

    async Task<string> LoadSuggestions()
    {
        var source = _game == GameType.ASA ? "CurseForge" : "the Steam Workshop";
        if (_game == GameType.ASA && string.IsNullOrWhiteSpace(_s.CurseForgeApiKey))
            return "Description: no CurseForge API key.";
        _status.Text = $"Reading the mod description from {source}...";
        try
        {
            string text;
            if (_game == GameType.ASA)
            {
                using var cf = new CurseForge(_s.CurseForgeApiKey);
                text = await cf.Description(int.Parse(_id));
            }
            else
            {
                var item = (await SteamWorkshop.GetDetails(new[] { _id })).FirstOrDefault();
                text = StripBbCode(item?.Description ?? "");
            }
            var files = Parse(text);
            foreach (var (sec, kind) in files)
            {
                if (!Map.Any(e => Split(e).sec.Equals(sec, StringComparison.OrdinalIgnoreCase)))
                    Map.Add($"{(kind == IniKind.Game ? "Game" : "GUS")}|{sec}");
            }
            _changed();
            var n = _suggest.Sum(x => x.Value.Count);
            return n == 0 ? "Description: no INI settings mentioned." : $"Description: {n} settings with defaults found.";
        }
        catch (Exception ex) { return "Description could not be loaded: " + ex.Message; }
    }

    async Task<string> ScanModFiles()
    {
        var folders = ModFileScanner.FindModFolders(_s.Servers, _game, _id);
        if (folders.Count == 0) return "Mod files: not installed yet (start the server once so it downloads the mod).";
        _status.Text = "Scanning the mod files...";
        try
        {
            var found = await Task.Run(() => ModFileScanner.Scan(folders[0], _game, _s.OodleDllPath));
            int added = 0;
            foreach (var (sec, keys) in found)
            {
                if (!_suggest.TryGetValue(sec, out var list)) _suggest[sec] = list = new();
                foreach (var k in keys)
                    if (!list.Any(x => x.key.Equals(k, StringComparison.OrdinalIgnoreCase))) { list.Add((k, null!)); added++; }
                if (!Map.Any(e => Split(e).sec.Equals(sec, StringComparison.OrdinalIgnoreCase))) Map.Add($"GUS|{sec}");
            }
            _changed();
            var total = found.Sum(x => x.Value.Count);
            return total == 0
                ? "Mod files: this mod reads no INI settings (or uses a pattern the scanner doesn't recognise)."
                : $"Mod files: {total} settings found in {found.Count} section(s){(added < total ? $", {added} new" : "")}. Values without a default are left empty – the mod then uses its own default.";
        }
        catch (Exception ex) { return "Mod files could not be scanned: " + ex.Message; }
    }

    static string StripBbCode(string s) =>
        Regex.Replace(s, @"\[/?(b|i|u|s|strike|h1|h2|h3|code|noparse|quote|spoiler|list|olist|\*|hr|img|table|tr|td|th|url(=[^\]]*)?|previewyoutube[^\]]*)\]",
                      m => m.Value.Contains("code") || m.Value.Contains("list") || m.Value.Contains("h") || m.Value == "[*]" ? "\n" : "",
                      RegexOptions.IgnoreCase);

    List<(string sec, IniKind kind)> Parse(string html)
    {
        var text = Regex.Replace(html, @"<(br|/p|/div|/li|/h\d|/pre|/tr)[^>]*>", "\n", RegexOptions.IgnoreCase);
        text = WebUtility.HtmlDecode(Regex.Replace(text, "<[^>]+>", ""));
        var lines = text.Split('\n').Select(l => l.Trim()).ToArray();
        var found = new List<(string, IniKind)>();
        string? sec = null;
        for (int i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            var m = SecRx.Match(l);
            if (m.Success)
            {
                sec = m.Groups[1].Value.Trim();
                if (IsVanilla(sec)) { sec = null; continue; }
                var before = string.Join(" ", lines[Math.Max(0, i - 4)..i]);
                var kind = before.Contains("Game.ini", StringComparison.OrdinalIgnoreCase) &&
                           !before.Contains("GameUserSettings", StringComparison.OrdinalIgnoreCase) ? IniKind.Game : IniKind.GUS;
                if (!_suggest.ContainsKey(sec)) { _suggest[sec] = new(); found.Add((sec, kind)); }
                continue;
            }
            if (sec == null) continue;
            var kv = KvRx.Match(l);
            if (kv.Success)
            {
                var val = Regex.Split(kv.Groups[2].Value, @"\s+(//|;|#)")[0].Trim();
                if (!_suggest[sec].Any(x => x.key.Equals(kv.Groups[1].Value, StringComparison.OrdinalIgnoreCase)))
                    _suggest[sec].Add((kv.Groups[1].Value, val));
            }
        }
        foreach (var e in _suggest.Where(x => x.Value.Count == 0).Select(x => x.Key).ToList())
        {
            _suggest.Remove(e);
            found.RemoveAll(f => f.Item1.Equals(e, StringComparison.OrdinalIgnoreCase));
        }
        return found;
    }

    static SType Guess(string? def) =>
        def != null && (def.Equals("True", StringComparison.OrdinalIgnoreCase) || def.Equals("False", StringComparison.OrdinalIgnoreCase))
            ? SType.Bool : SType.Text;

    void Render()
    {
        _body.Children.Clear();
        if (Map.Count == 0)
        {
            _body.Children.Add(new TextBlock { Text = "No INI sections are assigned to this mod yet.", Foreground = Theme.Muted });
            return;
        }
        foreach (var entry in Map.ToList())
        {
            var (kind, sec) = Split(entry);
            var ini = _c.Get(kind);

            var head = new DockPanel { Margin = new Thickness(0, 12, 0, 4) };
            var rm = new Button { Content = "Unassign", MinWidth = 0, Padding = new Thickness(6, 1, 6, 1) };
            rm.Click += (_, _) => { Map.Remove(entry); _changed(); Render(); };
            DockPanel.SetDock(rm, Dock.Right);
            head.Children.Add(rm);
            head.Children.Add(new TextBlock
            {
                Text = $"[{sec}]   →   {(kind == IniKind.Game ? "Game.ini" : "GameUserSettings.ini")}",
                FontWeight = FontWeights.Bold, FontSize = 14, VerticalAlignment = VerticalAlignment.Center,
            });
            _body.Children.Add(head);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int row = 0;
            void Put(UIElement e, int col)
            {
                while (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(e, row); Grid.SetColumn(e, col); grid.Children.Add(e);
            }

            var sugg = _suggest.TryGetValue(sec, out var sl) ? sl : new();
            var keys = sugg.Select(x => x.key)
                .Concat(ini.Entries(sec).Select(x => x.key))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var key in keys)
            {
                var def = sugg.FirstOrDefault(x => x.key.Equals(key, StringComparison.OrdinalIgnoreCase)).def;
                Put(new TextBlock { Text = key, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 8, 2), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = key }, 0);
                var current = ini.Get(sec, key);
                var t = def == null && current == null ? ModFileScanner.GuessType(key) : Guess(def ?? current);
                var (ctrl, x) = SettingsUi.Field(_c, kind, sec, key, t, def, 250);
                ctrl.HorizontalAlignment = HorizontalAlignment.Left;
                Put(ctrl, 1); Put(x, 2);
                Put(new TextBlock { Text = def != null ? $"Default: {def}" : sugg.Any(x => x.key.Equals(key, StringComparison.OrdinalIgnoreCase)) ? "found in mod files" : "custom entry", Foreground = Theme.Muted, VerticalAlignment = VerticalAlignment.Center }, 3);
                row++;
            }

            var newKey = new TextBox { Width = 280, Margin = new Thickness(0, 6, 8, 0), ToolTip = "New key" };
            var newVal = new TextBox { Width = 250, Margin = new Thickness(0, 6, 0, 0), ToolTip = "Value" };
            var addBtn = new Button { Content = "+ Entry", MinWidth = 0, Margin = new Thickness(6, 6, 0, 0) };
            addBtn.Click += (_, _) =>
            {
                var k = newKey.Text.Trim();
                if (k == "") return;
                ini.Set(sec, k, newVal.Text.Trim());
                _c.Changed(ini);
                Render();
            };
            Put(newKey, 0); Put(newVal, 1); Put(addBtn, 2);
            _body.Children.Add(grid);
        }
    }

    FrameworkElement AddSectionBar()
    {
        var bar = new WrapPanel { Margin = new Thickness(10) };
        var secBox = new ComboBox { IsEditable = true, Width = 300, ToolTip = "Section name, e.g. from the mod description" };
        var fileBox = new ComboBox { Width = 170, ItemsSource = new[] { "GameUserSettings.ini", "Game.ini" }, SelectedIndex = 0, Margin = new Thickness(6, 0, 6, 0) };
        var assigned = _s.ModSections.Values.SelectMany(v => v).Select(e => Split(e).sec).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var sec in _c.Gus.Sections().Concat(_c.Game.Sections()).Where(x => !IsVanilla(x) && !assigned.Contains(x)).Distinct())
            secBox.Items.Add(sec);
        secBox.SelectionChanged += (_, _) =>
        {
            if (secBox.SelectedItem is string s)
                fileBox.SelectedIndex = _c.Game.Sections().Contains(s, StringComparer.OrdinalIgnoreCase) &&
                                        !_c.Gus.Sections().Contains(s, StringComparer.OrdinalIgnoreCase) ? 1 : 0;
        };
        var btn = new Button { Content = "Assign Section" };
        btn.Click += (_, _) =>
        {
            var sec = secBox.Text.Trim().Trim('[', ']');
            if (sec == "") return;
            var entry = $"{(fileBox.SelectedIndex == 1 ? "Game" : "GUS")}|{sec}";
            if (!Map.Contains(entry)) Map.Add(entry);
            _changed(); Render();
        };
        bar.Children.Add(new TextBlock { Text = "INI section: ", VerticalAlignment = VerticalAlignment.Center });
        bar.Children.Add(secBox); bar.Children.Add(fileBox); bar.Children.Add(btn);
        return bar;
    }
}
