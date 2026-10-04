using System;
using System.IO;
using System.Linq;

/// <summary>Tests for the final review's findings (each failed before its fix).</summary>
static partial class Tests
{
    static void ReviewTests()
    {
        Case("off_uses_the_folder_on_wrote_to", () => WithTempRoot(() =>
        {
            string a = ChosenGame();
            string aBefore = Snapshot(a);
            var e = new Engine(CopyOnlyMashup());
            e.TurnOn(Quiet, new[] { e });
            // the chosen folder changes while ON (another copy of the game, holding another mod's files)
            string b = TempDir();
            File.WriteAllText(Path.Combine(b, "Game.exe"), "");
            File.WriteAllText(Path.Combine(b, "mod.asi"), "another mod's file");
            string bBefore = Snapshot(b);
            GameLocatorForceChoose("t", b);
            e.TurnOff(Quiet);
            Check(Snapshot(b) == bBefore && Snapshot(a) == aBefore && e.State() == FileState.Off, "off_uses_the_folder_on_wrote_to");
        }));

        Case("choose_refused_while_on", () => WithTempRoot(() =>
        {
            ChosenGame();
            var e = new Engine(CopyOnlyMashup());
            e.TurnOn(Quiet, new[] { e });
            string b = TempDir();
            File.WriteAllText(Path.Combine(b, "Game.exe"), "");
            string message = null;
            try { e.ChooseGameFolder(b); } catch (InvalidOperationException ex) { message = ex.Message; }
            e.TurnOff(Quiet);
            Check(message != null && message.Contains("OFF"), "choose_refused_while_on");
        }));
    }

    /// <summary>Writes the choice file directly, the way a folder change outside the launcher's checks would.</summary>
    static void GameLocatorForceChoose(string id, string dir) =>
        File.WriteAllText(Path.Combine(Paths.Data, id, "game-folder.txt"), dir);
}
