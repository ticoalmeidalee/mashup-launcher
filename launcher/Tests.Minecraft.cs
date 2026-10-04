using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

static partial class Tests
{
    static readonly byte[] FakeFabricApiJar = Encoding.UTF8.GetBytes("fabric api jar");

    static string Sha512(byte[] b) { using (var s = SHA512.Create()) return string.Concat(s.ComputeHash(b).Select(x => x.ToString("x2"))); }

    /// <summary>Stands in for the network: Fabric's profile JSON, Modrinth's version list, the Fabric API jar.</summary>
    static Func<string, byte[]> FakeNet(string fabricApiSha512) => url =>
    {
        if (url.Contains("meta.fabricmc.net"))
            return Encoding.UTF8.GetBytes("{\"id\":\"fabric-loader-0.19.5-26.3\",\"inheritsFrom\":\"26.3\",\"libraries\":[]}");
        if (url.Contains("api.modrinth.com"))
            return Encoding.UTF8.GetBytes("[{\"version_number\":\"0.161.0+26.3\",\"files\":[{\"filename\":\"fabric-api-0.161.0+26.3.jar\",\"primary\":true," +
                "\"url\":\"https://cdn.modrinth.com/fabric-api.jar\",\"hashes\":{\"sha512\":\"" + fabricApiSha512 + "\"}}]}]");
        if (url.Contains("cdn.modrinth.com"))
            return FakeFabricApiJar;
        throw new InvalidOperationException("unexpected download " + url);
    };

    static Manifest MinecraftManifest() => Manifest.Load(WithManifest(MinimalManifest.Replace(@"""install"":",
        @"""guest"": { ""name"": ""Minecraft"", ""kind"": ""minecraft-fabric"", ""minecraft"": ""26.3"", ""fabricLoader"": ""0.19.5"", ""fabricApi"": ""0.161.0+26.3"" }, ""install"":")));

    /// <summary>A fake .minecraft with one existing profile the install must not disturb.</summary>
    static string FakeDotMinecraft()
    {
        string dot = TempDir();
        File.WriteAllText(Path.Combine(dot, "launcher_profiles.json"),
            "{\"profiles\":{\"mine\":{\"name\":\"My worlds\",\"type\":\"custom\",\"lastVersionId\":\"1.21.10\"}},\"settings\":{\"keepLauncherOpen\":true},\"version\":3}");
        return dot;
    }

    static IDictionary<string, object> Profiles(string dot) =>
        Json.Obj(Json.Parse(File.ReadAllText(Path.Combine(dot, "launcher_profiles.json"))), "profiles");

    static void MinecraftTests()
    {
        Case("profile_added_and_removed", () => WithTempRoot(() =>
        {
            string dot = FakeDotMinecraft();
            var m = MinecraftManifest();
            MinecraftProfile.Install(m, Encoding.UTF8.GetBytes("mod jar"), dot, FakeNet(Sha512(FakeFabricApiJar)));
            var p = Json.Obj(Profiles(dot), "mashup-t");
            string mods = Path.Combine(Paths.Data, "t", "minecraft", "mods");
            bool added = p != null && Json.Str(p, "lastVersionId") == "fabric-loader-0.19.5-26.3"
                && Json.Str(p, "gameDir") == Path.Combine(Paths.Data, "t", "minecraft")
                && File.Exists(Path.Combine(dot, "versions", "fabric-loader-0.19.5-26.3", "fabric-loader-0.19.5-26.3.json"))
                && File.ReadAllText(Path.Combine(mods, "fabric-api-0.161.0+26.3.jar")) == "fabric api jar"
                && File.ReadAllText(Path.Combine(mods, "t.jar")) == "mod jar"
                && Json.Obj(Profiles(dot), "mine") != null;
            MinecraftProfile.Remove(m, dot);
            var after = Profiles(dot);
            bool removed = after.Count == 1 && after.ContainsKey("mine")
                && !Directory.Exists(Path.Combine(dot, "versions", "fabric-loader-0.19.5-26.3"))
                && Json.Bool(Json.Obj(Json.Parse(File.ReadAllText(Path.Combine(dot, "launcher_profiles.json"))), "settings"), "keepLauncherOpen");
            Check(added && removed, "profile_added_and_removed (added=" + added + " removed=" + removed + ")");
        }));

        Case("profile_removal_restores_exact_bytes", () => WithTempRoot(() =>
        {
            string dot = FakeDotMinecraft();
            string file = Path.Combine(dot, "launcher_profiles.json");
            File.WriteAllText(file, "{\n  \"profiles\" : {\n    \"mine\" : { \"name\" : \"My worlds\", \"type\" : \"custom\" }\n  },\n  \"version\" : 3\n}\n"); // the launcher's own formatting
            string before = File.ReadAllText(file);
            var m = MinecraftManifest();
            MinecraftProfile.Install(m, Encoding.UTF8.GetBytes("mod jar"), dot, FakeNet(Sha512(FakeFabricApiJar)));
            MinecraftProfile.Remove(m, dot);
            bool noLeftovers = Directory.GetFiles(dot).Select(Path.GetFileName).SequenceEqual(new[] { "launcher_profiles.json" });
            Check(File.ReadAllText(file) == before && noLeftovers, "profile_removal_restores_exact_bytes (no files left in .minecraft: " + noLeftovers + ")");
        }));

        Case("profile_removal_keeps_later_changes", () => WithTempRoot(() =>
        {
            string dot = FakeDotMinecraft();
            var m = MinecraftManifest();
            MinecraftProfile.Install(m, Encoding.UTF8.GetBytes("mod jar"), dot, FakeNet(Sha512(FakeFabricApiJar)));
            // the player adds a profile while the mashup is ON: OFF must keep it
            string file = Path.Combine(dot, "launcher_profiles.json");
            File.WriteAllText(file, File.ReadAllText(file).Replace("\"profiles\":{", "\"profiles\":{\"new\":{\"name\":\"Added later\"},"));
            MinecraftProfile.Remove(m, dot);
            var after = Profiles(dot);
            Check(after.ContainsKey("new") && after.ContainsKey("mine") && !after.ContainsKey("mashup-t"), "profile_removal_keeps_later_changes");
        }));

        Case("existing_version_json_kept", () => WithTempRoot(() =>
        {
            string dot = FakeDotMinecraft();
            string vdir = Path.Combine(dot, "versions", "fabric-loader-0.19.5-26.3");
            Directory.CreateDirectory(vdir);
            File.WriteAllText(Path.Combine(vdir, "fabric-loader-0.19.5-26.3.json"), "{\"theirs\":true}");
            var m = MinecraftManifest();
            MinecraftProfile.Install(m, Encoding.UTF8.GetBytes("mod jar"), dot, FakeNet(Sha512(FakeFabricApiJar)));
            MinecraftProfile.Remove(m, dot);
            Check(File.ReadAllText(Path.Combine(vdir, "fabric-loader-0.19.5-26.3.json")) == "{\"theirs\":true}", "existing_version_json_kept");
        }));

        Case("fabric_api_hash_checked", () => WithTempRoot(() =>
        {
            string dot = FakeDotMinecraft();
            string before = File.ReadAllText(Path.Combine(dot, "launcher_profiles.json"));
            bool threw = false;
            try { MinecraftProfile.Install(MinecraftManifest(), Encoding.UTF8.GetBytes("mod jar"), dot, FakeNet(new string('0', 128))); }
            catch (InvalidDataException) { threw = true; }
            Check(threw && File.ReadAllText(Path.Combine(dot, "launcher_profiles.json")) == before, "fabric_api_hash_checked");
        }));
    }
}
