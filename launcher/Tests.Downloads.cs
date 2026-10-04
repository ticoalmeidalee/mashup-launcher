using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

static partial class Tests
{
    static string FileUrl(string path) => new Uri(path).AbsoluteUri;

    /// <summary>The pattern the GTA x Minecraft manifest uses for ScriptHookV's runtime zip (digits first: not the SDK).</summary>
    const string ScriptHookVPattern = @"/files/ScriptHookV_[0-9][0-9._]*\.zip";

    /// <summary>A zip with the given entries (name -> text) as bytes.</summary>
    static byte[] MakeZip(Dictionary<string, string> entries)
    {
        using (var ms = new MemoryStream())
        {
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                foreach (var e in entries)
                    using (var w = new StreamWriter(zip.CreateEntry(e.Key).Open()))
                        w.Write(e.Value);
            return ms.ToArray();
        }
    }

    static void DownloadTests()
    {
        string dir = TempDir();
        string payload = Path.Combine(dir, "payload.bin");
        File.WriteAllBytes(payload, Encoding.UTF8.GetBytes("hello mashup"));
        string sha = Downloads.Sha256(File.ReadAllBytes(payload));

        Case("hash_ok", () =>
            Check(Encoding.UTF8.GetString(Downloads.Verified(FileUrl(payload), sha)) == "hello mashup", "hash_ok"));

        Case("hash_mismatch_aborts", () =>
        {
            bool threw = false;
            try { Downloads.Verified(FileUrl(payload), new string('0', 64)); } catch (InvalidDataException) { threw = true; }
            Check(threw, "hash_mismatch_aborts");
        });

        var zip = MakeZip(new Dictionary<string, string> { ["bin/a.dll"] = "AAA", ["readme.txt"] = "r" });
        Case("extract_zip_members", () =>
        {
            var got = Downloads.Extract(zip, new Dictionary<string, string> { ["bin/a.dll"] = "a.dll" });
            Check(got.Count == 1 && Encoding.UTF8.GetString(got["a.dll"]) == "AAA", "extract_zip_members");
        });

        Case("extract_exe_appended_zip", () =>
        {
            var exe = new byte[4096].Select((b, i) => (byte)(i * 7)).Concat(zip).ToArray();
            var got = Downloads.Extract(exe, new Dictionary<string, string> { ["bin/a.dll"] = "a.dll" });
            Check(got.Count == 1 && Encoding.UTF8.GetString(got["a.dll"]) == "AAA", "extract_exe_appended_zip");
        });

        Case("extract_missing_member_throws", () =>
        {
            bool threw = false;
            try { Downloads.Extract(zip, new Dictionary<string, string> { ["nope.dll"] = "x.dll" }); } catch (InvalidDataException) { threw = true; }
            Check(threw, "extract_missing_member_throws");
        });

        Case("find_link", () =>
        {
            string page = Path.Combine(dir, "page.html");
            // the real page's naming (2026-10): the runtime zip has a second build number; the SDK must not match
            File.WriteAllText(page, "<html><a href=\"/files/ScriptHookV_SDK_1.0.617.1a.zip\">sdk</a> <a href=\"/files/ScriptHookV_3889.0_1158.13.zip\">dl</a></html>");
            string link = Downloads.FindLink("https://www.dev-c.com/gtav/scripthookv/", File.ReadAllText(page), ScriptHookVPattern);
            Check(link == "https://www.dev-c.com/files/ScriptHookV_3889.0_1158.13.zip", "find_link (" + link + ")");
        });
    }
}
