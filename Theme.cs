using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace ObeliskPanel;

public static class Theme
{
    public static bool IsDark { get; private set; }

    public static Brush Muted => IsDark ? B(0xA6, 0xA6, 0xA6) : B(0x6B, 0x6B, 0x6B);
    public static Brush Border => IsDark ? B(0x3F, 0x3F, 0x46) : B(0xD6, 0xD6, 0xD6);
    public static Brush RowAlt => IsDark ? B(0x2B, 0x2B, 0x30) : B(0xF4, 0xF4, 0xF6);
    public static Brush Card => IsDark ? B(0x26, 0x26, 0x2B) : B(0xFF, 0xFF, 0xFF);
    public static Brush Bar => IsDark ? B(0x1C, 0x1C, 0x20) : B(0xEC, 0xEC, 0xEF);
    public static Brush Accent => IsDark ? B(0xF0, 0xA0, 0x4B) : B(0xB8, 0x5C, 0x12);
    public static Brush Warn => IsDark ? B(0xFF, 0xB3, 0x4D) : B(0xC2, 0x62, 0x00);
    public static Brush Good => IsDark ? B(0x5C, 0xD6, 0x7A) : B(0x1E, 0x8E, 0x3E);
    public static Brush Bad => IsDark ? B(0xFF, 0x6B, 0x6B) : B(0xC4, 0x2B, 0x1C);

    public static Brush Asa => IsDark ? B(0x5A, 0xB4, 0xFF) : B(0x00, 0x5F, 0xB8);
    public static Brush Ase => IsDark ? B(0x7C, 0xD9, 0x6A) : B(0x2E, 0x7D, 0x32);
    public static Brush ModeBrush(GameType g) => g == GameType.ASA ? Asa : Ase;
    public static Brush ModeOnDark(GameType g) => g == GameType.ASA ? B(0x5A, 0xB4, 0xFF) : B(0x7C, 0xD9, 0x6A);

    static SolidColorBrush B(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    static bool SystemIsDark()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    public static void Apply(string mode)
    {
        IsDark = mode == "Dark" || (mode == "System" && SystemIsDark());
#pragma warning disable WPF0001
        Application.Current.ThemeMode = IsDark ? ThemeMode.Dark : ThemeMode.Light;
#pragma warning restore WPF0001
        var r = Application.Current.Resources;
        r["FieldBg"] = IsDark ? B(0x1E, 0x1E, 0x22) : B(0xFF, 0xFF, 0xFF);
        r["FieldFg"] = IsDark ? B(0xF0, 0xF0, 0xF0) : B(0x1A, 0x1A, 0x1A);
        r["FieldBorder"] = IsDark ? B(0x4A, 0x4A, 0x52) : B(0xC4, 0xC4, 0xC8);
        r["FieldFocus"] = IsDark ? B(0x00, 0xD9, 0xCE) : B(0x00, 0x7A, 0x74);

        var md = r.MergedDictionaries;
        var own = md.FirstOrDefault(d => d.Source?.OriginalString.EndsWith("Styles.xaml") == true);
        if (own != null) md.Remove(own);
        md.Add(own ?? new ResourceDictionary { Source = new Uri("pack://application:,,,/Styles.xaml") });
    }
}
