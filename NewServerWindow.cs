using System.Windows;
using System.Windows.Controls;

namespace ObeliskPanel;

public class NewServerWindow : Window
{
    public string ServerName => _name.Text.Trim();
    public string SessionName => _session.Text.Trim();
    public GameType Game { get; }
    public string Map => _map.Text.Trim();
    public ServerProfile? Source => _copySettings.IsChecked == true || _copyMods.IsChecked == true ? _source.SelectedItem as ServerProfile : null;
    public bool CopySettings => _copySettings.IsChecked == true && _source.SelectedItem != null;
    public bool CopyMods => _copyMods.IsChecked == true && _source.SelectedItem != null;

    readonly TextBox _name, _session;
    readonly ComboBox _map, _source;
    readonly CheckBox _copySettings, _copyMods;

    public NewServerWindow(string defaultName, GameType game, IList<ServerProfile> all, ServerProfile? preselect)
    {
        Game = game;
        var existing = all.Where(x => x.Game == game).ToList();
        if (preselect != null && preselect.Game != game) preselect = null;
        Title = $"New {game} Server";
        Width = 520; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var g = new Grid { Margin = new Thickness(18) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        int row = 0;
        void Put(UIElement e, int col, int span = 1)
        {
            while (g.RowDefinitions.Count <= row) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(e, row); Grid.SetColumn(e, col); Grid.SetColumnSpan(e, span); g.Children.Add(e);
        }
        void Label(string t) => Put(new TextBlock { Text = t, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 10, 4) }, 0);

        var src = preselect ?? existing.FirstOrDefault();

        Label("Profile Name");
        _name = new TextBox { Text = defaultName, Margin = new Thickness(0, 4, 0, 4) };
        Put(_name, 1); row++;

        Label("Game");
        Put(new TextBlock
        {
            Text = game == GameType.ASA ? "ARK: Survival Ascended" : "ARK: Survival Evolved",
            FontWeight = FontWeights.SemiBold, Foreground = Theme.ModeBrush(game),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 0, 4),
        }, 1);
        row++;

        Label("Map");
        _map = new ComboBox { IsEditable = true, Margin = new Thickness(0, 4, 0, 4) };
        _map.ItemsSource = game == GameType.ASA ? MainPage.AsaMaps : MainPage.AseMaps;
        var used = existing.Select(x => x.Map).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _map.Text = ((string[])_map.ItemsSource).FirstOrDefault(m => !used.Contains(m)) ?? ((string[])_map.ItemsSource)[0];
        Put(_map, 1); row++;

        Label("Session Name");
        _session = new TextBox { Margin = new Thickness(0, 4, 0, 4) };
        _session.Text = src != null ? $"{src.SessionName} - {_map.Text.Replace("_WP", "").Replace("_P", "")}" : "My ARK Server";
        _map.SelectionChanged += (_, _) =>
        {
            if (_map.SelectedItem is string m && src != null) _session.Text = $"{src.SessionName} - {m.Replace("_WP", "").Replace("_P", "")}";
        };
        Put(_session, 1); row++;

        var box = new GroupBox { Header = "Copy from existing server", Margin = new Thickness(0, 14, 0, 0), Padding = new Thickness(10) };
        var sp = new StackPanel();
        _source = new ComboBox { ItemsSource = existing, SelectedItem = src, Margin = new Thickness(0, 0, 0, 8), IsEnabled = existing.Count > 0 };
        _copySettings = new CheckBox
        {
            Content = "Copy settings (all INIs, launch options, passwords)",
            IsChecked = existing.Count > 0, IsEnabled = existing.Count > 0, Margin = new Thickness(0, 3, 0, 3),
        };
        _copyMods = new CheckBox { Content = "Copy mods (incl. mod settings in the INIs)", IsChecked = existing.Count > 0, IsEnabled = existing.Count > 0, Margin = new Thickness(0, 3, 0, 3) };
        sp.Children.Add(_source); sp.Children.Add(_copySettings); sp.Children.Add(_copyMods);
        if (existing.Count == 0) sp.Children.Add(new TextBlock { Text = $"No other {game} server yet.", Foreground = Theme.Muted });
        sp.Children.Add(new TextBlock
        {
            Text = "Ports are assigned automatically so the servers don't conflict. Save games are not copied.",
            Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0),
        });
        box.Content = sp;
        Put(box, 0, 2); row++;

        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var ok = new Button { Content = "Create Server", IsDefault = true, MinWidth = 120, Margin = new Thickness(0, 0, 8, 0) };
        ok.SetResourceReference(StyleProperty, "AccentButtonStyle");
        ok.Click += (_, _) =>
        {
            if (ServerName == "" || Map == "") { MessageBox.Show("Name and map must not be empty.", Title); return; }
            DialogResult = true;
        };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 100 };
        btns.Children.Add(ok); btns.Children.Add(cancel);
        Put(btns, 0, 2);

        Content = g;
    }
}
