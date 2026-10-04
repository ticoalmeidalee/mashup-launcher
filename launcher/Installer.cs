using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// What a mashup added to a game folder: data\&lt;id&gt;\installed.json. Saved after every change, so whatever happens
/// mid-install (crash, kill, power cut) the record on disk covers everything that is in the game folder.
/// </summary>
class InstallRecord
{
    public bool Complete;
    public string GameDir;                              // the folder ON wrote to: OFF works on this one, whatever is chosen later
    public List<string> Added = new List<string>();    // files written into the game folder (relative)
    public List<string> Dirs = new List<string>();     // folders created for them, outermost first
    public List<string> BackedUp = new List<string>(); // originals copied to data\backups\<id> before being replaced
    public List<string> Runtime = new List<string>();  // files the mod may write while running that weren't there at ON
    string file;

    public static string FileFor(string mashupId) => Path.Combine(Paths.Data, mashupId, "installed.json");
    public static bool Exists(string mashupId) => File.Exists(FileFor(mashupId)) || File.Exists(FileFor(mashupId) + ".bak");

    public static InstallRecord Load(string mashupId)
    {
        var r = new InstallRecord { file = FileFor(mashupId) };
        if (!File.Exists(r.file) && !File.Exists(r.file + ".bak"))
            return r;
        // a crash or power cut can leave the file empty or garbled: the previous version (.bak) is used then
        object o = SafeFile.ReadWithFallback(r.file, text => Json.Parse(text) as IDictionary<string, object>)
            ?? throw new InvalidDataException("The install record " + r.file + " is unreadable. Restore it from " + r.file + ".bak or remove the mashup's files by hand.");
        r.Complete = Json.Bool(o, "complete");
        r.GameDir = Json.Str(o, "gameDir");
        r.Added = Strings(Json.Arr(o, "added"));
        r.Dirs = Strings(Json.Arr(o, "dirs"));
        r.BackedUp = Strings(Json.Arr(o, "backedUp"));
        r.Runtime = Strings(Json.Arr(o, "runtime"));
        return r;
    }

    static List<string> Strings(IList l) => (l ?? new object[0]).Cast<object>().Select(x => x.ToString()).ToList();

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        SafeFile.WriteAllText(file, Json.Write(new Dictionary<string, object>
        {
            ["complete"] = Complete, ["gameDir"] = GameDir, ["added"] = Added, ["dirs"] = Dirs, ["backedUp"] = BackedUp, ["runtime"] = Runtime,
        }));
    }

    public void Delete() => SafeFile.Delete(file);
}

static class Installer
{
    /// <summary>The absolute path of rel inside root, or a ManifestException if it would leave root.</summary>
    public static string Inside(string root, string rel)
    {
        rel = Manifest.SafeRelative(rel);
        string full = Path.GetFullPath(Path.Combine(root, rel));
        string r = Path.GetFullPath(root).TrimEnd('\\') + "\\";
        if (!full.StartsWith(r, StringComparison.OrdinalIgnoreCase))
            throw new ManifestException("Path '" + rel + "' leaves " + root);
        return full;
    }

    /// <summary>The record, tied to gameDir: the first write records the folder, and a write to any other folder is refused.</summary>
    static InstallRecord Claim(string mashupId, string gameDir)
    {
        var rec = InstallRecord.Load(mashupId);
        string dir = Path.GetFullPath(gameDir).TrimEnd('\\');
        if (rec.GameDir == null) rec.GameDir = dir;
        else if (!string.Equals(rec.GameDir, dir, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This mashup is installed in " + rec.GameDir + ". Turn it OFF there first.");
        return rec;
    }

    /// <summary>The game folder an install (finished or not) wrote to, or null when nothing is installed.</summary>
    public static string RecordedGameDir(string mashupId) =>
        InstallRecord.Exists(mashupId) ? InstallRecord.Load(mashupId).GameDir : null;

    static string BackupPath(string mashupId, string rel) => Path.Combine(Paths.Backups, mashupId, rel);

    /// <summary>
    /// Adds one file to the game folder. writeTo gets a temp path to write the content to; the file only replaces
    /// the target once it is complete. An existing target is backed up first and restored by RemoveAll.
    /// </summary>
    public static void AddFile(string mashupId, string gameDir, string relTarget, Action<string> writeTo)
    {
        string rel = Manifest.SafeRelative(relTarget);
        string full = Inside(gameDir, rel);
        var rec = Claim(mashupId, gameDir);
        rec.Complete = false;

        // folders it needs, outermost first, recorded before they exist
        var missing = new List<string>();
        for (string d = Path.GetDirectoryName(full); !Directory.Exists(d); d = Path.GetDirectoryName(d))
            missing.Insert(0, d);
        foreach (string d in missing)
        {
            string relDir = d.Substring(Path.GetFullPath(gameDir).TrimEnd('\\').Length + 1);
            if (!rec.Dirs.Contains(relDir)) rec.Dirs.Add(relDir);
            rec.Save();
            Directory.CreateDirectory(d);
        }

        // an original it replaces: copied away and recorded before anything touches it
        if (File.Exists(full) && !rec.Added.Contains(rel, StringComparer.OrdinalIgnoreCase) && !rec.BackedUp.Contains(rel, StringComparer.OrdinalIgnoreCase))
        {
            string backup = BackupPath(mashupId, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            File.Copy(full, backup, true);
            rec.BackedUp.Add(rel);
            rec.Save();
        }

        if (!rec.Added.Contains(rel, StringComparer.OrdinalIgnoreCase)) rec.Added.Add(rel);
        rec.Save();

        string tmp = full + ".mashup-tmp";
        try
        {
            writeTo(tmp);
            if (File.Exists(full)) File.Delete(full);
            File.Move(tmp, full);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    /// <summary>Records which of the mod's runtime files (logs) are absent now: RemoveAll deletes those, never pre-existing ones.</summary>
    public static void TrackRuntime(string mashupId, string gameDir, IEnumerable<string> files)
    {
        var rec = Claim(mashupId, gameDir);
        foreach (string rel in files)
            if (!File.Exists(Inside(gameDir, rel)) && !rec.Runtime.Contains(rel, StringComparer.OrdinalIgnoreCase)) rec.Runtime.Add(rel);
        rec.Save();
    }

    /// <summary>Marks the install complete: State reads On only after this.</summary>
    public static void Finish(string mashupId)
    {
        var rec = InstallRecord.Load(mashupId);
        rec.Complete = true;
        rec.Save();
    }

    /// <summary>Removes everything the record lists, restores the originals, deletes folders it created if empty.</summary>
    public static void RemoveAll(string mashupId, string gameDir)
    {
        var rec = InstallRecord.Load(mashupId);
        foreach (string rel in Enumerable.Reverse(rec.Added))
        {
            string full = Inside(gameDir, rel);
            if (File.Exists(full)) File.Delete(full);
            if (File.Exists(full + ".mashup-tmp")) File.Delete(full + ".mashup-tmp");
        }
        foreach (string rel in rec.Runtime)
        {
            string full = Inside(gameDir, rel);
            if (File.Exists(full)) File.Delete(full);
        }
        foreach (string rel in rec.BackedUp)
        {
            string backup = BackupPath(mashupId, rel);
            if (File.Exists(backup))
            {
                File.Copy(backup, Inside(gameDir, rel), true);
                File.Delete(backup);
            }
        }
        foreach (string rel in Enumerable.Reverse(rec.Dirs))
        {
            string full = Inside(gameDir, rel);
            if (Directory.Exists(full) && !Directory.EnumerateFileSystemEntries(full).Any()) Directory.Delete(full);
        }
        string backups = Path.Combine(Paths.Backups, mashupId);
        if (Directory.Exists(backups) && !Directory.EnumerateFiles(backups, "*", SearchOption.AllDirectories).Any())
            Directory.Delete(backups, true);
        rec.Delete();
    }

    /// <summary>Off: nothing recorded. On: a finished install whose files are all there. Partial: anything else (FIX removes it).</summary>
    public static FileState State(string mashupId, string gameDir)
    {
        if (!InstallRecord.Exists(mashupId))
            return FileState.Off;
        var rec = InstallRecord.Load(mashupId);
        return rec.Complete && rec.Added.Count > 0 && rec.Added.All(rel => File.Exists(Inside(gameDir, rel))) ? FileState.On : FileState.Partial;
    }
}
