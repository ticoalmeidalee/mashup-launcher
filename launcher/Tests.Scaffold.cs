using System;
using System.IO;

static partial class Tests
{
    static void ScaffoldTests()
    {
        Case("scaffold_writes_valid_manifest", () => WithTempRoot(() =>
        {
            string host = TempDir(), guest = TempDir();
            File.WriteAllText(Path.Combine(host, "Game.exe"), "");
            File.WriteAllText(Path.Combine(guest, "Other.exe"), "");
            string dir = Scaffold.Create("Valheim x The Forest", host, "Game.exe", guest, "Other.exe");
            var m = Manifest.Load(dir);
            string prompt = File.ReadAllText(Path.Combine(dir, "PROMPT.md"));
            Check(m.Id == "valheim-x-the-forest" && m.Host.Exe == "Game.exe" && m.Guest != null && m.Install.Count == 0
                && prompt.Contains(host) && prompt.Contains(guest) && prompt.Contains("mashup.json")
                && string.Equals(GameLocator.Chosen(m.Id), host, StringComparison.OrdinalIgnoreCase), "scaffold_writes_valid_manifest");
        }));

        Case("scaffold_ids_are_unique", () => WithTempRoot(() =>
        {
            string host = TempDir();
            File.WriteAllText(Path.Combine(host, "Game.exe"), "");
            string a = Scaffold.Create("Same Name", host, "Game.exe", null, null);
            string b = Scaffold.Create("Same Name", host, "Game.exe", null, null);
            Check(Path.GetFileName(a) == "same-name" && Path.GetFileName(b) == "same-name-2", "scaffold_ids_are_unique");
        }));

        Case("draft_keeps_the_windows_username_out", () => WithTempRoot(() =>
        {
            string host = TempDir();
            File.WriteAllText(Path.Combine(host, "Game.exe"), "");
            string dir = Scaffold.Create("Shareable", host, "Game.exe", null, null);
            var m = Manifest.Load(dir);
            string json = File.ReadAllText(Path.Combine(dir, "mashup.json"));
            string prompt = File.ReadAllText(Path.Combine(dir, "PROMPT.md"));
            Check(m.Authors.Length == 0 && !json.Contains(Environment.UserName) && prompt.Contains("don't share this file"),
                "draft_keeps_the_windows_username_out");
        }));

        Case("draft_mashup_cannot_turn_on", () => WithTempRoot(() =>
        {
            string host = TempDir();
            File.WriteAllText(Path.Combine(host, "Game.exe"), "");
            var e = new Engine(Manifest.Load(Scaffold.Create("Draft", host, "Game.exe", null, null)));
            string message = null;
            try { e.TurnOn(Quiet, new[] { e }); } catch (InvalidOperationException ex) { message = ex.Message; }
            Check(message != null && message.Contains("draft") && e.State() == FileState.Off, "draft_mashup_cannot_turn_on");
        }));
    }
}
