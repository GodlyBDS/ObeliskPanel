using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ObeliskPanel;

public class ModsPanel : DockPanel
{
    readonly ServerProfile _p;
    readonly AppSettings _s;
    readonly Func<IniContext> _ctx;
    readonly Action _changed;
    readonly Action<string> _log;
    readonly Window _owner;
    readonly List<string> _ids;
    static readonly HashSet<string> _checked = new();
    readonly StackPanel _rows = new();
    readonly TextBox _search = new() { Width = 260 };
    readonly TextBlock _count = new() { Foreground = Theme.Muted, VerticalAlignment = VerticalAlignment.Center };

    bool Asa => _p.Game == GameType.ASA;
    string Source => Asa ? "CurseForge" : "Steam Workshop";

    public static string Key(GameType g, string id) => g == GameType.ASA ? id : "ase:" + id;
    string K(string id) => Key(_p.Game, id);

    public ModsPanel(ServerProfile p, AppSettings s, Func<IniContext> ctx, Action changed, Action<string> log, Window owner)
    {
        _p = p; _s = s; _ctx = ctx; _changed = changed; _log = log; _owner = owner;
        _ids = p.Mods.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        Margin = new Thickness(6);

        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var srcLabel = new TextBlock
        {
            Text = Asa ? "ASA mods from CurseForge" : "ASE mods from the Steam Workshop",
            Foreground = Theme.ModeBrush(_p.Game), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center,
        };
        SetDock(srcLabel, Dock.Right);
        top.Children.Add(srcLabel);
        var searchRow = new StackPanel { Orientation = Orientation.Horizontal };
        searchRow.Children.Add(new TextBlock { Text = "Search  ", VerticalAlignment = VerticalAlignment.Center });
        _search.TextChanged += (_, _) => Render();
        searchRow.Children.Add(_search);
        top.Children.Add(searchRow);
        SetDock(top, Dock.Top); Children.Add(top);

        var bottom = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        Btn(bottom, Asa ? "Browse Mods (CurseForge)" : "Browse Mods (Steam Workshop)", OpenBrowser);
        var addBox = new TextBox { Width = 130, VerticalAlignment = VerticalAlignment.Center, ToolTip = Asa ? "CurseForge Project ID" : "Steam Workshop ID (the number after ?id= in the link)" };
        bottom.Children.Add(new TextBlock { Text = Asa ? "   Mod ID: " : "   Workshop ID: ", VerticalAlignment = VerticalAlignment.Center });
        bottom.Children.Add(addBox);
        Btn(bottom, "Add", () =>
        {
            var id = ExtractId(addBox.Text);
            if (id == null) { _log("Mod ID must be a number (or a Workshop link)."); return; }
            if (!_ids.Contains(id)) { _ids.Add(id); Commit(); _ = LoadNames(); }
            addBox.Clear();
        });
        Btn(bottom, "Load Names", () => _ = LoadNames(force: true));
        Btn(bottom, "Remove All", () =>
        {
            if (MessageBox.Show("Remove all mods from this server?", "Mods", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            _ids.Clear(); Commit();
        });
        bottom.Children.Add(_count);
        SetDock(bottom, Dock.Bottom); Children.Add(bottom);

        Children.Add(new Border
        {
            BorderBrush = Theme.Border, BorderThickness = new Thickness(1),
            Child = new ScrollViewer { Content = _rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
        });

        Render();
        if (_ids.Any(i => !_s.ModNames.ContainsKey(K(i)) || (Asa && !_checked.Contains(i)))) _ = LoadNames();
    }

    static string? ExtractId(string text)
    {
        var t = text.Trim();
        var m = System.Text.RegularExpressions.Regex.Match(t, @"[?&]id=(\d+)");
        if (m.Success) return m.Groups[1].Value;
        return long.TryParse(t, out _) ? t : null;
    }

    static void Btn(Panel parent, string text, Action a)
    {
        var b = new Button { Content = text, Margin = new Thickness(0, 2, 6, 2) };
        b.Click += (_, _) => a();
        parent.Children.Add(b);
    }

    void Commit()
    {
        _p.Mods = string.Join(",", _ids);
        _changed();
        Render();
    }

    string NameOf(string id) => _s.ModNames.TryGetValue(K(id), out var n) ? n : "(unknown name)";

    void OpenSettings(string id, string name) =>
        new ModSettingsWindow(id, name, _p.Game, _ctx(), _s, () => { _changed(); Render(); }, _log) { Owner = _owner }.ShowDialog();

    void Render()
    {
        _rows.Children.Clear();
        var q = _search.Text.Trim();
        for (int i = 0; i < _ids.Count; i++)
        {
            var id = _ids[i];
            var name = NameOf(id);
            if (q != "" && !id.Contains(q) && !name.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
            int idx = i;

            var g = new Grid { Margin = new Thickness(4, 1, 4, 1), Background = i % 2 == 0 ? Brushes.Transparent : Theme.RowAlt };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Asa ? 90 : 120) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var idT = new TextBlock { Text = id, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
            var hasSettings = _s.ModSections.TryGetValue(K(id), out var secs) && secs.Count > 0;
            var cosmetic = Asa && _s.CosmeticMods.Contains(id);
            var nameT = new TextBlock { Text = name + (hasSettings ? "   ⚙" : "") + (cosmetic ? "   ⚠ Custom Cosmetic – not loaded by the server" : ""), VerticalAlignment = VerticalAlignment.Center };
            if (cosmetic) { nameT.Foreground = Theme.Warn; nameT.ToolTip = "Cosmetics only run in the game client. The server would fail its mod install, so ObeliskPanel leaves this mod out of -mods. Remove it with 🗑."; }
            Grid.SetColumn(nameT, 1);
            var btns = new StackPanel { Orientation = Orientation.Horizontal };
            Grid.SetColumn(btns, 2);
            Btn(btns, "Settings", () => OpenSettings(id, name));
            if (!Asa) SmallBtn(btns, "↗", () => Process.Start(new ProcessStartInfo(SteamWorkshop.ItemUrl(id)) { UseShellExecute = true }));
            SmallBtn(btns, "▲", () => { if (idx > 0) { (_ids[idx - 1], _ids[idx]) = (_ids[idx], _ids[idx - 1]); Commit(); } });
            SmallBtn(btns, "▼", () => { if (idx < _ids.Count - 1) { (_ids[idx + 1], _ids[idx]) = (_ids[idx], _ids[idx + 1]); Commit(); } });
            SmallBtn(btns, "🗑", () => { _ids.RemoveAt(idx); Commit(); });
            g.Children.Add(idT); g.Children.Add(nameT); g.Children.Add(btns);
            g.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) OpenSettings(id, name); };
            _rows.Children.Add(g);
        }
        _count.Text = $"   Mods: {_ids.Count}   (order = load order)";
    }

    static bool IsCosmetic(CfMod m) =>
        m.Categories.Split(',', StringSplitOptions.TrimEntries).Any(c => c.Equals("Custom Cosmetics", StringComparison.OrdinalIgnoreCase));

    static void SmallBtn(Panel parent, string text, Action a)
    {
        var b = new Button { Content = text, MinWidth = 28, Padding = new Thickness(4, 1, 4, 1), Margin = new Thickness(0, 2, 3, 2) };
        b.Click += (_, _) => a();
        parent.Children.Add(b);
    }

    async Task LoadNames(bool force = false)
    {
        var ids = _ids.Where(i => force || !_s.ModNames.ContainsKey(K(i)) || (Asa && !_checked.Contains(i))).ToList();
        if (ids.Count == 0) return;
        try
        {
            if (Asa)
            {
                if (string.IsNullOrWhiteSpace(_s.CurseForgeApiKey))
                {
                    if (force) _log("ASA mod names need a CurseForge API key (Server Settings → Cluster & App).");
                    return;
                }
                using var cf = new CurseForge(_s.CurseForgeApiKey);
                foreach (var m in await cf.GetMods(ids.Select(int.Parse)))
                {
                    var id = m.Id.ToString();
                    _checked.Add(id);
                    _s.ModNames[K(id)] = m.Name;
                    if (IsCosmetic(m))
                    {
                        if (_s.CosmeticMods.Add(id))
                            _log($"'{m.Name}' ({id}) is a Custom Cosmetic – those only work in the client and make the server's mod install fail. It will be left out of the server start.");
                    }
                    else _s.CosmeticMods.Remove(id);
                }
            }
            else
            {
                foreach (var w in await SteamWorkshop.GetDetails(ids)) _s.ModNames[K(w.Id)] = w.Title;
            }
            _changed();
            Render();
        }
        catch (Exception ex) { _log($"Loading mod names from {Source} failed: " + ex.Message); }
    }

    void OpenBrowser()
    {
        if (!Asa)
        {
            new WorkshopBrowserWindow(_s.SteamApiKey, _ids, w =>
            {
                if (_ids.Contains(w.Id)) return false;
                _ids.Add(w.Id);
                _s.ModNames[K(w.Id)] = w.Title;
                Commit();
                _log($"Mod added: {w.Title} ({w.Id})");
                return true;
            }) { Owner = _owner }.ShowDialog();
            return;
        }
        if (string.IsNullOrWhiteSpace(_s.CurseForgeApiKey))
        {
            MessageBox.Show("Please enter a CurseForge API key first:\nServer Settings → Cluster & App.\n\nYou can get one for free at console.curseforge.com.", "Mod Browser");
            return;
        }
        new ModBrowserWindow(_s.CurseForgeApiKey, _ids, m =>
        {
            var id = m.Id.ToString();
            if (_ids.Contains(id)) return false;
            if (IsCosmetic(m))
            {
                MessageBox.Show($"'{m.Name}' is a Custom Cosmetic.\n\nCosmetics only work in the game client. On a server they make the mod install fail and the server shuts down at start, so it can't be added here.",
                                "Mod Browser", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            _ids.Add(id);
            _s.ModNames[K(id)] = m.Name;
            Commit();
            _log($"Mod added: {m.Name} ({id})");
            return true;
        }) { Owner = _owner }.ShowDialog();
    }
}
