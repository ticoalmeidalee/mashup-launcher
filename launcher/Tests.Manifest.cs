using System;
using System.IO;

static partial class Tests
{
    const string MinimalManifest = @"{
  ""format"": 1, ""id"": ""t"", ""name"": ""Test"", ""version"": ""1.0.0"",
  ""host"": { ""name"": ""Game"", ""exe"": ""Game.exe"", ""process"": ""Game"" },
  ""install"": [ { ""kind"": ""copy"", ""from"": ""payload/game"", ""to"": ""game"" } ]
}";

    /// <summary>A temp mashup folder holding this mashup.json.</summary>
    static string WithManifest(string json)
    {
        string dir = TempDir();
        File.WriteAllText(Path.Combine(dir, "mashup.json"), json);
        return dir;
    }

    static bool Rejects(string json)
    {
        try { Manifest.Load(WithManifest(json)); return false; }
        catch (ManifestException) { return true; }
    }

    static void ManifestTests()
    {
        Case("manifest_loads", () =>
        {
            var m = Manifest.Load(WithManifest(MinimalManifest));
            Check(m.Id == "t" && m.Name == "Test" && m.Host.Exe == "Game.exe" && m.Install.Count == 1 && m.Install[0].Kind == "copy", "manifest_loads");
        });
        Case("manifest_rejects_escaping_paths", () =>
        {
            bool to = Rejects(MinimalManifest.Replace(@"""to"": ""game""", @"""to"": ""..\\..""" ));
            bool from = Rejects(MinimalManifest.Replace(@"""from"": ""payload/game""", @"""from"": ""C:\\Windows"""));
            bool up = Rejects(MinimalManifest.Replace(@"""from"": ""payload/game""", @"""from"": ""payload/../../x"""));
            bool extract = Rejects(MinimalManifest.Replace(@"{ ""kind"": ""copy"", ""from"": ""payload/game"", ""to"": ""game"" }",
                @"{ ""kind"": ""download"", ""url"": ""https://example.com/a.zip"", ""extract"": { ""a.dll"": ""..\\x.dll"" }, ""to"": ""game"" }"));
            Check(to && from && up && extract, "manifest_rejects_escaping_paths (to=" + to + " from=" + from + " up=" + up + " extract=" + extract + ")");
        });
        Case("manifest_rejects_unknown_step", () =>
            Check(Rejects(MinimalManifest.Replace(@"""kind"": ""copy""", @"""kind"": ""run""")), "manifest_rejects_unknown_step"));
        Case("manifest_rejects_bad_id", () =>
            Check(Rejects(MinimalManifest.Replace(@"""id"": ""t""", @"""id"": ""a/b""")), "manifest_rejects_bad_id"));
        Case("manifest_requires_host_exe", () =>
            Check(Rejects(MinimalManifest.Replace(@"""exe"": ""Game.exe"", ", "")), "manifest_requires_host_exe"));
    }
}
