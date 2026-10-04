using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

class Settings
{
    // key -> (label, default); labels name the mashup's games
    public static (string key, string label, bool def)[] All(Mashup m) => new[] {
        ("startGuest", "Start " + m.GuestName + " automatically", true),
        ("startHost", "Start " + m.HostName + " automatically", true),
        ("autoOff", "Turn OFF automatically when " + m.HostName + " closes", true),
        ("closeGuest", "Close " + m.GuestName + " when turning OFF", true),
    };
    readonly Dictionary<string, bool> v = new Dictionary<string, bool>();
    readonly string file;

    public Settings(Mashup m)
    {
        file = Path.Combine(m.Dir, "settings.ini");
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
    public bool this[string k] { get => v[k]; set { v[k] = value; Save(); } }
    void Save() => File.WriteAllLines(file, v.Select(kv => kv.Key + "=" + (kv.Value ? "1" : "0")));
}
