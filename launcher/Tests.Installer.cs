using System;
using System.IO;
using System.Linq;

static partial class Tests
{
    /// <summary>Every file and folder under dir (relative) with file contents, for exact before/after comparison.</summary>
    static string Snapshot(string dir) => string.Join("\n",
        Directory.GetFileSystemEntries(dir, "*", SearchOption.AllDirectories).OrderBy(p => p)
            .Select(p => p.Substring(dir.Length) + (File.Exists(p) ? "=" + File.ReadAllText(p) : "/")));

    /// <summary>Runs body with Paths.Root pointed at a fresh temp folder (install records and backups land there).</summary>
    static void WithTempRoot(Action body)
    {
        string old = Paths.Root;
        Paths.Root = TempDir();
        try { body(); } finally { Paths.Root = old; }
    }

    static void Write(string path, string text) => File.WriteAllText(path, text);

    static void InstallerTests()
    {
        Case("install_then_remove_leaves_folder_identical", () => WithTempRoot(() =>
        {
            string game = TempDir();
            Write(Path.Combine(game, "a.txt"), "game file");
            string before = Snapshot(game);
            Installer.AddFile("t", game, "x.dll", tmp => Write(tmp, "mod"));
            Installer.AddFile("t", game, @"sub\deeper\y.ini", tmp => Write(tmp, "cfg"));
            Installer.Finish("t");
            bool on = Installer.State("t", game) == FileState.On && File.ReadAllText(Path.Combine(game, @"sub\deeper\y.ini")) == "cfg";
            Installer.RemoveAll("t", game);
            Check(on && Snapshot(game) == before && Installer.State("t", game) == FileState.Off, "install_then_remove_leaves_folder_identical");
        }));

        Case("existing_file_is_backed_up_and_restored", () => WithTempRoot(() =>
        {
            string game = TempDir();
            Write(Path.Combine(game, "dinput8.dll"), "theirs");
            Installer.AddFile("t", game, "dinput8.dll", tmp => Write(tmp, "ours"));
            Installer.Finish("t");
            bool replaced = File.ReadAllText(Path.Combine(game, "dinput8.dll")) == "ours";
            Installer.RemoveAll("t", game);
            Check(replaced && File.ReadAllText(Path.Combine(game, "dinput8.dll")) == "theirs", "existing_file_is_backed_up_and_restored");
        }));

        Case("partial_install_is_removable", () => WithTempRoot(() =>
        {
            string game = TempDir();
            Write(Path.Combine(game, "a.txt"), "game file");
            string before = Snapshot(game);
            Installer.AddFile("t", game, "x.dll", tmp => Write(tmp, "mod"));
            // crash before Finish: a fresh process sees only what is on disk
            bool partial = Installer.State("t", game) == FileState.Partial;
            Installer.RemoveAll("t", game);
            Check(partial && Snapshot(game) == before && Installer.State("t", game) == FileState.Off, "partial_install_is_removable");
        }));

        Case("write_failure_leaves_nothing", () => WithTempRoot(() =>
        {
            string game = TempDir();
            string before = Snapshot(game);
            try { Installer.AddFile("t", game, "x.dll", tmp => { throw new IOException("download failed"); }); } catch (IOException) { }
            Installer.RemoveAll("t", game);
            Check(Snapshot(game) == before && Installer.State("t", game) == FileState.Off, "write_failure_leaves_nothing");
        }));

        Case("target_outside_game_rejected", () => WithTempRoot(() =>
        {
            string game = TempDir();
            bool threw = false;
            try { Installer.AddFile("t", game, @"..\escape.dll", tmp => Write(tmp, "x")); } catch (ManifestException) { threw = true; }
            Check(threw && !File.Exists(Path.Combine(Path.GetDirectoryName(game), "escape.dll")), "target_outside_game_rejected");
        }));
    }
}
