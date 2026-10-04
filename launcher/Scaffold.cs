using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// "Create mashup": a draft mashup folder from two picked game folders. The launcher can't write the mod itself (each
/// engine needs real modding work), so the draft gets a PROMPT.md for universal-modder, the AI toolkit that builds it.
/// </summary>
static class Scaffold
{
    public const string ModderRepo = "https://github.com/rehan-remade/universal-modder";

    public static string Create(string name, string hostDir, string hostExe, string guestDir, string guestExe)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Give the mashup a name.");
        if (!GameLocator.IsGameFolder(hostDir, hostExe)) throw new InvalidOperationException("Pick the host game's folder and its .exe.");
        string baseId = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (baseId.Length == 0) baseId = "mashup";
        if (baseId.Length > 36) baseId = baseId.Substring(0, 36).Trim('-');
        string id = baseId;
        for (int n = 2; Directory.Exists(Path.Combine(Paths.Mashups, id)); ++n) id = baseId + "-" + n;

        string dir = Directory.CreateDirectory(Path.Combine(Paths.Mashups, id)).FullName;
        string hostName = GameName(hostDir), guestName = guestDir != null ? GameName(guestDir) : null;
        var host = new Dictionary<string, object>
        {
            ["name"] = hostName, ["exe"] = hostExe, ["process"] = Path.GetFileNameWithoutExtension(hostExe),
        };
        int appId = GameLocator.SteamAppIdFor(hostDir);
        if (appId > 0) { host["steamAppId"] = appId; host["launch"] = "steam://rungameid/" + appId; }
        if (Engine.AntiCheatPresent(hostDir))
            host["antiCheat"] = new Dictionary<string, object> { ["offlineArgs"] = "", ["note"] = "This game has anti-cheat: say how the mashup keeps it offline, or it won't install." };
        var manifest = new Dictionary<string, object>
        {
            ["format"] = Manifest.CurrentFormat, ["id"] = id, ["name"] = name.Trim(), ["version"] = "0.1.0",
            ["tagline"] = (guestName ?? "Something") + " inside " + hostName, ["description"] = "Draft: being built with universal-modder.",
            ["authors"] = new[] { Environment.UserName }, ["host"] = host, ["install"] = new object[0],
        };
        if (guestDir != null)
            manifest["guest"] = new Dictionary<string, object> { ["name"] = guestName, ["kind"] = "process" };
        File.WriteAllText(Path.Combine(dir, "mashup.json"), Json.Write(manifest));
        File.WriteAllText(Path.Combine(dir, "PROMPT.md"), Prompt(name.Trim(), id, hostName, hostDir, hostExe, guestName, guestDir, guestExe, dir));
        GameLocator.Choose(id, hostDir, hostExe);
        return dir;
    }

    /// <summary>A readable game name from its folder (Steam folders are named after the game).</summary>
    static string GameName(string dir) => Path.GetFileName(Path.GetFullPath(dir).TrimEnd('\\'));

    static string Prompt(string name, string id, string hostName, string hostDir, string hostExe, string guestName, string guestDir, string guestExe, string dir) =>
        "# Build the \"" + name + "\" mashup\n\n" +
        "Use universal-modder (" + ModderRepo + "): start with its `mod-any-game` skill, then `mashup-mods`.\n\n" +
        "- Host game: **" + hostName + "** at `" + hostDir + "` (`" + hostExe + "`)\n" +
        (guestDir != null ? "- Guest game: **" + guestName + "** at `" + guestDir + "` (`" + guestExe + "`)\n" : "- Guest: (decide: another game, or content ported into the host)\n") +
        "- Mashup folder: `" + dir + "`\n\n" +
        "Goal: " + (guestDir != null ? guestName + " playable inside " + hostName : "a mashup inside " + hostName) +
        ". Pick the lightest pattern that delivers it (content port, passthrough, decomp-as-library), following the\n" +
        "GTA V x Minecraft example. Single-player and offline only; never bypass anti-cheat, DRM or ownership checks.\n\n" +
        "Deliver it as a Mashup Launcher package in that folder:\n\n" +
        "1. `payload/game/...`: the files to add to the host game folder (your own builds and configs only).\n" +
        "2. `mashup.json` install steps (format " + Manifest.CurrentFormat + "): `copy` from `payload/game`; `download` steps for third-party files\n" +
        "   from their official sites (with `sha256` where the file is fixed); `minecraft-profile` if the guest is Minecraft.\n" +
        "   Fill `host.process`, `host.launch`, and `host.antiCheat.offlineArgs` if the game has anti-cheat.\n" +
        "3. `README.md` (what it does, controls, credits) and a square `cover.png`.\n\n" +
        "Test with the launcher: turn it ON, play, turn it OFF, and check the game folder is back to stock. Id: `" + id + "`.\n";
}
