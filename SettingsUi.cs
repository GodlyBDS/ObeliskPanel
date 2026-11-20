using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ObeliskPanel;

public class IniContext
{
    public IniFile Gus { get; }
    public IniFile Game { get; }
    readonly Func<bool> _running;
    readonly Action<string> _log;
    bool _warned;

    public IniContext(ServerProfile p, Func<bool> running, Action<string> log)
    {
        Gus = IniFile.Load(p.GameUserSettingsPath);
        Game = IniFile.Load(p.GameIniPath);
        _running = running; _log = log;
    }

    public IniFile Get(IniKind k) => k == IniKind.GUS ? Gus : Game;

    public void Changed(IniFile f)
    {
        f.Save();
        if (!_warned && _running())
        {
            _warned = true;
            _log("Note: The server is running – changes take effect after a restart.");
        }
    }
}

public static class SettingsUi
{
    public static bool IsTrue(string? v) => string.Equals(v?.Trim(), "True", StringComparison.OrdinalIgnoreCase);

    static bool Same(string? a, string? b, SType t)
    {
        if (a == null || b == null) return a == b;
        if (t == SType.Bool) return IsTrue(a) == IsTrue(b);
        if (t is SType.Float or SType.Int &&
            double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
            double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            return Math.Abs(x - y) < 1e-9;
        return a == b;
    }

    static Button ResetButton() => new()
    {
        Content = "✕", Foreground = Theme.Bad, Background = Brushes.Transparent,
        BorderThickness = new Thickness(0), Padding = new Thickness(3, 0, 3, 0),
        Margin = new Thickness(3, 0, 6, 0), MinWidth = 0, FontWeight = FontWeights.Bold,
        VerticalAlignment = VerticalAlignment.Center, ToolTip = "Reset to default value",
    };

    public static (FrameworkElement ctrl, Button reset) Field(IniContext c, IniKind k, string section, string key,
                                                              SType t, string? def, double width = 90)
    {
        var f = c.Get(k);
        string? Current() => f.Get(section, key) ?? def;
        var reset = ResetButton();
        void UpdateX() => reset.Visibility = def != null && !Same(Current(), def, t) ? Visibility.Visible : Visibility.Hidden;
        UpdateX();

        if (t == SType.Bool)
        {
            var cb = new CheckBox { IsChecked = IsTrue(Current()), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 0, 4) };
            cb.Click += (_, _) => { f.Set(section, key, cb.IsChecked == true ? "True" : "False"); c.Changed(f); UpdateX(); };
            reset.Click += (_, _) => { f.Set(section, key, def!); cb.IsChecked = IsTrue(def); c.Changed(f); UpdateX(); };
            return (cb, reset);
        }

        var tb = new TextBox
        {
            Text = Current() ?? "", Width = width, VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = t == SType.Text ? TextAlignment.Left : TextAlignment.Right,
        };
        tb.LostFocus += (_, _) =>
        {
            var v = tb.Text.Trim();
            if (t != SType.Text) v = v.Replace(',', '.');
            if (Same(v, f.Get(section, key), t)) return;
            bool bad = (t == SType.Float && !double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    || (t == SType.Int && !long.TryParse(v, out _));
            if (bad) { tb.BorderBrush = Theme.Bad; tb.ToolTip = "Invalid number"; return; }
            tb.ClearValue(Control.BorderBrushProperty); tb.ToolTip = null;
            tb.Text = v;
            f.Set(section, key, v); c.Changed(f); UpdateX();
        };
        reset.Click += (_, _) => { f.Set(section, key, def!); tb.Text = def!; c.Changed(f); UpdateX(); };
        return (tb, reset);
    }

    static Button ResetAllButton(IniContext c, IEnumerable<(IniKind k, string sec, string key, string def)> items, Action rebuild)
    {
        var b = new Button { Content = "Reset All", MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 10) };
        b.Click += (_, _) =>
        {
            if (MessageBox.Show("Reset all values on this page to default?", "Reset All", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            foreach (var (k, sec, key, def) in items) c.Get(k).Set(sec, key, def);
            c.Changed(c.Gus); c.Changed(c.Game);
            rebuild();
        };
        return b;
    }

    public static FrameworkElement BuildPage(IniContext c, IEnumerable<SettingDef> defs, Action rebuild)
    {
        var list = defs.ToList();
        var grid = new Grid { Margin = new Thickness(8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(290) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        int row = 0;
        void Put(UIElement e, int col, int span = 1)
        {
            while (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(e, row); Grid.SetColumn(e, col); Grid.SetColumnSpan(e, span);
            grid.Children.Add(e);
        }

        Put(ResetAllButton(c, list.Select(d => (d.File, d.Section, d.Key, d.Default)), rebuild), 0, 4); row++;
        string? group = null;
        foreach (var d in list)
        {
            if (d.Group != group)
            {
                group = d.Group;
                if (group != "")
                {
                    Put(new TextBlock { Text = group, FontWeight = FontWeights.Bold, FontSize = 15, Margin = new Thickness(0, row > 1 ? 14 : 0, 0, 4) }, 0, 4);
                    row++;
                }
            }
            Put(new TextBlock { Text = d.Label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 10, 2) }, 0);
            var (ctrl, x) = Field(c, d.File, d.Section, d.Key, d.Type, d.Default);
            ctrl.HorizontalAlignment = d.Type == SType.Bool ? HorizontalAlignment.Center : HorizontalAlignment.Right;
            Put(ctrl, 1); Put(x, 2);
            var desc = d.Desc == "" ? $"Default: {d.Default}" : $"{d.Desc}  (Default: {d.Default})";
            Put(new TextBlock { Text = desc, Foreground = Theme.Muted, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap }, 3);
            row++;
        }
        return new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    record StatCol(string Header, Func<int, string> Key, Func<int, string> Default);

    public static FrameworkElement StatsPage(IniContext c, Action rebuild)
    {
        string[] pNames = { "Health", "Stamina", "Torpor", "Oxygen", "Food", "Water", "Temperature", "Weight", "Melee", "Speed", "Fortitude", "Crafting" };
        var pIdx = Enumerable.Range(0, 12).ToArray();
        int[] cIdx = { 0, 1, 2, 3, 4, 7, 8, 9 };
        string[] cNames = { "Health", "Stamina", "Torpor", "Oxygen", "Food", "Weight", "Damage", "Speed" };

        var playerCols = new[]
        {
            new StatCol("Base", i => $"PlayerBaseStatMultipliers[{i}]", _ => "1"),
            new StatCol("Per Level", i => $"PerLevelStatsMultiplier_Player[{i}]", _ => "1"),
        };
        var creatureCols = new[]
        {
            new StatCol("Wild Per Level", i => $"PerLevelStatsMultiplier_DinoWild[{i}]", _ => "1"),
            new StatCol("Tamed Per Level", i => $"PerLevelStatsMultiplier_DinoTamed[{i}]", i => i == 0 ? "0.2" : i == 8 ? "0.17" : "1"),
            new StatCol("Taming Reward", i => $"PerLevelStatsMultiplier_DinoTamed_Add[{i}]", i => i is 0 or 8 ? "0.14" : "1"),
            new StatCol("Max Effectiveness Reward", i => $"PerLevelStatsMultiplier_DinoTamed_Affinity[{i}]", i => i is 0 or 8 ? "0.44" : "1"),
        };

        var all = playerCols.SelectMany(col => pIdx.Select(i => (IniKind.Game, SettingDefs.GM, col.Key(i), col.Default(i))))
            .Concat(creatureCols.SelectMany(col => cIdx.Select(i => (IniKind.Game, SettingDefs.GM, col.Key(i), col.Default(i)))));

        var sp = new StackPanel { Margin = new Thickness(8) };
        sp.Children.Add(ResetAllButton(c, all, rebuild));
        sp.Children.Add(Table(c, "Player Stats", pNames, pIdx, playerCols));
        sp.Children.Add(Table(c, "Creature Stats", cNames, cIdx, creatureCols));
        return new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    static FrameworkElement Table(IniContext c, string title, string[] names, int[] idx, StatCol[] cols)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        foreach (var _ in cols)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        }
        void Put(UIElement e, int r, int col, int span = 1)
        {
            while (grid.RowDefinitions.Count <= r) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(e, r); Grid.SetColumn(e, col); Grid.SetColumnSpan(e, span); grid.Children.Add(e);
        }
        Put(new TextBlock { Text = title, FontWeight = FontWeights.Bold, FontSize = 15, Margin = new Thickness(0, 0, 0, 4) }, 0, 0, 1 + cols.Length * 2);
        for (int j = 0; j < cols.Length; j++)
            Put(new TextBlock { Text = cols[j].Header, HorizontalAlignment = HorizontalAlignment.Center }, 1, 1 + j * 2, 2);
        for (int r = 0; r < idx.Length; r++)
        {
            Put(new TextBlock { Text = names[r], VerticalAlignment = VerticalAlignment.Center }, r + 2, 0);
            for (int j = 0; j < cols.Length; j++)
            {
                var (ctrl, x) = Field(c, IniKind.Game, SettingDefs.GM, cols[j].Key(idx[r]), SType.Float, cols[j].Default(idx[r]), 70);
                ctrl.Margin = new Thickness(0, 2, 0, 2);
                Put(ctrl, r + 2, 1 + j * 2);
                x.HorizontalAlignment = HorizontalAlignment.Left;
                Put(x, r + 2, 2 + j * 2);
            }
        }
        return grid;
    }
}
