using System.IO;

namespace ObeliskPanel;

public class IniFile
{
    public string FilePath { get; }
    readonly List<string> _lines;

    IniFile(string path, List<string> lines) { FilePath = path; _lines = lines; }

    public static IniFile Load(string path) =>
        new(path, File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>());

    public string Text => string.Join(Environment.NewLine, _lines);

    static string? SecName(string l)
    {
        l = l.Trim();
        return l.Length > 1 && l[0] == '[' && l[^1] == ']' ? l[1..^1] : null;
    }

    int FindSection(string section)
    {
        for (int i = 0; i < _lines.Count; i++)
            if (string.Equals(SecName(_lines[i]), section, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    int SectionEnd(int start)
    {
        int i = start + 1;
        while (i < _lines.Count && SecName(_lines[i]) == null) i++;
        return i;
    }

    int FindKey(string section, string key)
    {
        int s = FindSection(section);
        if (s < 0) return -1;
        int end = SectionEnd(s);
        for (int i = s + 1; i < end; i++)
        {
            var l = _lines[i];
            int eq = l.IndexOf('=');
            if (eq > 0 && l[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    public string? Get(string section, string key)
    {
        int i = FindKey(section, key);
        return i < 0 ? null : _lines[i][(_lines[i].IndexOf('=') + 1)..].Trim();
    }

    public void Set(string section, string key, string value)
    {
        int i = FindKey(section, key);
        if (i >= 0) { _lines[i] = $"{key}={value}"; return; }
        int s = FindSection(section);
        if (s < 0)
        {
            if (_lines.Count > 0 && _lines[^1].Trim() != "") _lines.Add("");
            _lines.Add($"[{section}]");
            _lines.Add($"{key}={value}");
            return;
        }
        int end = SectionEnd(s);
        while (end > s + 1 && _lines[end - 1].Trim() == "") end--;
        _lines.Insert(end, $"{key}={value}");
    }

    public void Remove(string section, string key)
    {
        int i;
        while ((i = FindKey(section, key)) >= 0) _lines.RemoveAt(i);
    }

    public void SetAll(string section, string key, IEnumerable<string> values)
    {
        Remove(section, key);
        var vals = values.ToList();
        if (vals.Count == 0) return;
        Set(section, key, vals[0]);
        int at = FindKey(section, key);
        for (int i = 1; i < vals.Count; i++) _lines.Insert(at + i, $"{key}={vals[i]}");
    }

    public IEnumerable<string> Sections() =>
        _lines.Select(SecName).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase);

    public List<(string key, string value)> Entries(string section)
    {
        var res = new List<(string, string)>();
        int s = FindSection(section);
        if (s < 0) return res;
        int end = SectionEnd(s);
        for (int i = s + 1; i < end; i++)
        {
            var l = _lines[i];
            int eq = l.IndexOf('=');
            if (eq > 0 && !l.TrimStart().StartsWith(';')) res.Add((l[..eq].Trim(), l[(eq + 1)..].Trim()));
        }
        return res;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
        File.WriteAllLines(FilePath, _lines);
    }
}
