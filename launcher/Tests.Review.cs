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

    static void RecordDurabilityTests()
    {
        Case("empty_record_falls_back_to_previous", () => WithTempRoot(() =>
        {
            string game = ChosenGame();
            string before = Snapshot(game);
            var e = new Engine(CopyOnlyMashup());
            e.TurnOn(Quiet, new[] { e });
            File.WriteAllText(InstallRecord.FileFor("t"), ""); // what a power cut can leave behind
            bool sawOn = e.State() == FileState.On;
            e.TurnOff(Quiet);
            Check(sawOn && Snapshot(game) == before, "empty_record_falls_back_to_previous");
        }));

        Case("garbled_record_falls_back_to_previous", () => WithTempRoot(() =>
        {
            string game = ChosenGame();
            string before = Snapshot(game);
            var e = new Engine(CopyOnlyMashup());
            e.TurnOn(Quiet, new[] { e });
            File.WriteAllBytes(InstallRecord.FileFor("t"), new byte[64]); // NULs
            e.TurnOff(Quiet);
            Check(Snapshot(game) == before, "garbled_record_falls_back_to_previous");
        }));
    }

    /// <summary>Writes the choice file directly, the way a folder change outside the launcher's checks would.</summary>
    static void GameLocatorForceChoose(string id, string dir) =>
        File.WriteAllText(Path.Combine(Paths.Data, id, "game-folder.txt"), dir);
}
