using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ObeliskPanel;

public class WorkshopBrowserWindow : Window
{
    readonly string _key;
    readonly ICollection<string> _installed;
    readonly Func<WorkshopListing, bool> _onAdd;
    readonly bool _mapsOnly;
    readonly ComboBox _sort = new() { Width = 180 };
    readonly ComboBox _size = new() { Width = 80, ItemsSource = new[] { 10, 20, 50 }, SelectedIndex = 1 };
    readonly ComboBox _tag = new() { Width = 150 };
    readonly TextBox _search = new() { Width = 300 };
    readonly StackPanel _results = new();
    readonly TextBlock _info = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly Button _prev = new() { Content = "◀ Previous" };
    readonly Button _next = new() { Content = "Next ▶" };
    readonly ScrollViewer _scroll;
    int _page = 1;
    bool _ready;

    static readonly string[] Tags = { "All", "Map", "Creatures", "Structures", "Items", "Weapons", "Gameplay", "Utility", "Overhaul", "Total Conversion" };
    static readonly System.Text.RegularExpressions.Regex IdRx = new(@"^[\d,;\s]+$");

    public WorkshopBrowserWindow(string steamApiKey, ICollection<string> installed, Func<WorkshopListing, bool> onAdd, bool mapsOnly = false)
    {
        _key = steamApiKey; _installed = installed; _onAdd = onAdd; _mapsOnly = mapsOnly;
        Title = mapsOnly ? "ASE Map Browser (Steam Workshop)" : "ASE Mod Browser (Steam Workshop)";
        Width = 1050; Height = 760;

        foreach (var (label, type) in SteamWorkshop.Sorts) _sort.Items.Add(new ComboBoxItem { Content = label, Tag = type });
        _sort.SelectedIndex = 0;
        _tag.ItemsSource = Tags;
        _tag.SelectedItem = mapsOnly ? "Map" : "All";

        var top = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        void L(string t) => top.Children.Add(new TextBlock { Text = t, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        L("Sort by"); top.Children.Add(_sort);
        L("   Tag"); top.Children.Add(_tag);
        L("   Per page"); top.Children.Add(_size);
        L("   Search"); top.Children.Add(_search);
        _search.ToolTip = "Search term – or one or more Workshop IDs, comma separated";
        var go = new Button { Content = "Search", Margin = new Thickness(6, 0, 0, 0) };
        top.Children.Add(go);
        var web = new Button { Content = "Open Workshop ↗", Margin = new Thickness(6, 0, 0, 0) };
        web.Click += (_, _) => Process.Start(new ProcessStartInfo(SteamWorkshop.BrowseUrl) { UseShellExecute = true });
        top.Children.Add(web);

        var bottom = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var nav = new StackPanel { Orientation = Orientation.Horizontal };
        nav.Children.Add(_prev); nav.Children.Add(_next);
        DockPanel.SetDock(nav, Dock.Right);
        bottom.Children.Add(nav); bottom.Children.Add(_info);

        _scroll = new ScrollViewer { Content = _results, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(top, Dock.Top); DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(top); root.Children.Add(bottom); root.Children.Add(_scroll);
        Content = root;

        go.Click += async (_, _) => { _page = 1; await Run(); };
        _search.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { _page = 1; await Run(); } };
        foreach (var cb in new[] { _sort, _size, _tag }) cb.SelectionChanged += async (_, _) => { if (_ready) { _page = 1; await Run(); } };
        _prev.Click += async (_, _) => { _page = Math.Max(1, _page - 1); await Run(); };
        _next.Click += async (_, _) => { _page++; await Run(); };
        Loaded += async (_, _) => { _ready = true; await Run(); };
    }

    int PageSize => (int)_size.SelectedItem;

    async Task Run()
    {
        _results.Children.Clear();
        _info.Text = "Loading...";
        _prev.IsEnabled = _next.IsEnabled = false;
        var q = _search.Text.Trim();
        try
        {
            if (IdRx.IsMatch(q) || q.Contains("id="))
            {
                var ids = System.Text.RegularExpressions.Regex.Matches(q, @"\d{5,}").Select(m => m.Value).Distinct().ToList();
                var items = await SteamWorkshop.GetDetails(ids);
                foreach (var w in items)
                    _results.Children.Add(Card(new WorkshopListing(w.Id, w.Title, Short(w.Description), null, 0, default, w.Size, "")));
                _info.Text = items.Count == 0 ? "No Workshop item found with this ID." : $"{items.Count} item(s) loaded by ID";
                return;
            }
            if (string.IsNullOrWhiteSpace(_key)) { ShowNoKeyHint(); return; }
            var tag = _tag.SelectedItem as string;
            var (list, total) = await SteamWorkshop.Query(_key, q, (int)((ComboBoxItem)_sort.SelectedItem).Tag, _page, PageSize,
                                                         tag == "All" ? null : tag);
            foreach (var w in list) _results.Children.Add(Card(w));
            int from = (_page - 1) * PageSize;
            _info.Text = list.Count == 0 ? "No items found." : $"Showing {from + 1}–{from + list.Count} of {total} items";
            _prev.IsEnabled = _page > 1;
            _next.IsEnabled = from + PageSize < total;
            _scroll.ScrollToTop();
        }
        catch (Exception ex) { _info.Text = "Error: " + ex.Message; }
    }

    static string Short(string bbcode)
    {
        var t = System.Text.RegularExpressions.Regex.Replace(bbcode, @"\[[^\]]*\]", " ");
        t = string.Join(" ", t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return t.Length > 260 ? t[..260] + "…" : t;
    }

    void ShowNoKeyHint()
    {
        _info.Text = "Searching needs a Steam Web API key – entering Workshop IDs or links works without one.";
        _results.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4), FontSize = 13,
            Text = "To search the Workshop here, add a free Steam Web API key under Server Settings → Cluster & App " +
                   "(get one at steamcommunity.com/dev/apikey).\n\n" +
                   "Without a key:\n" +
                   "  1. \"Open Workshop ↗\" opens the ARK: Survival Evolved Workshop in your browser.\n" +
                   "  2. Copy the link of a mod or map (or the number after ?id=).\n" +
                   "  3. Paste it into the search box above and press Enter – it then shows up here with \"Add\".",
        });
    }

    static string Fmt(long n) => n >= 1_000_000 ? $"{n / 1_000_000.0:0.#}M" : n >= 1000 ? $"{n / 1000.0:0.#}k" : n.ToString();
    static string FmtSize(long b) => b <= 0 ? "" : b >= 1L << 30 ? $"{b / (double)(1L << 30):0.##} GB" : $"{b / (double)(1 << 20):0.##} MB";

    FrameworkElement Card(WorkshopListing m)
    {
        var dock = new DockPanel();
        var img = new Image { Width = 110, Height = 110, Margin = new Thickness(0, 0, 10, 0), Stretch = Stretch.UniformToFill };
        if (m.Preview != null) try { img.Source = new BitmapImage(new Uri(m.Preview)); } catch { }
        DockPanel.SetDock(img, Dock.Left);

        var btns = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        var view = new Button { Content = "View", Width = 120 };
        view.Click += (_, _) => Process.Start(new ProcessStartInfo(SteamWorkshop.ItemUrl(m.Id)) { UseShellExecute = true });
        var already = _installed.Contains(m.Id);
        var add = new Button { Content = already ? "Already added ✓" : _mapsOnly ? "Use Map" : "Add Mod", Width = 120, IsEnabled = !already };
        add.Click += (_, _) => { if (_onAdd(m)) { add.Content = _mapsOnly ? "Selected ✓" : "Added ✓"; add.IsEnabled = false; } };
        btns.Children.Add(view); btns.Children.Add(add);
        DockPanel.SetDock(btns, Dock.Right);

        var info = new StackPanel();
        info.Children.Add(new TextBlock { Text = m.Title, FontWeight = FontWeights.Bold, FontSize = 15, Foreground = Theme.Ase });
        info.Children.Add(new TextBlock { Text = m.Summary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) });
        info.Children.Add(new TextBlock { Text = $"Workshop ID: {m.Id}", Foreground = Theme.Muted });
        if (m.Tags != "") info.Children.Add(new TextBlock { Text = $"Tags: {m.Tags}", Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap });
        var meta = string.Join("     ", new[]
        {
            m.Subscriptions > 0 ? $"👥 {Fmt(m.Subscriptions)} subscribers" : "",
            m.Updated != default ? $"updated {m.Updated:dd.MM.yyyy}" : "",
            FmtSize(m.Size),
        }.Where(x => x != ""));
        if (meta != "") info.Children.Add(new TextBlock { Text = meta, Foreground = Theme.Muted });

        dock.Children.Add(img); dock.Children.Add(btns); dock.Children.Add(info);
        return new Border { BorderBrush = Theme.Border, BorderThickness = new Thickness(1), Padding = new Thickness(6), Margin = new Thickness(0, 0, 0, 6), Child = dock };
    }
}
