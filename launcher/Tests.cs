// MashupLauncher.exe --test: plain-assert self-tests over temp folders (no network; file:// fixtures).
using System;
using System.Collections.Generic;
using System.IO;

static partial class Tests
{
    static int passed, failed;
    static readonly List<string> temps = new List<string>();

    public static int Run()
    {
        Console.WriteLine("Mashup Launcher self-tests");
        Check(Directory.Exists(Paths.Root), "root exists");
        Check(Paths.Mashups == Path.Combine(Paths.Root, "mashups") && Paths.Data == Path.Combine(Paths.Root, "data") && Paths.Backups == Path.Combine(Paths.Data, "backups"), "data folders are root/mashups, root/data, data/backups");
        ManifestTests();
        InstallerTests();
        DownloadTests();
        GameLocatorTests();
        MinecraftTests();
        EngineTests();
        LibraryTests();
        ScaffoldTests();
        foreach (var t in temps) try { Directory.Delete(t, true); } catch (Exception) { }
        Console.WriteLine(passed + "/" + (passed + failed) + " passed");
        return failed == 0 ? 0 : 1;
    }

    public static void Check(bool ok, string name)
    {
        if (ok) passed++; else failed++;
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
    }

    /// <summary>Runs a test body; an exception is a failure named after the test.</summary>
    public static void Case(string name, Action body)
    {
        try { body(); }
        catch (Exception e) { Check(false, name + " threw " + e.GetType().Name + ": " + e.Message); }
    }

    public static string TempDir()
    {
        string d = Path.Combine(Path.GetTempPath(), "mashup-test-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(d);
        temps.Add(d);
        return d;
    }
}
