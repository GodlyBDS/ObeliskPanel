using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace ObeliskPanel;

public partial class App : Application
{
    public static bool IsAdmin { get; } = CheckAdmin();

    static bool CheckAdmin()
    {
        try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
        catch { return false; }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Theme.Apply(ConfigStore.Load().Theme);
        if (!IsAdmin && !e.Args.Contains("--no-admin-check") && AskRestartAsAdmin())
        {
            StartupUri = null;
            Shutdown();
            return;
        }
        base.OnStartup(e);
    }

    static bool AskRestartAsAdmin()
    {
        var answer = MessageBox.Show(
            "ObeliskPanel is running without administrator rights.\n\n" +
            "Some features may not work as expected – for example creating firewall rules for your servers, " +
            "detecting or stopping servers that were started as administrator, and writing to protected folders.\n\n" +
            "Restart ObeliskPanel as administrator now?",
            "ObeliskPanel – administrator rights", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
        if (answer != MessageBoxResult.Yes) return false;
        try
        {
            var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName;
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" });
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            MessageBox.Show("Administrator rights were not granted. ObeliskPanel continues with limited rights.",
                            "ObeliskPanel", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
    }
}
