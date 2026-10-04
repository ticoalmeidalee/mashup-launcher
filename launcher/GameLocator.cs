using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

/// <summary>
/// Where a mashup's host game lives: the folder the user chose (data\&lt;id&gt;\game-folder.txt), else the Steam install found
/// from the manifest's steamAppId. A folder only counts if the manifest's host exe is in it.
/// </summary>
static class GameLocator
{
    public static string SteamRoot()
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                if (k?.GetValue("SteamPath") is string p && Directory.Exists(p))
                    return Path.GetFullPath(p);
        }
        catch (Exception) { }
        string def = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        return Directory.Exists(def) ? def : null;
    }

    /// <summary>Every Steam library folder: the Steam folder itself and each "path" in libraryfolders.vdf.</summary>
    public static IEnumerable<string> SteamLibraries(string steamRoot = null)
    {
        steamRoot = steamRoot ?? SteamRoot();
        if (steamRoot == null)
            yield break;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(Path.Combine(steamRoot, "steamapps")) && seen.Add(Path.GetFullPath(steamRoot)))
            yield return Path.GetFullPath(steamRoot);
        string vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf))
            yield break;
        foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
        {
            string lib = m.Groups[1].Value.Replace(@"\\", @"\");
            if (Directory.Exists(lib) && seen.Add(Path.GetFullPath(lib)))
                yield return Path.GetFullPath(lib);
        }
    }

    /// <summary>The install folder of a Steam app (from its appmanifest), or null.</summary>
    public static string FindSteamApp(int appId, string steamRoot = null)
    {
        if (appId <= 0)
            return null;
        foreach (string lib in SteamLibraries(steamRoot))
        {
            string acf = Path.Combine(lib, "steamapps", "appmanifest_" + appId + ".acf");
            if (!File.Exists(acf))
                continue;
            var m = Regex.Match(File.ReadAllText(acf), "\"installdir\"\\s+\"([^\"]+)\"");
            if (!m.Success)
                continue;
            string dir = Path.Combine(lib, "steamapps", "common", m.Groups[1].Value);
            if (Directory.Exists(dir))
                return dir;
        }
        return null;
    }

    public static bool IsGameFolder(string dir, string exe) =>
        !string.IsNullOrEmpty(dir) && Directory.Exists(dir) && File.Exists(Path.Combine(dir, exe));

    static string ChoiceFile(string mashupId) => Path.Combine(Paths.Data, mashupId, "game-folder.txt");

    /// <summary>The folder the user picked for this mashup, or null.</summary>
    public static string Chosen(string mashupId)
    {
        string f = ChoiceFile(mashupId);
        return File.Exists(f) ? File.ReadAllText(f).Trim() : null;
    }

    /// <summary>Remembers dir as this mashup's game folder, or throws if exe isn't in it.</summary>
    public static void Choose(string mashupId, string dir, string exe)
    {
        if (!IsGameFolder(dir, exe))
            throw new InvalidOperationException("That folder doesn't contain " + exe + ". Pick the folder the game's " + exe + " is in.");
        Directory.CreateDirectory(Path.GetDirectoryName(ChoiceFile(mashupId)));
        File.WriteAllText(ChoiceFile(mashupId), Path.GetFullPath(dir));
    }

    /// <summary>The game folder to use: the user's valid choice, else Steam's install of the app, else null (ask the user).</summary>
    public static string Resolve(string mashupId, string exe, int steamAppId)
    {
        string chosen = Chosen(mashupId);
        if (IsGameFolder(chosen, exe))
            return chosen;
        string steam = FindSteamApp(steamAppId);
        return IsGameFolder(steam, exe) ? steam : null;
    }
}
