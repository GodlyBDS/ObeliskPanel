using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ObeliskPanel;

public record Prerequisite(string Name, string Why, string Url, Func<bool> IsInstalled, Func<AppSettings, bool> Needed);

public static class Prerequisites
{
    const string VcRedistPage = "https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist";
    const string DirectXPage = "https://www.microsoft.com/download/details.aspx?id=35";

    static string Sys32(string dll) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), dll);

    static bool VcRuntime(string version, string dll)
    {
        try
        {
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var k = hklm.OpenSubKey($@"SOFTWARE\Microsoft\VisualStudio\{version}\VC\Runtimes\x64");
            if (k?.GetValue("Installed") is int i && i == 1) return true;
        }
        catch { }
        return File.Exists(Sys32(dll));
    }

    static bool HasAsa(AppSettings s) => s.Mode == GameType.ASA || s.Servers.Any(p => p.Game == GameType.ASA);
    static bool HasAse(AppSettings s) => s.Mode == GameType.ASE || s.Servers.Any(p => p.Game == GameType.ASE);

    public static readonly Prerequisite[] All =
    {
        new("Microsoft Visual C++ Redistributable 2015–2022 (x64)",
            "ARK: Survival Ascended servers need it to start.",
            VcRedistPage, () => VcRuntime("14.0", "msvcp140.dll"), HasAsa),
        new("Microsoft Visual C++ Redistributable 2013 (x64)",
            "ARK: Survival Evolved servers need it to start.",
            VcRedistPage, () => VcRuntime("12.0", "msvcr120.dll"), HasAse),
        new("DirectX End-User Runtime",
            "ARK: Survival Evolved servers need the legacy DirectX files (e.g. xinput1_3.dll) to start.",
            DirectXPage, () => File.Exists(Sys32("xinput1_3.dll")) && File.Exists(Sys32("d3dx9_43.dll")), HasAse),
    };

    public static void OpenPage(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    public static void CheckAndPrompt(AppSettings s, Window owner, Action<string> log)
    {
        foreach (var p in All.Where(p => p.Needed(s) && !p.IsInstalled()))
        {
            var answer = MessageBox.Show(owner,
                $"You need {p.Name} to use this program.\n\n{p.Why}\n\nInstall it now? \"Yes\" opens the official Microsoft download page in your browser.",
                "ObeliskPanel – missing requirement", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
            if (answer == MessageBoxResult.Yes) OpenPage(p.Url);
            else log($"{p.Name} is not installed – servers may fail to start.");
        }
    }
}
