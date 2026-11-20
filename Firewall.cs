using System.Diagnostics;

namespace ObeliskPanel;

public static class Firewall
{
    static int Netsh(string args)
    {
        using var p = Process.Start(new ProcessStartInfo("netsh", args)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        })!;
        p.WaitForExit(10000);
        return p.ExitCode;
    }

    public static void EnsureRule(ServerProfile p, Action<string> log)
    {
        if (!App.IsAdmin)
        {
            log($"[{p.Name}] Firewall rule skipped – ObeliskPanel is not running as administrator.");
            return;
        }
        var name = $"ObeliskPanel - {p.Name}";
        if (Netsh($"advfirewall firewall show rule name=\"{name}\"") == 0 ||
            Netsh($"advfirewall firewall show rule name=\"ArkManager - {p.Name}\"") == 0) return;
        foreach (var proto in new[] { "UDP", "TCP" })
            Netsh($"advfirewall firewall add rule name=\"{name}\" dir=in action=allow program=\"{p.ExePath}\" protocol={proto} enable=yes");
        log($"[{p.Name}] Firewall rule created.");
    }
}
