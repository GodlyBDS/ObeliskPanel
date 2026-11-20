using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ObeliskPanel;

public class ModBrowserWindow : Window
{
    readonly CurseForge _cf;
    readonly ICollection<string> _installed;
    readonly Func<CfMod, bool> _onAdd;
    readonly ListBox _cats = new() { Width = 200 };
    readonly ComboBox _sort = new() { Width = 170 };
    readonly ComboBox _size = new() { Width = 80, ItemsSource = new[] { 10, 20, 50 }, SelectedIndex = 1 };
    readonly TextBox _search = new() { Width = 320 };
    readonly StackPanel _results = new();
    readonly TextBlock _info = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly Button _prev = new() { Content = "◀ Previous" };
    readonly Button _next = new() { Content = "Next ▶" };
    readonly ScrollViewer _scroll;
    int _index;
    bool _ready;

    readonly bool _mapsOnly;

    public ModBrowserWindow(string apiKey, ICollection<string> installed, Func<CfMod, bool> onAdd, bool mapsOnly = false)
    {
        _cf = new CurseForge(apiKey); _installed = installed; _onAdd = onAdd; _mapsOnly = mapsOnly;
        Title = mapsOnly ? "ASA Map Browser (CurseForge)" : "Mod Browser (CurseForge)"; Width = 1050; Height = 760;
        Closed += (_, _) => _cf.Dispose();

        foreach (var (label, field) in new[] { ("Popularity", 2), ("Last Updated", 3), ("Total Downloads", 6), ("Name", 4), ("Featured", 1) })
            _sort.Items.Add(new ComboBoxItem { Content = label, Tag = field });
        _sort.SelectedIndex = 0;

        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        top.Children.Add(new TextBlock { Text = "Sort by ", VerticalAlignment = VerticalAlignment.Center });
        top.Children.Add(_sort);
        top.Children.Add(new TextBlock { Text = "   Per page ", VerticalAlignment = VerticalAlignment.Center });
        top.Children.Add(_size);
        top.Children.Add(new TextBlock { Text = "   Search ", VerticalAlignment = VerticalAlignment.Center });
        top.Children.Add(_search);
        var go = new Button { Content = "Search", Margin = new Thickness(6, 0, 0, 0) };
        top.Children.Add(go);
        var web = new Button { Content = "Browse on CurseForge ↗", Margin = new Thickness(6, 0, 0, 0), ToolTip = "Opens the mod site in your browser. Paste the Project ID from there into the search box." };
        web.Click += (_, _) =>
        {
            var q = _search.Text.Trim();
            var url = "https://www.curseforge.com/ark-survival-ascended/search?page=1&pageSize=20&sortBy=relevancy" +
                      (q != "" && !IdRx.IsMatch(q) ? "&search=" + Uri.EscapeDataString(q) : "");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        };
        top.Children.Add(web);
        _search.ToolTip = "Search term – or one or more mod IDs (Project ID), comma separated";

        var bottom = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var nav = new StackPanel { Orientation = Orientation.Horizontal };
        nav.Children.Add(_prev); nav.Children.Add(_next);
        DockPanel.SetDock(nav, Dock.Right);
        bottom.Children.Add(nav); bottom.Children.Add(_info);

        _scroll = new ScrollViewer { Content = _results, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(top, Dock.Top); DockPanel.SetDock(bottom, Dock.Bottom); DockPanel.SetDock(_cats, Dock.Left);
        _cats.Margin = new Thickness(0, 0, 8, 0);
        root.Children.Add(top); root.Children.Add(bottom); root.Children.Add(_cats); root.Children.Add(_scroll);
        Content = root;

        go.Click += async (_, _) => { _index = 0; await Run(); };
        _search.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { _index = 0; await Run(); } };
        _sort.SelectionChanged += async (_, _) => { if (_ready) { _index = 0; await Run(); } };
        _size.SelectionChanged += async (_, _) => { if (_ready) { _index = 0; await Run(); } };
        _cats.SelectionChanged += async (_, _) => { if (_ready) { _index = 0; await Run(); } };
        _prev.Click += async (_, _) => { _index = Math.Max(0, _index - PageSize); await Run(); };
        _next.Click += async (_, _) => { _index += PageSize; await Run(); };

        Loaded += async (_, _) =>
        {
            _cats.Items.Add("All Categories");
            try { foreach (var c in await _cf.Categories()) _cats.Items.Add(c); }
            catch (Exception ex) { _info.Text = "Categories: " + ex.Message; }
            _cats.SelectedIndex = 0;
            if (_mapsOnly)
                foreach (var c in _cats.Items.OfType<CfCategory>())
                    if (c.Name.Equals("Maps", StringComparison.OrdinalIgnoreCase)) { _cats.SelectedItem = c; break; }
            _ready = true;
            await Run();
        };
    }

    int PageSize => (int)_size.SelectedItem;
    static readonly System.Text.RegularExpressions.Regex IdRx = new(@"^[\d,;\s]+$");
    bool _searchBlocked;

    async Task Run()
    {
        _results.Children.Clear();
        _info.Text = "Loading...";
        _prev.IsEnabled = _next.IsEnabled = false;
        var q = _search.Text.Trim();

        if (IdRx.IsMatch(q))
        {
            try
            {
                var ids = q.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).Distinct().ToList();
                var mods = await _cf.GetMods(ids);
                foreach (var m in mods) _results.Children.Add(Card(m));
                _info.Text = mods.Count == 0 ? "No mod found with this ID." : $"{mods.Count} mod(s) loaded by ID";
            }
            catch (Exception ex) { _info.Text = "Error: " + ex.Message; }
            return;
        }
        if (_searchBlocked) { ShowBlockedHint(); return; }

        try
        {
            var cat = (_cats.SelectedItem as CfCategory)?.Id;
            var sort = (int)((ComboBoxItem)_sort.SelectedItem).Tag;
            var (mods, total) = await _cf.Search(_search.Text, cat, sort, _index, PageSize);
            foreach (var m in mods) _results.Children.Add(Card(m));
            _info.Text = mods.Count == 0 ? "No mods found." : $"Showing {_index + 1}–{_index + mods.Count} of {total} mods";
            _prev.IsEnabled = _index > 0;
            _next.IsEnabled = _index + PageSize < Math.Min(total, 10000);
            _scroll.ScrollToTop();
        }
        catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _searchBlocked = true;
            ShowBlockedHint();
        }
        catch (Exception ex) { _info.Text = "Error: " + ex.Message; }
    }

    void ShowBlockedHint()
    {
        _info.Text = "Search is blocked for this API key – entering mod IDs works.";
        _results.Children.Clear();
        _results.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4), FontSize = 13,
            Text = "CurseForge allows your API key everything except the mod search (403). This is a CurseForge setting, not a problem with the key.\n\n" +
                   "How to add mods anyway:\n" +
                   "  1. \"Browse on CurseForge ↗\" opens the mod site in your browser.\n" +
                   "  2. Each mod page shows its \"Project ID\" on the right.\n" +
                   "  3. Paste that ID into the search box above (several separated by commas) and press Enter – the mod then shows up here with \"Add Mod\".\n\n" +
                   "To use the search directly here, request access to \"Search Mods\" for your key in the CurseForge developer console (console.curseforge.com) or from CurseForge support.",
        });
    }

    static string Fmt(long n) => n >= 1_000_000 ? $"{n / 1_000_000.0:0.#}M" : n >= 1000 ? $"{n / 1000.0:0.#}k" : n.ToString();
    static string FmtSize(long b) => b >= 1L << 30 ? $"{b / (double)(1L << 30):0.##} GB" : $"{b / (double)(1 << 20):0.##} MB";

    FrameworkElement Card(CfMod m)
    {
        var dock = new DockPanel();
        var img = new Image { Width = 110, Height = 110, Margin = new Thickness(0, 0, 10, 0), Stretch = Stretch.UniformToFill };
        if (m.Logo != null)
            try { img.Source = new BitmapImage(new Uri(m.Logo)); } catch { }
        DockPanel.SetDock(img, Dock.Left);

        var btns = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        var view = new Button { Content = "View", Width = 120 };
        view.IsEnabled = m.Url != null;
        view.Click += (_, _) => Process.Start(new ProcessStartInfo(m.Url!) { UseShellExecute = true });
        var already = _installed.Contains(m.Id.ToString());
        var add = new Button { Content = already ? "Already added ✓" : _mapsOnly ? "Use Map" : "Add Mod", Width = 120, IsEnabled = !already };
        add.Click += (_, _) => { if (_onAdd(m)) { add.Content = _mapsOnly ? "Selected ✓" : "Added ✓"; add.IsEnabled = false; } };
        btns.Children.Add(view); btns.Children.Add(add);
        DockPanel.SetDock(btns, Dock.Right);

        var info = new StackPanel();
        info.Children.Add(new TextBlock { Text = m.Name, FontWeight = FontWeights.Bold, FontSize = 15, Foreground = Theme.Accent });
        info.Children.Add(new TextBlock { Text = m.Summary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) });
        info.Children.Add(new TextBlock { Text = $"Mod ID: {m.Id}", Foreground = Theme.Muted });
        info.Children.Add(new TextBlock { Text = $"Categories: {m.Categories}", Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap });
        info.Children.Add(new TextBlock
        {
            Text = $"⬇ {Fmt(m.Downloads)}     updated {m.Updated:dd.MM.yyyy}     released {m.Released:dd.MM.yyyy}     {FmtSize(m.Size)}",
            Foreground = Theme.Muted,
        });

        dock.Children.Add(img); dock.Children.Add(btns); dock.Children.Add(info);
        return new Border { BorderBrush = Theme.Border, BorderThickness = new Thickness(1), Padding = new Thickness(6), Margin = new Thickness(0, 0, 0, 6), Child = dock };
    }
}
