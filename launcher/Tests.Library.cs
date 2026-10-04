using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

static partial class Tests
{
    /// <summary>A mashup package zip (mashup.json + a payload file) for id, optionally inside a top folder.</summary>
    static byte[] Package(string id, string topFolder = "", Dictionary<string, string> extra = null)
    {
        var entries = new Dictionary<string, string>
        {
            [topFolder + "mashup.json"] = MinimalManifest.Replace(@"""id"": ""t""", @"""id"": """ + id + @""""),
            [topFolder + "payload/game/mod.asi"] = "mod",
        };
        foreach (var e in extra ?? new Dictionary<string, string>()) entries[e.Key] = e.Value;
        return MakeZip(entries);
    }

    /// <summary>An index (file://) listing one package at a file:// url with the given pin.</summary>
    static IndexEntry Indexed(string id, byte[] zip, string sha = null)
    {
        string dir = TempDir();
        string zipPath = Path.Combine(dir, id + ".zip");
        File.WriteAllBytes(zipPath, zip);
        string index = Path.Combine(dir, "index.json");
        File.WriteAllText(index, "{\"format\":1,\"mashups\":[{\"id\":\"" + id + "\",\"name\":\"Pkg " + id + "\",\"version\":\"1.0.0\",\"tagline\":\"t\"," +
            "\"url\":\"" + FileUrl(zipPath) + "\",\"sha256\":\"" + (sha ?? Downloads.Sha256(zip)) + "\"}]}");
        return Library.Public(FileUrl(index)).Single();
    }

    static void LibraryTests()
    {
        Case("package_installs_from_index", () => WithTempRoot(() =>
        {
            Library.InstallPackage(Indexed("pkg", Package("pkg")));
            string dir = Path.Combine(Paths.Mashups, "pkg");
            Check(File.Exists(Path.Combine(dir, "mashup.json")) && File.Exists(Path.Combine(dir, @"payload\game\mod.asi"))
                && new Engine(Manifest.Load(dir)).Reviewed && Library.Local().Any(m => m.Id == "pkg"), "package_installs_from_index");
        }));

        Case("package_in_top_folder_installs", () => WithTempRoot(() =>
        {
            Library.InstallPackage(Indexed("pkg2", Package("pkg2", "pkg2-1.0.0/")));
            Check(File.Exists(Path.Combine(Paths.Mashups, "pkg2", "mashup.json")), "package_in_top_folder_installs");
        }));

        Case("package_hash_mismatch_installs_nothing", () => WithTempRoot(() =>
        {
            bool threw = false;
            try { Library.InstallPackage(Indexed("pkg", Package("pkg"), new string('0', 64))); } catch (InvalidDataException) { threw = true; }
            Check(threw && !Directory.Exists(Path.Combine(Paths.Mashups, "pkg")), "package_hash_mismatch_installs_nothing");
        }));

        Case("zip_slip_rejected", () => WithTempRoot(() =>
        {
            var evil = Package("pkg", "", new Dictionary<string, string> { ["../../evil.txt"] = "pwned" });
            bool threw = false;
            try { Library.InstallPackage(Indexed("pkg", evil)); } catch (ManifestException) { threw = true; }
            bool escaped = File.Exists(Path.Combine(Paths.Root, "evil.txt")) || File.Exists(Path.Combine(Path.GetDirectoryName(Paths.Root), "evil.txt"));
            Check(threw && !escaped && !Directory.Exists(Path.Combine(Paths.Mashups, "pkg")), "zip_slip_rejected");
        }));

        Case("package_id_must_match_manifest", () => WithTempRoot(() =>
        {
            bool threw = false;
            try { Library.InstallPackage(Indexed("wanted", Package("other"))); } catch (ManifestException) { threw = true; }
            Check(threw && !Directory.Exists(Path.Combine(Paths.Mashups, "other")) && !Directory.Exists(Path.Combine(Paths.Mashups, "wanted")), "package_id_must_match_manifest");
        }));

        Case("local_zip_is_unreviewed", () => WithTempRoot(() =>
        {
            string zip = Path.Combine(TempDir(), "mine.zip");
            File.WriteAllBytes(zip, Package("mine"));
            Library.InstallLocalZip(zip);
            Check(!new Engine(Manifest.Load(Path.Combine(Paths.Mashups, "mine"))).Reviewed, "local_zip_is_unreviewed");
        }));

        Case("update_refused_while_on", () => WithTempRoot(() =>
        {
            Library.InstallPackage(Indexed("t", Package("t")));
            string game = ChosenGame();
            var e = new Engine(Manifest.Load(Path.Combine(Paths.Mashups, "t")));
            e.TurnOn(Quiet, new[] { e });
            string message = null;
            try { Library.InstallPackage(Indexed("t", Package("t"))); } catch (InvalidOperationException ex) { message = ex.Message; }
            e.TurnOff(Quiet);
            Check(message != null && message.Contains("OFF"), "update_refused_while_on");
        }));
    }
}
