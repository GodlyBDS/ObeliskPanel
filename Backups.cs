using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace ObeliskPanel;

public record BackupFile(string Path, DateTime Time)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public long Size => new FileInfo(Path).Length;
}

public static class Backups
{
    const string Stamp = "dd.MM.yyyy_HH.mm.ss";
    static readonly Regex StampRx = new(@"_(\d{2}\.\d{2}\.\d{4}_\d{2}\.\d{2}\.\d{2})\.zip$", RegexOptions.IgnoreCase);

    static string MapName(ServerProfile p) => p.Map;

    public static string UndoDir(ServerProfile p) => Path.Combine(p.SaveDir, "_UndoLastRestore");

    public static List<BackupFile> List(ServerProfile p)
    {
        if (!Directory.Exists(p.SaveDir)) return new();
        return Directory.GetFiles(p.SaveDir, MapName(p) + "_*.zip")
            .Select(f =>
            {
                var m = StampRx.Match(f);
                var t = m.Success && DateTime.TryParseExact(m.Groups[1].Value, Stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                    ? d : File.GetLastWriteTime(f);
                return new BackupFile(f, t);
            })
            .OrderByDescending(b => b.Time)
            .ToList();
    }

    static IEnumerable<string> SaveFiles(ServerProfile p)
    {
        if (!Directory.Exists(p.SaveDir)) yield break;
        var ark = Path.Combine(p.SaveDir, MapName(p) + ".ark");
        if (File.Exists(ark)) yield return ark;
        foreach (var pattern in new[] { "*.arkprofile", "*.arktribe", "*.arktributetribe" })
            foreach (var f in Directory.GetFiles(p.SaveDir, pattern)) yield return f;
    }

    static FileStream OpenShared(string f) => new(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    public static string Create(ServerProfile p, AppSettings s, Action<string> log)
    {
        var files = SaveFiles(p).ToList();
        if (files.Count == 0) throw new InvalidOperationException($"No save files found in {p.SaveDir}.");
        var zipPath = Path.Combine(p.SaveDir, $"{MapName(p)}_{DateTime.Now.ToString(Stamp, CultureInfo.InvariantCulture)}.zip");
        var tmp = zipPath + ".tmp";
        using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            foreach (var f in files)
            {
                var e = zip.CreateEntry(Path.GetFileName(f), CompressionLevel.Optimal);
                using var src = OpenShared(f);
                using var dst = e.Open();
                src.CopyTo(dst);
            }
            if (p.BackupIncludeCluster)
            {
                var cluster = s.GetClusterDir(p.Game);
                if (Directory.Exists(cluster))
                    foreach (var f in Directory.GetFiles(cluster, "*", SearchOption.AllDirectories))
                    {
                        var e = zip.CreateEntry("clusters/" + Path.GetRelativePath(cluster, f).Replace('\\', '/'), CompressionLevel.Optimal);
                        using var src = OpenShared(f);
                        using var dst = e.Open();
                        src.CopyTo(dst);
                    }
            }
        }
        File.Move(tmp, zipPath, true);
        log($"[{p.Name}] Backup created: {Path.GetFileName(zipPath)} ({files.Count} files{(p.BackupIncludeCluster ? " + cluster" : "")})");
        Prune(p, log);
        return zipPath;
    }

    public static void Prune(ServerProfile p, Action<string> log)
    {
        if (p.MaxBackups <= 0) return;
        foreach (var old in List(p).Skip(p.MaxBackups))
        {
            try { File.Delete(old.Path); log($"[{p.Name}] Old backup removed: {old.Name}"); }
            catch (Exception ex) { log($"[{p.Name}] Could not remove {old.Name}: {ex.Message}"); }
        }
    }

    public static void Restore(ServerProfile p, AppSettings s, BackupFile b, Action<string> log)
    {
        var undo = UndoDir(p);
        if (Directory.Exists(undo)) Directory.Delete(undo, true);
        Directory.CreateDirectory(undo);
        foreach (var f in SaveFiles(p)) File.Copy(f, Path.Combine(undo, Path.GetFileName(f)), true);

        using var zip = ZipFile.OpenRead(b.Path);
        foreach (var e in zip.Entries.Where(e => e.Name != ""))
        {
            string target;
            if (e.FullName.StartsWith("clusters/", StringComparison.OrdinalIgnoreCase))
                target = Path.Combine(s.GetClusterDir(p.Game), e.FullName["clusters/".Length..].Replace('/', '\\'));
            else
                target = Path.Combine(p.SaveDir, e.Name);
            var full = Path.GetFullPath(target);
            if (!full.StartsWith(Path.GetFullPath(p.SaveDir), StringComparison.OrdinalIgnoreCase) &&
                !full.StartsWith(Path.GetFullPath(s.GetClusterDir(p.Game)), StringComparison.OrdinalIgnoreCase))
                continue;
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            e.ExtractToFile(full, true);
        }
        log($"[{p.Name}] Backup restored: {b.Name}. The previous save was kept for 'Undo Last Restore'.");
    }

    public static bool CanUndo(ServerProfile p) => Directory.Exists(UndoDir(p)) && Directory.GetFiles(UndoDir(p)).Length > 0;

    public static void UndoRestore(ServerProfile p, Action<string> log)
    {
        var undo = UndoDir(p);
        if (!CanUndo(p)) throw new InvalidOperationException("Nothing to undo.");
        foreach (var f in Directory.GetFiles(undo)) File.Copy(f, Path.Combine(p.SaveDir, Path.GetFileName(f)), true);
        Directory.Delete(undo, true);
        log($"[{p.Name}] Last restore undone.");
    }

    public static void Wipe(ServerProfile p, Action<string> log)
    {
        var ark = Path.Combine(p.SaveDir, MapName(p) + ".ark");
        if (File.Exists(ark)) File.Delete(ark);
        int n = 1;
        if (p.WipePlayers)
            foreach (var pattern in new[] { "*.arkprofile", "*.arktribe", "*.arktributetribe", "*.profilebak", "*.tribebak" })
                foreach (var f in Directory.GetFiles(p.SaveDir, pattern)) { File.Delete(f); n++; }
        log($"[{p.Name}] Server wiped ({(p.WipePlayers ? "world, players and tribes" : "world only")}, {n} file(s) deleted).");
    }
}
