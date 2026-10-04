using System;
using System.IO;

static partial class Tests
{
    static void PackageFeatureTests()
    {
        Case("download_save_puts_file_at_path", () => WithTempRoot(() =>
        {
            string game = ChosenGame();
            string src = Path.Combine(TempDir(), "ReShade.fxh");
            File.WriteAllText(src, "// header");
            var m = CopyOnlyMashup();
            m.Install.Add(new Step { Kind = "download", Url = FileUrl(src), Save = @"reshade-shaders\Shaders\ReShade.fxh", Sha256 = Downloads.Sha256(File.ReadAllBytes(src)), To = "game" });
            var e = new Engine(m);
            bool planned = e.Plan().Files.Contains(@"reshade-shaders\Shaders\ReShade.fxh");
            e.TurnOn(Quiet, new[] { e });
            bool placed = File.ReadAllText(Path.Combine(game, @"reshade-shaders\Shaders\ReShade.fxh")) == "// header";
            e.TurnOff(Quiet);
            Check(planned && placed && !Directory.Exists(Path.Combine(game, "reshade-shaders")), "download_save_puts_file_at_path");
        }));

        Case("manifest_rejects_escaping_save", () =>
            Check(Rejects(MinimalManifest.Replace(@"{ ""kind"": ""copy"", ""from"": ""payload/game"", ""to"": ""game"" }",
                @"{ ""kind"": ""download"", ""url"": ""https://example.com/a.fxh"", ""save"": ""..\\a.fxh"", ""to"": ""game"" }")), "manifest_rejects_escaping_save"));

        Case("runtime_files_removed_on_off_unless_preexisting", () => WithTempRoot(() =>
        {
            string game = ChosenGame();
            File.WriteAllText(Path.Combine(game, "keep.log"), "the player's own log");
            string dir = WithManifest(MinimalManifest.Replace(@"""install"":", @"""runtimeFiles"": [""mod.log"", ""keep.log""], ""install"":"));
            Directory.CreateDirectory(Path.Combine(dir, @"payload\game"));
            File.WriteAllText(Path.Combine(dir, @"payload\game\mod.asi"), "mod");
            var e = new Engine(Manifest.Load(dir));
            e.TurnOn(Quiet, new[] { e });
            File.WriteAllText(Path.Combine(game, "mod.log"), "written while playing");   // the mod's own log
            File.WriteAllText(Path.Combine(game, "keep.log"), "appended while playing");
            e.TurnOff(Quiet);
            Check(!File.Exists(Path.Combine(game, "mod.log")) && File.Exists(Path.Combine(game, "keep.log")) && e.State() == FileState.Off,
                "runtime_files_removed_on_off_unless_preexisting");
        }));
    }
}
