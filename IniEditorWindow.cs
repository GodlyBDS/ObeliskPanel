using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ObeliskPanel;

public class IniEditorWindow : Window
{
    readonly List<(string path, TextBox box)> _tabs = new();

    public IniEditorWindow(ServerProfile p)
    {
        Title = $"Edit INI – {p.Name}";
        Width = 900; Height = 650;
        var tabs = new TabControl();
        foreach (var path in new[] { p.GameUserSettingsPath, p.GameIniPath, p.EngineIniPath })
        {
            var box = new TextBox
            {
                Text = File.Exists(path) ? File.ReadAllText(path) : "",
                AcceptsReturn = true, AcceptsTab = true,
                FontFamily = new FontFamily("Consolas"),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            _tabs.Add((path, box));
            tabs.Items.Add(new TabItem { Header = Path.GetFileName(path), Content = box });
        }

        var save = new Button { Content = "Save (with backup)", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        save.Click += (_, _) => SaveAll();
        var hint = new TextBlock { Text = "Stop the server first – ARK overwrites the INIs on shutdown.", Foreground = Theme.Warn, Margin = new Thickness(0, 8, 0, 0) };

        var dock = new DockPanel { Margin = new Thickness(10) };
        var bottom = new DockPanel();
        DockPanel.SetDock(save, Dock.Right);
        bottom.Children.Add(save); bottom.Children.Add(hint);
        DockPanel.SetDock(bottom, Dock.Bottom);
        dock.Children.Add(bottom); dock.Children.Add(tabs);
        Content = dock;
    }

    void SaveAll()
    {
        foreach (var (path, box) in _tabs)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path))
            {
                if (File.ReadAllText(path) == box.Text) continue;
                File.Copy(path, path + $".{DateTime.Now:yyyyMMdd-HHmmss}.bak", true);
            }
            File.WriteAllText(path, box.Text);
        }
        MessageBox.Show("Saved.", Title);
    }
}
