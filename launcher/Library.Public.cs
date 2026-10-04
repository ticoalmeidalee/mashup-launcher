using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

/// <summary>Installing would replace a mashup the user has from another source: the UI asks, then retries with replace.</summary>
class ReplaceNeedsConfirmation : Exception
{
    public ReplaceNeedsConfirmation(string existing) : base("This replaces " + existing + " in your library.") { }
}

/// <summary>One mashup in the reviewed public index (library/index.json in the repo): where its package is, and its pin.</summary>
class IndexEntry
{
    public string Id, Name, Version, Tagline, Url, Sha256;
    public string[] Authors = new string[0];
}

static partial class Library
{
    public const string DefaultIndex = "https://raw.githubusercontent.com/ticoalmeidalee/mashup-launcher/main/library/index.json";

    /// <summary>The reviewed public mashups. Every entry needs an https/file url and a SHA-256 pin.</summary>
    public static List<IndexEntry> Public(string indexUrl = DefaultIndex)
    {
        object root = Json.Parse(Encoding.UTF8.GetString(Downloads.Get(indexUrl)));
        var list = new List<IndexEntry>();
        foreach (object o in Json.Arr(root, "mashups") ?? new object[0])
        {
            var e = new IndexEntry
            {
                Id = Json.Str(o, "id", ""), Name = Json.Str(o, "name", ""), Version = Json.Str(o, "version", ""),
                Tagline = Json.Str(o, "tagline", ""), Url = Json.Str(o, "url", ""), Sha256 = Json.Str(o, "sha256", ""),
                Authors = (Json.Arr(o, "authors") ?? new object[0]).Cast<object>().Select(a => a.ToString()).ToArray(),
            };
            if (e.Id.Length > 0 && e.Sha256.Length == 64 && Downloads.Allowed(e.Url))
                list.Add(e);
        }
        return list;
    }

    /// <summary>Downloads a public mashup, checks its pin, and installs it as reviewed.</summary>
    public static Manifest InstallPackage(IndexEntry e, bool replace = false) =>
        InstallZip(Downloads.Verified(e.Url, e.Sha256), e.Id, reviewed: true, from: e.Url, replace: replace);

    /// <summary>Installs a mashup zip from this PC: it is marked not reviewed (the trust screen says so).</summary>
    public static Manifest InstallLocalZip(string path, bool replace = false) =>
        InstallZip(File.ReadAllBytes(path), null, reviewed: false, from: Path.GetFullPath(path), replace: replace);

    static Manifest InstallZip(byte[] zip, string expectedId, bool reviewed, string from, bool replace)
    {
        string incoming = Path.Combine(Paths.Mashups, ".incoming", Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(incoming);
        try
        {
            using (var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
                foreach (var entry in archive.Entries)
                {
                    string target = Installer.Inside(incoming, entry.FullName.TrimEnd('/', '\\')); // ManifestException if it leaves
                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\")) { Directory.CreateDirectory(target); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    entry.ExtractToFile(target, true);
                }

            // the package may hold the mashup at its root or inside one top folder
            string root = incoming;
            if (!File.Exists(Path.Combine(root, "mashup.json")))
            {
                var dirs = Directory.GetDirectories(root);
                if (dirs.Length == 1 && File.Exists(Path.Combine(dirs[0], "mashup.json"))) root = dirs[0];
                else throw new ManifestException("The package has no mashup.json");
            }
            var m = Manifest.Load(root);
            if (expectedId != null && m.Id != expectedId)
                throw new ManifestException("The package says it is '" + m.Id + "', the index says '" + expectedId + "'");

            string dest = Path.Combine(Paths.Mashups, m.Id);
            if (Directory.Exists(dest))
            {
                Manifest old = null;
                try { old = Manifest.Load(dest); } catch (ManifestException) { }
                if (old != null && new Engine(old).State() != FileState.Off)
                    throw new InvalidOperationException(old.Name + " is ON. Turn it OFF before updating it.");
                // only a reviewed package updating a reviewed install replaces silently; anything else (your own draft or
                // zip, or a local zip over a reviewed copy) asks first
                bool oldReviewed = old != null && new Engine(old).Reviewed;
                if (!replace && !(reviewed && oldReviewed))
                    throw new ReplaceNeedsConfirmation((old?.Name ?? m.Id) + (oldReviewed ? " (a reviewed public mashup)" : " (your own or a local copy)"));
                Directory.Delete(dest, true);
            }
            File.WriteAllText(Path.Combine(root, "source.json"), Json.Write(new Dictionary<string, object>
            {
                ["reviewed"] = reviewed, ["sha256"] = Downloads.Sha256(zip), ["from"] = from, ["installed"] = DateTime.UtcNow.ToString("u"),
            }));
            Directory.Move(root, dest);
            return Manifest.Load(dest);
        }
        finally
        {
            if (Directory.Exists(incoming)) Directory.Delete(incoming, true);
            string parent = Path.Combine(Paths.Mashups, ".incoming");
            if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
        }
    }
}
