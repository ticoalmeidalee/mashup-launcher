using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>A mashup's per-user switches, in data\&lt;id&gt;\settings.ini (not in the mashup folder, which an update replaces).</summary>
class Settings
{
    public static (string key, string label, bool def)[] All(Manifest m) => new[] {
        ("startGuest", m.Guest == null ? null : "Open " + m.Guest.Name + (m.Guest.Kind == "minecraft-fabric" ? " Launcher" : "") + " when turning ON", true),
        ("startHost", "Start " + m.Host.Name + " when turning ON", true),
        ("autoOff", "Turn OFF automatically when " + m.Host.Name + " closes", true),
    }.Where(s => s.Item2 != null).ToArray();

    readonly Dictionary<string, bool> v = new Dictionary<string, bool>();
    readonly string file; // null: in memory only

    Settings(string file) { this.file = file; }

    public Settings(Manifest m) : this(Path.Combine(Paths.Data, m.Id, "settings.ini"))
    {
        foreach (var s in All(m)) v[s.key] = s.def;
        try
        {
            foreach (var line in File.ReadAllLines(file))
            {
                var kv = line.Split('=');
                if (kv.Length == 2 && v.ContainsKey(kv[0].Trim())) v[kv[0].Trim()] = kv[1].Trim() == "1";
            }
        }
        catch (IOException) { } // no file yet: defaults
    }

    /// <summary>Settings that are never saved (headless runs, tests).</summary>
    public static Settings Defaults(bool startGuest = true, bool startHost = true, bool autoOff = true)
    {
        var s = new Settings((string)null);
        s.v["startGuest"] = startGuest; s.v["startHost"] = startHost; s.v["autoOff"] = autoOff;
        return s;
    }

    public bool this[string k]
    {
        get => v.TryGetValue(k, out bool b) && b;
        set
        {
            v[k] = value;
            if (file == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllLines(file, v.Select(kv => kv.Key + "=" + (kv.Value ? "1" : "0")));
        }
    }
}
