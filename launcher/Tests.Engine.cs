using System;
using System.IO;
using System.Linq;

static partial class Tests
{
    /// <summary>A mashup folder whose only step copies payload/game (one file + one nested file) into the game.</summary>
    static Manifest CopyOnlyMashup(string extraHostJson = "")
    {
        string dir = WithManifest(MinimalManifest.Replace(@"""process"": ""Game"" }", @"""process"": ""Game""" + extraHostJson + " }"));
        Directory.CreateDirectory(Path.Combine(dir, @"payload\game\sub"));
        File.WriteAllText(Path.Combine(dir, @"payload\game\mod.asi"), "mod");
        File.WriteAllText(Path.Combine(dir, @"payload\game\sub\mod.ini"), "cfg");
        return Manifest.Load(dir);
    }

    /// <summary>A fake game folder (Game.exe) chosen for mashup "t".</summary>
    static string ChosenGame()
    {
        string game = TempDir();
        File.WriteAllText(Path.Combine(game, "Game.exe"), "");
        GameLocator.Choose("t", game, "Game.exe");
        return game;
    }

    static readonly Settings Quiet = Settings.Defaults(startGuest: false, startHost: false);

    static void EngineTests()
    {
        Case("on_off_roundtrip_copy_only", () => WithTempRoot(() =>
        {
            string game = ChosenGame();
            string before = Snapshot(game);
            var e = new Engine(CopyOnlyMashup());
            e.TurnOn(Quiet, new[] { e });
            bool on = e.State() == FileState.On && File.ReadAllText(Path.Combine(game, @"sub\mod.ini")) == "cfg";
            e.TurnOff(Quiet);
            Check(on && e.State() == FileState.Off && Snapshot(game) == before, "on_off_roundtrip_copy_only");
        }));

        Case("anticheat_refused_without_offline_args", () => WithTempRoot(() =>
        {
            string game = ChosenGame();
            Directory.CreateDirectory(Path.Combine(game, "BattlEye"));
            string before = Snapshot(game);
            var e = new Engine(CopyOnlyMashup());
            string message = null;
            try { e.TurnOn(Quiet, new[] { e }); } catch (InvalidOperationException ex) { message = ex.Message; }
            Check(message != null && message.Contains("anti-cheat") && Snapshot(game) == before, "anticheat_refused_without_offline_args");
        }));

        Case("anticheat_allowed_with_offline_args", () => WithTempRoot(() =>
        {
            string game = ChosenGame();
            Directory.CreateDirectory(Path.Combine(game, "EasyAntiCheat"));
            var e = new Engine(CopyOnlyMashup(@", ""antiCheat"": { ""offlineArgs"": ""-offline"", ""note"": ""Story only"" }"));
            var plan = e.Plan();
            e.TurnOn(Quiet, new[] { e });
            bool on = e.State() == FileState.On;
            e.TurnOff(Quiet);
            Check(plan.AntiCheatFound && plan.AntiCheatNote == "Story only" && on, "anticheat_allowed_with_offline_args");
        }));

        Case("plan_lists_files", () => WithTempRoot(() =>
        {
            ChosenGame();
            var plan = new Engine(CopyOnlyMashup()).Plan();
            Check(plan.Files.Contains("mod.asi") && plan.Files.Contains(@"sub\mod.ini") && !plan.Reviewed, "plan_lists_files");
        }));

        Case("failed_step_rolls_back", () => WithTempRoot(() =>
        {
            string game = ChosenGame();
            string before = Snapshot(game);
            var m = CopyOnlyMashup();
            m.Install.Add(new Step { Kind = "download", Url = FileUrl(Path.Combine(TempDir(), "missing.zip")), To = "game" });
            var e = new Engine(m);
            bool threw = false;
            try { e.TurnOn(Quiet, new[] { e }); } catch (Exception) { threw = true; }
            Check(threw && e.State() == FileState.Off && Snapshot(game) == before, "failed_step_rolls_back");
        }));

        Case("no_game_folder_explains", () => WithTempRoot(() =>
        {
            var e = new Engine(CopyOnlyMashup());
            string message = null;
            try { e.TurnOn(Quiet, new[] { e }); } catch (InvalidOperationException ex) { message = ex.Message; }
            Check(message != null && message.Contains("Game.exe"), "no_game_folder_explains");
        }));
    }
}
