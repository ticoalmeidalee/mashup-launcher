using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// A guest of kind "minecraft-fabric", run by the player's own Minecraft Launcher (their account): a Fabric version
/// json, a launcher profile "mashup-&lt;id&gt;" whose game folder is data\&lt;id&gt;\minecraft (its own worlds and mods:
/// Fabric API + the mashup's jar). Everything is downloaded and checked before anything is written, and Remove takes
/// out only what Install added (record: data\&lt;id&gt;\minecraft.json).
/// </summary>
static class MinecraftProfile
{
    public static string DotMinecraft() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");

    static string VersionId(GuestSpec g) => "fabric-loader-" + g.FabricLoader + "-" + g.Minecraft;
    static string ProfileKey(Manifest m) => "mashup-" + m.Id;
    public static string GameDir(Manifest m) => Path.Combine(Paths.Data, m.Id, "minecraft");
    static string RecordFile(Manifest m) => Path.Combine(Paths.Data, m.Id, "minecraft.json");

    public static void Install(Manifest m, byte[] modJar, string dotMinecraft = null, Func<string, byte[]> get = null)
    {
        var g = m.Guest ?? throw new InvalidOperationException(m.Name + " has no Minecraft guest");
        dotMinecraft = dotMinecraft ?? DotMinecraft();
        get = get ?? (url => Downloads.Get(url));
        string profiles = Path.Combine(dotMinecraft, "launcher_profiles.json");
        if (!File.Exists(profiles))
            throw new InvalidOperationException("Minecraft Launcher not found (no " + profiles + "). Install it and run it once.");

        // 1. everything from the network, checked
        string vid = VersionId(g);
        string versionFile = Path.Combine(dotMinecraft, "versions", vid, vid + ".json");
        byte[] versionJson = File.Exists(versionFile) ? null
            : get("https://meta.fabricmc.net/v2/versions/loader/" + Uri.EscapeDataString(g.Minecraft) + "/" + Uri.EscapeDataString(g.FabricLoader) + "/profile/json");
        var api = FabricApiFile(g, get);
        byte[] apiJar = get(api.url);
        if (!Sha512(apiJar).Equals(api.sha512, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Fabric API download does not match Modrinth's SHA-512: refusing it");

        // 2. write: record first, then each piece
        bool createdVersion = versionJson != null;
        File.WriteAllText(Directory.CreateDirectory(Path.GetDirectoryName(RecordFile(m))).FullName + "\\minecraft.json",
            Json.Write(new Dictionary<string, object> { ["versionId"] = vid, ["createdVersion"] = createdVersion }));
        if (createdVersion)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(versionFile));
            File.WriteAllBytes(versionFile, versionJson);
        }
        string mods = Directory.CreateDirectory(Path.Combine(GameDir(m), "mods")).FullName;
        foreach (string old in Directory.GetFiles(mods, "fabric-api-*.jar")) File.Delete(old);
        File.WriteAllBytes(Path.Combine(mods, api.filename), apiJar);
        File.WriteAllBytes(Path.Combine(mods, m.Id + ".jar"), modJar);

        string backup = Path.Combine(Paths.Backups, m.Id, "launcher_profiles.json");
        Directory.CreateDirectory(Path.GetDirectoryName(backup));
        File.Copy(profiles, backup, true);
        var root = (IDictionary<string, object>)Json.Parse(File.ReadAllText(profiles));
        if (!(root.TryGetValue("profiles", out object p) && p is IDictionary<string, object> list))
            root["profiles"] = list = new Dictionary<string, object>();
        string now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.000Z");
        list[ProfileKey(m)] = new Dictionary<string, object>
        {
            ["name"] = m.Name, ["type"] = "custom", ["created"] = now, ["lastUsed"] = now, ["icon"] = "TNT",
            ["lastVersionId"] = vid, ["gameDir"] = GameDir(m), ["javaArgs"] = "-Xmx4G",
        };
        WriteJson(profiles, root);
    }

    public static void Remove(Manifest m, string dotMinecraft = null)
    {
        dotMinecraft = dotMinecraft ?? DotMinecraft();
        string profiles = Path.Combine(dotMinecraft, "launcher_profiles.json");
        if (File.Exists(profiles))
        {
            var root = (IDictionary<string, object>)Json.Parse(File.ReadAllText(profiles));
            if (root.TryGetValue("profiles", out object p) && p is IDictionary<string, object> list && list.Remove(ProfileKey(m)))
                WriteJson(profiles, root);
        }
        string rec = RecordFile(m);
        if (File.Exists(rec))
        {
            object o = Json.Parse(File.ReadAllText(rec));
            string vid = Json.Str(o, "versionId");
            if (Json.Bool(o, "createdVersion") && vid != null)
            {
                string vdir = Path.Combine(dotMinecraft, "versions", Manifest.SafeRelative(vid));
                if (Directory.Exists(vdir)) Directory.Delete(vdir, true);
            }
            File.Delete(rec);
        }
        string backup = Path.Combine(Paths.Backups, m.Id, "launcher_profiles.json");
        if (File.Exists(backup)) File.Delete(backup);
    }

    public static bool Installed(Manifest m) => File.Exists(RecordFile(m));

    /// <summary>Opens the player's Minecraft Launcher (Microsoft Store app, else the classic install).</summary>
    public static void OpenLauncher()
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", @"shell:AppsFolder\Microsoft.4297127D64EC6_8wekyb3d8bbwe!Minecraft")); return; }
        catch (Exception) { }
        string classic = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Minecraft Launcher", "MinecraftLauncher.exe");
        if (File.Exists(classic)) Process.Start(classic);
    }

    static (string url, string filename, string sha512) FabricApiFile(GuestSpec g, Func<string, byte[]> get)
    {
        string url = "https://api.modrinth.com/v2/project/fabric-api/version?game_versions=" + Uri.EscapeDataString("[\"" + g.Minecraft + "\"]")
            + "&loaders=" + Uri.EscapeDataString("[\"fabric\"]");
        foreach (object v in (IList)Json.Parse(Encoding.UTF8.GetString(get(url))) ?? new object[0])
        {
            if (Json.Str(v, "version_number") != g.FabricApi)
                continue;
            foreach (object f in Json.Arr(v, "files") ?? new object[0])
                if (Json.Str(f, "filename", "").EndsWith(".jar"))
                    return (Json.Str(f, "url"), Manifest.SafeRelative(Json.Str(f, "filename")), Json.Str(Json.Obj(f, "hashes"), "sha512", ""));
        }
        throw new InvalidDataException("Fabric API " + g.FabricApi + " for Minecraft " + g.Minecraft + " is not on Modrinth");
    }

    static string Sha512(byte[] b) { using (var s = SHA512.Create()) return string.Concat(s.ComputeHash(b).Select(x => x.ToString("x2"))); }

    static void WriteJson(string path, object value) => SafeFile.WriteAllText(path, Json.Write(value));
}
