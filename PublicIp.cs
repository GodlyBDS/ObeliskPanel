using System.Net.Http;

namespace ObeliskPanel;

public static class PublicIp
{
    static readonly string[] Services = { "https://api.ipify.org", "https://ifconfig.me/ip", "https://icanhazip.com" };
    static string? _cached;
    static DateTime _fetched;

    public static string? Current => _cached;

    public static async Task<string?> Get(AppSettings s, bool force = false)
    {
        if (!string.IsNullOrWhiteSpace(s.PublicAddressOverride)) return s.PublicAddressOverride.Trim();
        if (!force && _cached != null && DateTime.Now - _fetched < TimeSpan.FromMinutes(30)) return _cached;
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        foreach (var url in Services)
        {
            try
            {
                var ip = (await http.GetStringAsync(url)).Trim();
                if (System.Net.IPAddress.TryParse(ip, out _))
                {
                    _cached = ip;
                    _fetched = DateTime.Now;
                    return ip;
                }
            }
            catch { }
        }
        return _cached;
    }

    public static string Address(AppSettings s, ServerProfile p)
    {
        var host = !string.IsNullOrWhiteSpace(s.PublicAddressOverride) ? s.PublicAddressOverride.Trim() : _cached;
        return host == null ? $"(unknown):{p.Port}" : $"{host}:{p.Port}";
    }
}
