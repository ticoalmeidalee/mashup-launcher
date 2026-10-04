using System;
using System.IO;
using System.Linq;

static partial class Tests
{
    /// <summary>A fake Steam install: root library + a second library (D:-style) holding GTA V's appmanifest.</summary>
    static (string steam, string lib2) FakeSteam()
    {
        string steam = TempDir(), lib2 = TempDir();
        Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
        Directory.CreateDirectory(Path.Combine(lib2, "steamapps", "common", "Grand Theft Auto V"));
        // libraryfolders.vdf escapes backslashes
        File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"),
            "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"" + steam.Replace("\\", "\\\\") + "\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"" + lib2.Replace("\\", "\\\\") + "\"\n\t}\n}\n");
        File.WriteAllText(Path.Combine(lib2, "steamapps", "appmanifest_271590.acf"),
            "\"AppState\"\n{\n\t\"appid\"\t\t\"271590\"\n\t\"installdir\"\t\t\"Grand Theft Auto V\"\n}\n");
        return (steam, lib2);
    }

    static void GameLocatorTests()
    {
        Case("steam_libraries_parsed", () =>
        {
            var (steam, lib2) = FakeSteam();
            var libs = GameLocator.SteamLibraries(steam).ToList();
            Check(libs.Count == 2 && libs.Contains(steam, StringComparer.OrdinalIgnoreCase) && libs.Contains(lib2, StringComparer.OrdinalIgnoreCase), "steam_libraries_parsed");
        });

        Case("find_app_by_manifest", () =>
        {
            var (steam, lib2) = FakeSteam();
            string found = GameLocator.FindSteamApp(271590, steam);
            Check(string.Equals(found, Path.Combine(lib2, "steamapps", "common", "Grand Theft Auto V"), StringComparison.OrdinalIgnoreCase), "find_app_by_manifest (" + found + ")");
        });

        Case("find_app_missing_is_null", () =>
        {
            var (steam, _) = FakeSteam();
            Check(GameLocator.FindSteamApp(12345, steam) == null, "find_app_missing_is_null");
        });

        Case("locate_rejects_folder_without_exe", () => WithTempRoot(() =>
        {
            string notGame = TempDir();
            string message = null;
            try { GameLocator.Choose("t", notGame, "GTA5.exe"); } catch (InvalidOperationException e) { message = e.Message; }
            Check(message != null && message.Contains("GTA5.exe") && GameLocator.Chosen("t") == null, "locate_rejects_folder_without_exe");
        }));

        Case("chosen_folder_remembered", () => WithTempRoot(() =>
        {
            string game = TempDir();
            File.WriteAllText(Path.Combine(game, "GTA5.exe"), "");
            GameLocator.Choose("t", game, "GTA5.exe");
            Check(string.Equals(GameLocator.Chosen("t"), game, StringComparison.OrdinalIgnoreCase), "chosen_folder_remembered");
        }));
    }
}
