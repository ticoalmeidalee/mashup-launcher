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

    static void LaunchAndSourceTests()
    {
        string Host(string launch, int appId = 271590) => MinimalManifest.Replace(@"""process"": ""Game"" }",
            @"""process"": ""Game"", ""steamAppId"": " + appId + @", ""launch"": """ + launch + @""" }");

        Case("manifest_rejects_arbitrary_launch", () =>
        {
            bool abs = Rejects(Host(@"C:\\Windows\\System32\\calc.exe"));
            bool unc = Rejects(Host(@"\\\\server\\share\\x.exe"));
            bool uri = Rejects(Host("ms-settings:"));
            bool otherApp = Rejects(Host("steam://rungameid/12345"));
            bool escape = Rejects(Host(@"..\\evil.exe"));
            Check(abs && unc && uri && otherApp && escape, "manifest_rejects_arbitrary_launch (abs=" + abs + " unc=" + unc + " uri=" + uri + " otherApp=" + otherApp + " escape=" + escape + ")");
        });

        Case("manifest_accepts_steam_or_game_exe_launch", () =>
        {
            var steam = Manifest.Load(WithManifest(Host("steam://rungameid/271590")));
            var exe = Manifest.Load(WithManifest(Host("PlayGame.exe")));
            Check(steam.Host.Launch == "steam://rungameid/271590" && exe.Host.Launch == "PlayGame.exe", "manifest_accepts_steam_or_game_exe_launch");
        });

        Case("file_urls_only_in_tests", () =>
        {
            Downloads.AllowFileUrls = false;
            try
            {
                bool manifest = Rejects(MinimalManifest.Replace(@"{ ""kind"": ""copy"", ""from"": ""payload/game"", ""to"": ""game"" }",
                    @"{ ""kind"": ""download"", ""url"": ""file:///C:/Windows/win.ini"", ""to"": ""game"" }"));
                bool get = false;
                try { Downloads.Get("file:///C:/Windows/win.ini"); } catch (InvalidOperationException) { get = true; }
                Check(manifest && get, "file_urls_only_in_tests (manifest=" + manifest + " get=" + get + ")");
            }
            finally { Downloads.AllowFileUrls = true; }
        });

        Case("local_zip_over_reviewed_needs_confirmation", () => WithTempRoot(() =>
        {
            Library.InstallPackage(Indexed("pkg", Package("pkg")));
            string zip = Path.Combine(TempDir(), "pkg.zip");
            File.WriteAllBytes(zip, Package("pkg"));
            bool asked = false;
            try { Library.InstallLocalZip(zip); } catch (ReplaceNeedsConfirmation) { asked = true; }
            bool stillReviewed = new Engine(Manifest.Load(Path.Combine(Paths.Mashups, "pkg"))).Reviewed;
            Library.InstallLocalZip(zip, replace: true);
            Check(asked && stillReviewed && !new Engine(Manifest.Load(Path.Combine(Paths.Mashups, "pkg"))).Reviewed, "local_zip_over_reviewed_needs_confirmation");
        }));

        Case("public_install_over_own_draft_needs_confirmation", () => WithTempRoot(() =>
        {
            string host = TempDir();
            File.WriteAllText(Path.Combine(host, "Game.exe"), "");
            Scaffold.Create("pkg", host, "Game.exe", null, null); // the user's own draft, id "pkg"
            bool asked = false;
            try { Library.InstallPackage(Indexed("pkg", Package("pkg"))); } catch (ReplaceNeedsConfirmation) { asked = true; }
            Check(asked && File.Exists(Path.Combine(Paths.Mashups, "pkg", "PROMPT.md")), "public_install_over_own_draft_needs_confirmation");
        }));

        Case("plan_shows_pinning_runtime_files_and_launch", () => WithTempRoot(() =>
        {
            ChosenGame();
            string dir = WithManifest(MinimalManifest
                .Replace(@"""process"": ""Game"" }", @"""process"": ""Game"", ""launch"": ""Game.exe"" }")
                .Replace(@"""install"":", @"""runtimeFiles"": [""mod.log""], ""install"":")
                .Replace(@"{ ""kind"": ""copy"", ""from"": ""payload/game"", ""to"": ""game"" }",
                    @"{ ""kind"": ""download"", ""url"": ""https://example.com/a.fxh"", ""sha256"": """ + new string('a', 64) + @""", ""save"": ""a.fxh"", ""to"": ""game"" }," +
                    @"{ ""kind"": ""download"", ""url"": ""https://example.com/page/"", ""page"": true, ""pattern"": ""x"", ""to"": ""game"" }"));
            var e = new Engine(Manifest.Load(dir));
            var p = e.Plan();
            Check(p.Downloads.Count == 2 && p.Downloads[0].pinned && !p.Downloads[1].pinned && p.RuntimeFiles.Contains("mod.log")
                && p.Files.Any(f => f.Contains("named by")) && p.Launch == "Game.exe in the game folder", "plan_shows_pinning_runtime_files_and_launch");
        }));

        Case("reviewed_update_needs_no_confirmation", () => WithTempRoot(() =>
        {
            Library.InstallPackage(Indexed("pkg", Package("pkg")));
            Library.InstallPackage(Indexed("pkg", Package("pkg")));
            Check(new Engine(Manifest.Load(Path.Combine(Paths.Mashups, "pkg"))).Reviewed, "reviewed_update_needs_no_confirmation");
        }));
    }

    static void RetryTests()
    {
        Case("retry_rides_out_a_brief_lock", () =>
        {
            int calls = 0;
            SafeFile.Retry(() => { if (++calls < 3) throw new IOException("in use by the antivirus"); });
            Check(calls == 3, "retry_rides_out_a_brief_lock");
        });

        Case("retry_gives_up_on_a_lasting_lock", () =>
        {
            bool threw = false;
            try { SafeFile.Retry(() => { throw new UnauthorizedAccessException("denied"); }); } catch (UnauthorizedAccessException) { threw = true; }
            Check(threw, "retry_gives_up_on_a_lasting_lock");
        });
    }

    /// <summary>Writes the choice file directly, the way a folder change outside the launcher's checks would.</summary>
    static void GameLocatorForceChoose(string id, string dir) =>
        File.WriteAllText(Path.Combine(Paths.Data, id, "game-folder.txt"), dir);
}
