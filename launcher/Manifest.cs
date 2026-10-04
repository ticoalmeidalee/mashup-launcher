using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

class ManifestException : Exception
{
    public ManifestException(string message) : base(message) { }
}

class HostSpec
{
    public string Name, Exe, Process, Stub, Launch, OfflineArgs, AntiCheatNote;
    public int SteamAppId;
}

class GuestSpec
{
    public string Name, Kind, Minecraft, FabricLoader, FabricApi;
    public int LinkPort;
}

class Step
{
    public string Kind, Url, From, To, Sha256, Pattern, Jar;
    public bool Page;
    public Dictionary<string, string> Extract;
}

/// <summary>
/// A mashup, read from &lt;folder&gt;\mashup.json (format 1). Every path in it is relative and is checked to stay inside
/// its root (the mashup folder for sources, the game folder for targets), so a manifest can never write elsewhere.
/// </summary>
class Manifest
{
    public const int CurrentFormat = 1;
    static readonly string[] Kinds = { "copy", "download", "minecraft-profile" };
    static readonly Regex IdPattern = new Regex("^[a-z0-9][a-z0-9-]{0,40}$");

    public int Format;
    public string Dir, Id, Name, Version, Tagline, Description, Hint, License;
    public string[] Authors = new string[0];
    public Color Accent = Color.FromArgb(61, 220, 132), Accent2 = Color.FromArgb(40, 90, 200);
    public HostSpec Host = new HostSpec();
    public GuestSpec Guest; // null: no guest game
    public List<Step> Install = new List<Step>();

    public static Manifest Load(string dir)
    {
        string file = Path.Combine(dir, "mashup.json");
        if (!File.Exists(file))
            throw new ManifestException("No mashup.json in " + dir);
        object root;
        try { root = Json.Parse(File.ReadAllText(file)); }
        catch (Exception e) { throw new ManifestException("mashup.json is not valid JSON: " + e.Message); }

        var m = new Manifest
        {
            Dir = dir,
            Format = Json.Int(root, "format"),
            Id = Json.Str(root, "id", ""),
            Name = Json.Str(root, "name", ""),
            Version = Json.Str(root, "version", "0.0.0"),
            Tagline = Json.Str(root, "tagline", ""),
            Description = Json.Str(root, "description", ""),
            Hint = Json.Str(root, "hint", ""),
            License = Json.Str(root, "license", ""),
        };
        if (m.Format < 1 || m.Format > CurrentFormat)
            throw new ManifestException("Unsupported mashup format " + m.Format + " (this launcher reads up to " + CurrentFormat + ")");
        if (!IdPattern.IsMatch(m.Id))
            throw new ManifestException("Bad id '" + m.Id + "': lowercase letters, digits and dashes only");
        if (m.Name.Length == 0)
            throw new ManifestException("Missing name");
        m.Authors = (Json.Arr(root, "authors") ?? new object[0]).Cast<object>().Select(a => a.ToString()).ToArray();
        m.Accent = ColorOf(Json.Arr(root, "accent")) ?? m.Accent;
        m.Accent2 = ColorOf(Json.Arr(root, "accent2")) ?? m.Accent2;

        var host = Json.Obj(root, "host") ?? throw new ManifestException("Missing host");
        var anti = Json.Obj(host, "antiCheat");
        m.Host = new HostSpec
        {
            Name = Json.Str(host, "name", "the game"),
            Exe = Json.Str(host, "exe", ""),
            Process = Json.Str(host, "process", ""),
            Stub = Json.Str(host, "stub", ""),
            Launch = Json.Str(host, "launch", ""),
            SteamAppId = Json.Int(host, "steamAppId"),
            OfflineArgs = Json.Str(anti, "offlineArgs"),
            AntiCheatNote = Json.Str(anti, "note"),
        };
        if (m.Host.Exe.Length == 0)
            throw new ManifestException("host.exe is required: it is how the launcher recognises the game folder");
        SafeRelative(m.Host.Exe);

        var guest = Json.Obj(root, "guest");
        if (guest != null)
            m.Guest = new GuestSpec
            {
                Name = Json.Str(guest, "name", "the guest"),
                Kind = Json.Str(guest, "kind", ""),
                Minecraft = Json.Str(guest, "minecraft"),
                FabricLoader = Json.Str(guest, "fabricLoader"),
                FabricApi = Json.Str(guest, "fabricApi"),
                LinkPort = Json.Int(guest, "linkPort"),
            };

        foreach (object o in Json.Arr(root, "install") ?? new object[0])
            m.Install.Add(ParseStep(o));
        return m;
    }

    static Step ParseStep(object o)
    {
        var s = new Step
        {
            Kind = Json.Str(o, "kind", ""),
            Url = Json.Str(o, "url"),
            From = Json.Str(o, "from"),
            To = Json.Str(o, "to", "game"),
            Sha256 = Json.Str(o, "sha256"),
            Pattern = Json.Str(o, "pattern"),
            Jar = Json.Str(o, "jar"),
            Page = Json.Bool(o, "page"),
        };
        if (!Kinds.Contains(s.Kind))
            throw new ManifestException("Unknown install step '" + s.Kind + "' (known: " + string.Join(", ", Kinds) + ")");
        if (s.To != "game")
            throw new ManifestException("Install steps can only target \"game\" (got \"" + s.To + "\")");
        if (s.From != null) SafeRelative(s.From);
        if (s.Jar != null) SafeRelative(s.Jar);
        if (s.Kind == "copy" && s.From == null)
            throw new ManifestException("A copy step needs \"from\"");
        if (s.Kind == "download")
        {
            if (s.Url == null || !(s.Url.StartsWith("https://") || s.Url.StartsWith("file://")))
                throw new ManifestException("A download step needs an https:// url");
            if (s.Page && s.Pattern == null)
                throw new ManifestException("A page download needs a \"pattern\" for the file link");
        }
        var extract = Json.Obj(o, "extract");
        if (extract != null)
        {
            s.Extract = new Dictionary<string, string>();
            foreach (var kv in extract)
                s.Extract[kv.Key] = SafeRelative(kv.Value?.ToString() ?? "");
        }
        return s;
    }

    /// <summary>A relative path that stays inside its root, or a ManifestException.</summary>
    public static string SafeRelative(string p)
    {
        if (string.IsNullOrWhiteSpace(p) || Path.IsPathRooted(p) || p.Contains(":") ||
            p.Split('\\', '/').Any(part => part == ".."))
            throw new ManifestException("Path '" + p + "' must be relative and stay inside its folder");
        return p.Replace('/', '\\');
    }

    static Color? ColorOf(IList rgb) =>
        rgb != null && rgb.Count == 3 ? Color.FromArgb(Clamp(rgb[0]), Clamp(rgb[1]), Clamp(rgb[2])) : (Color?)null;

    static int Clamp(object v) => Math.Max(0, Math.Min(255, Convert.ToInt32(v)));
}
