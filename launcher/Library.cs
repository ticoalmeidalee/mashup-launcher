using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>The mashups on this PC (mashups\&lt;id&gt;\mashup.json) and, from Task 8, the reviewed public index.</summary>
static partial class Library
{
    /// <summary>Every valid local mashup, by name. Folders whose mashup.json fails validation are skipped (see Problems).</summary>
    public static List<Manifest> Local()
    {
        Problems.Clear();
        var list = new List<Manifest>();
        if (!Directory.Exists(Paths.Mashups))
            return list;
        foreach (string dir in Directory.GetDirectories(Paths.Mashups).Where(d => !Path.GetFileName(d).StartsWith(".")))
        {
            if (!File.Exists(Path.Combine(dir, "mashup.json")))
                continue;
            try { list.Add(Manifest.Load(dir)); }
            catch (ManifestException e) { Problems.Add(Path.GetFileName(dir) + ": " + e.Message); }
        }
        return list.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Why folders were skipped by the last Local().</summary>
    public static readonly List<string> Problems = new List<string>();
}
