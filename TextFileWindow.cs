using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ObeliskPanel;

public class TextFileWindow : Window
{
    public TextFileWindow(string path, string title)
    {
        Title = title; Width = 560; Height = 480;
        var box = new TextBox
        {
            Text = File.Exists(path) ? File.ReadAllText(path) : "", AcceptsReturn = true,
            FontFamily = new FontFamily("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        var save = new Button { Content = "Save", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        save.Click += (_, _) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, box.Text);
            Close();
        };
        var dock = new DockPanel { Margin = new Thickness(10) };
        var info = new TextBlock { Text = path, Foreground = Theme.Muted, Margin = new Thickness(0, 0, 0, 6), TextTrimming = TextTrimming.CharacterEllipsis };
        DockPanel.SetDock(info, Dock.Top); DockPanel.SetDock(save, Dock.Bottom);
        dock.Children.Add(info); dock.Children.Add(save); dock.Children.Add(box);
        Content = dock;
    }
}
