using System.IO;

namespace ObeliskPanel;

public static class ServerFiles
{
    public static string KeptSavesDir(AppSettings s, GameType g) => Path.Combine(s.GetServersDir(g), "Kept Saves");

    static string AsaModsDir(ServerProfile p) => Path.Combine(p.BinDir, "ShooterGame", "Mods", "83374");
    static string AseModsDir(ServerProfile p) => Path.Combine(p.InstallDir, "ShooterGame", "Content", "Mods");

    public static List<string> InstalledModPaths(ServerProfile p)
    {
        var result = new List<string>();
        var ids = p.AllModIds();
        if (p.Game == GameType.ASA)
        {
            var dir = AsaModsDir(p);
            if (Directory.Exists(dir))
                foreach (var d in Directory.GetDirectories(dir))
                {
                    var name = Path.GetFileName(d);
                    if (ids.Any(id => name.StartsWith(id + "_", StringComparison.Ordinal) || name == id)) result.Add(d);
                }
        }
        else
        {
            var dir = AseModsDir(p);
            foreach (var id in ids)
            {
                var d = Path.Combine(dir, id);
                if (Directory.Exists(d)) result.Add(d);
                var f = Path.Combine(dir, id + ".mod");
                if (File.Exists(f)) result.Add(f);
            }
        }
        return result;
    }

    public static int DeleteInstalledMods(ServerProfile p, Action<string> log)
    {
        int n = 0;
        foreach (var path in InstalledModPaths(p))
        {
            try
            {
                if (Directory.Exists(path)) ForceDelete(path); else File.Delete(path);
                n++;
            }
            catch (Exception ex) { log($"[{p.Name}] Could not delete {Path.GetFileName(path)}: {ex.Message}"); }
        }
        return n;
    }

    public static bool LooksLikeServerFolder(ServerProfile p, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(p.InstallDir)) { reason = "No install folder set."; return false; }
        var full = Path.GetFullPath(p.InstallDir).TrimEnd('\\');
        if (Path.GetPathRoot(full)?.TrimEnd('\\') == full) { reason = "The install folder is a drive root."; return false; }
        if (full.Split('\\').Length < 3) { reason = "The install folder is too close to the drive root."; return false; }
        if (!Directory.Exists(full)) { reason = "The install folder does not exist."; return false; }
        if (!Directory.Exists(Path.Combine(full, "ShooterGame"))) { reason = "The folder does not look like an ARK server (no ShooterGame folder)."; return false; }
        return true;
    }

    public static string? MoveSaveAside(ServerProfile p, AppSettings s)
    {
        var saved = Path.Combine(p.InstallDir, "ShooterGame", "Saved");
        if (!Directory.Exists(saved)) return null;
        var target = Path.Combine(KeptSavesDir(s, p.Game), $"{p.Name} {DateTime.Now:yyyy-MM-dd HH-mm}");
        Directory.CreateDirectory(target);
        var dest = Path.Combine(target, "Saved");
        try { Directory.Move(saved, dest); }
        catch (IOException)
        {
            CopyDir(saved, dest);
            ForceDelete(saved);
        }
        File.WriteAllText(Path.Combine(target, "README.txt"),
            $"Savegame of the server '{p.Name}' ({p.Game}, map {p.Map}), kept by ObeliskPanel on {DateTime.Now:yyyy-MM-dd HH:mm}.\r\n" +
            "To use it again: create a new server, click Update once, then copy this 'Saved' folder into\r\n" +
            "<new server>\\Server Files\\ShooterGame\\ (replace the existing Saved folder) before the first start.\r\n");
        return target;
    }

    public static void DeleteServer(ServerProfile p)
    {
        var dir = Path.GetFullPath(p.InstallDir);
        ForceDelete(dir);
        var parent = Path.GetDirectoryName(dir);
        if (parent != null && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
            Directory.Delete(parent);
    }

    static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(from)) CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
    }

    static void ForceDelete(string dir)
    {
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
        }
        Directory.Delete(dir, true);
    }
}
