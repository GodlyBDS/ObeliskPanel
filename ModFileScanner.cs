using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ObeliskPanel;

public static class ModFileScanner
{
    static readonly Encoding Latin1 = Encoding.Latin1;

    static readonly Regex Pair = new(@"\x1c[\s\S]{4}\x1f([A-Za-z0-9_\.\- ]{2,80})\x00\x1f([A-Za-z_][A-Za-z0-9_\.\[\]\-]{1,100})\x00", RegexOptions.Compiled);
    static readonly Regex SectionOnly = new(@"\x1c[\s\S]{4}(?:\x1f\x00)?\x1f([A-Za-z0-9_\.\- ]{2,80})\x00[\x00\x01\x48]", RegexOptions.Compiled);
    static readonly Regex Const = new(@"\x1f([A-Za-z_][A-Za-z0-9_]{2,80})\x00", RegexOptions.Compiled);

    static readonly string[] ConfigFunctions = { "GetBoolOptionIni", "GetFloatOptionIni", "GetIntOptionIni", "GetStringOption" };

    static bool IsVanilla(string s) =>
        s.StartsWith("/script/", StringComparison.OrdinalIgnoreCase) ||
        new[] { "ServerSettings", "SessionSettings", "MessageOfTheDay" }.Contains(s, StringComparer.OrdinalIgnoreCase);

    public static List<string> FindModFolders(IEnumerable<ServerProfile> servers, GameType game, string modId)
    {
        var res = new List<string>();
        foreach (var p in servers.Where(s => s.Game == game).Select(s => s.InstallDir).Distinct())
        {
            var root = game == GameType.ASA
                ? Path.Combine(p, @"ShooterGame\Binaries\Win64\ShooterGame\Mods\83374")
                : Path.Combine(p, @"ShooterGame\Content\Mods");
            if (!Directory.Exists(root)) continue;
            res.AddRange(game == GameType.ASA
                ? Directory.GetDirectories(root, modId + "_*")
                : Directory.GetDirectories(root, modId));
        }
        return res;
    }

    public static Dictionary<string, SortedSet<string>> Scan(string modFolder, GameType game, string? oodleDll)
    {
        var blobs = new List<byte[]>();
        if (game == GameType.ASA)
        {
            var utocs = Directory.GetFiles(modFolder, "*.utoc", SearchOption.AllDirectories);
            if (utocs.Length == 0) throw new FileNotFoundException("No .utoc file in the mod folder.");
            var dll = IoStore.FindOodle(oodleDll) ?? throw new FileNotFoundException(
                "Oodle library (oo2core_9_win64.dll) not found. Set its path under Server Settings → Cluster & App.");
            foreach (var u in utocs) blobs.Add(IoStore.Unpack(u, dll));
        }
        else
        {
            foreach (var f in Directory.GetFiles(modFolder, "*.*", SearchOption.AllDirectories)
                         .Where(f => f.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".uexp", StringComparison.OrdinalIgnoreCase)))
                blobs.Add(File.ReadAllBytes(f));
        }

        var result = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var blob in blobs)
        {
            var text = Latin1.GetString(blob);
            if (!ConfigFunctions.Any(f => text.Contains(f))) continue;
            ScanText(text, game, result);
        }
        return result;
    }

    static int CommonPrefix(string a, string b)
    {
        int n = 0;
        while (n < Math.Min(a.Length, b.Length) && char.ToLowerInvariant(a[n]) == char.ToLowerInvariant(b[n])) n++;
        return n;
    }

    static void ScanText(string data, GameType game, Dictionary<string, SortedSet<string>> result)
    {
        var pair = game == GameType.ASA ? Pair : new Regex(Pair.ToString().Replace("{4}", "{8}"));
        var secOnly = game == GameType.ASA ? SectionOnly : new Regex(SectionOnly.ToString().Replace("{4}", "{8}"));

        var pairs = pair.Matches(data).Select(m => (sec: m.Groups[1].Value, key: m.Groups[2].Value)).ToList();
        var secCount = pairs.GroupBy(p => p.sec).ToDictionary(g => g.Key, g => g.Count());
        var sectionCalls = secOnly.Matches(data).Select(m => (sec: m.Groups[1].Value, pos: m.Index)).ToList();
        foreach (var c in sectionCalls) secCount[c.sec] = secCount.GetValueOrDefault(c.sec) + 1;

        void Add(string s, string k)
        {
            if (!result.TryGetValue(s, out var set)) result[s] = set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            set.Add(k);
        }

        foreach (var (s, k) in pairs)
        {
            if (IsVanilla(s)) continue;
            if (!k.StartsWith(s, StringComparison.OrdinalIgnoreCase) && CommonPrefix(s, k) >= 5) continue;
            if (k.Length < 10 && secCount[s] < 2) continue;
            Add(s, k);
        }

        var loopSecs = secCount.Where(x => x.Value >= 2 && !result.ContainsKey(x.Key) && !IsVanilla(x.Key)).Select(x => x.Key).ToList();
        if (loopSecs.Count == 0) return;
        var consts = Const.Matches(data).Select(m => (start: m.Index, end: m.Index + m.Length, val: m.Groups[1].Value)).ToList();
        var chains = new List<List<(int start, int end, string val)>>();
        var cur = new List<(int start, int end, string val)>();
        foreach (var c in consts)
        {
            if (cur.Count > 0 && c.start - cur[^1].end > 24)
            {
                if (cur.Count >= 3) chains.Add(cur);
                cur = new();
            }
            cur.Add(c);
        }
        if (cur.Count >= 3) chains.Add(cur);
        foreach (var s in loopSecs)
        {
            var calls = sectionCalls.Where(c => c.sec == s).Select(c => c.pos).ToList();
            foreach (var ch in chains.Where(ch => calls.Any(p => Math.Abs(ch[0].start - p) < 20000)))
                foreach (var c in ch)
                    if (!c.val.Equals(s, StringComparison.OrdinalIgnoreCase)) Add(s, c.val);
        }
    }

    public static string? DetectMapName(string modFolder, GameType game, string? oodleDll)
    {
        if (game == GameType.ASE)
            return Directory.GetFiles(modFolder, "*.umap", SearchOption.AllDirectories)
                .Select(Path.GetFileNameWithoutExtension).OfType<string>()
                .OrderBy(n => n.Length).FirstOrDefault();

        var umaps = Directory.GetFiles(modFolder, "*.utoc", SearchOption.AllDirectories)
            .SelectMany(u => Regex.Matches(Latin1.GetString(File.ReadAllBytes(u)), @"([A-Za-z0-9_\-]+)\.umap").Select(m => m.Groups[1].Value))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (umaps.Count > 0)
            return umaps.Where(n => n.EndsWith("_WP", StringComparison.OrdinalIgnoreCase)).OrderBy(n => n.Length).FirstOrDefault()
                   ?? umaps.OrderBy(n => n.Length).First();

        var dll = IoStore.FindOodle(oodleDll);
        if (dll == null) return null;
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in Directory.GetFiles(modFolder, "*.utoc", SearchOption.AllDirectories))
        {
            var text = Latin1.GetString(IoStore.Unpack(u, dll));
            foreach (Match m in Regex.Matches(text, @"/([A-Za-z0-9]+(?:_[A-Za-z0-9]+)*_WP)(?=[^a-z0-9_])"))
                counts[m.Groups[1].Value] = counts.GetValueOrDefault(m.Groups[1].Value) + 1;
        }
        return counts.OrderByDescending(x => x.Value).Select(x => x.Key).FirstOrDefault();
    }

    public static SType GuessType(string key) =>
        Regex.IsMatch(key, @"^(b[A-Z]|Allow|Disable|Enable|Prevent|Use|Is|Can|Should|Debug|Force|Restrict|Respect|No[A-Z]|Hide|Show)")
        && !Regex.IsMatch(key, @"(Level|Multiplier|Range|Cooldown|Duration|Time|Amount|Count|Num|Max|Min|Rate|Weight|Speed|Interval|Radius|Distance|Limit|Percent|Seconds|Scale)$")
            ? SType.Bool : SType.Text;
}
