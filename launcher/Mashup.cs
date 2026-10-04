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

/// <summary>One mashup, read from mashups\&lt;id&gt;\mashup.ini (key=value lines; paths relative to the launcher folder).</summary>
class Mashup
{
    public string Id, Dir, Name, Tagline, Description, Hint, HostName, GuestName, GameDir, Script;
    public string HostProcess, HostStub, HostLaunch, GuestDir, GuestCommand;
    public string[] Files = new string[0];
    public Dictionary<string, string> GuestEnv = new Dictionary<string, string>();
    public int GuestPort;
    public Color Accent = Color.FromArgb(61, 220, 132), Accent2 = Color.FromArgb(40, 90, 200);
    public Image Cover;

    public static List<Mashup> LoadAll()
    {
        var list = new List<Mashup>();
        if (!Directory.Exists(Paths.Mashups))
            return list;
        foreach (string dir in Directory.GetDirectories(Paths.Mashups).OrderBy(d => d))
        {
            string ini = Path.Combine(dir, "mashup.ini");
            if (File.Exists(ini))
                list.Add(Load(dir, ini));
        }
        return list;
    }

    static Mashup Load(string dir, string ini)
    {
        var v = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in File.ReadAllLines(ini))
        {
            int eq = line.IndexOf('=');
            if (eq > 0 && !line.TrimStart().StartsWith("#"))
                v[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
        }
        string Get(string k, string d = "") => v.TryGetValue(k, out string s) ? s : d;
        string Abs(string p) => p.Length == 0 || Path.IsPathRooted(p) ? p : Path.Combine(Paths.Root, p);
        var m = new Mashup
        {
            Id = Path.GetFileName(dir), Dir = dir,
            Name = Get("name", Path.GetFileName(dir)), Tagline = Get("tagline"), Description = Get("description"), Hint = Get("hint"),
            HostName = Get("hostName", "the game"), GuestName = Get("guestName", "the guest"),
            GameDir = Get("gameDir"), Script = Abs(Get("script", "mashup.sh")),
            Files = Get("files").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray(),
            HostProcess = Get("hostProcess"), HostStub = Get("hostStub"), HostLaunch = Get("hostLaunch"),
            GuestDir = Abs(Get("guestDir")), GuestCommand = Get("guestCommand"),
            GuestPort = int.TryParse(Get("guestPort", "0"), out int port) ? port : 0,
        };
        foreach (string pair in Get("guestEnv").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            if (eq > 0)
                m.GuestEnv[pair.Substring(0, eq).Trim()] = Abs(pair.Substring(eq + 1).Trim());
        }
        Color? C(string k)
        {
            var p = Get(k).Split(',');
            return p.Length == 3 && p.All(x => int.TryParse(x.Trim(), out _)) ? Color.FromArgb(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2])) : (Color?)null;
        }
        m.Accent = C("accent") ?? m.Accent;
        m.Accent2 = C("accent2") ?? m.Accent2;
        string cover = Path.Combine(dir, Get("cover", "cover.png"));
        if (File.Exists(cover))
            using (var img = Image.FromFile(cover))
                m.Cover = new Bitmap(img); // a copy: the file stays editable while the launcher runs
        return m;
    }
}
