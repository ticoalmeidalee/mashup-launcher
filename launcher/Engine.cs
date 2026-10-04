using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;

enum FileState { Off, On, Partial }

/// <summary>What turning a mashup ON would do: shown on the trust screen before anything happens.</summary>
class InstallPlan
{
    public string GameDir;
    public List<string> Files = new List<string>();                              // game-folder paths it adds or replaces
    public List<(string what, string host)> Downloads = new List<(string, string)>();
    public bool MinecraftProfile;
    public bool AntiCheatFound;
    public string AntiCheatNote, OfflineArgs;
    public bool Reviewed;                                                         // installed from the reviewed public index
}

/// <summary>Turns one mashup ON (install steps, then the guest and host games) and OFF (close, remove exactly what ON added).</summary>
class Engine
{
    public readonly Manifest M;
    public Action<string> Log = s => { };
    public Engine(Manifest m) { M = m; }

    static readonly string[] AntiCheatMarks = { "BattlEye", "EasyAntiCheat", "EasyAntiCheat_EOS", "start_protected_game.exe", "BEService.exe", "BEService_x64.exe" };

    public static bool AntiCheatPresent(string gameDir) =>
        gameDir != null && AntiCheatMarks.Any(n => File.Exists(Path.Combine(gameDir, n)) || Directory.Exists(Path.Combine(gameDir, n)));

    /// <summary>The folder an install wrote to while one exists (OFF must clean that one), else the chosen/Steam folder.</summary>
    public string GameDir => Installer.RecordedGameDir(M.Id) ?? GameLocator.Resolve(M.Id, M.Host.Exe, M.Host.SteamAppId);

    /// <summary>Picks the game folder; only while OFF, so ON and OFF always work on the same folder.</summary>
    public void ChooseGameFolder(string dir)
    {
        if (State() != FileState.Off)
            throw new InvalidOperationException("Turn " + M.Name + " OFF before changing the game folder.");
        GameLocator.Choose(M.Id, dir, M.Host.Exe);
    }

    public FileState State()
    {
        string game = GameDir;
        if (game == null) return InstallRecord.Exists(M.Id) ? FileState.Partial : FileState.Off;
        return Installer.State(M.Id, game);
    }

    public bool Reviewed
    {
        get
        {
            string f = Path.Combine(M.Dir, "source.json");
            return File.Exists(f) && Json.Bool(Json.Parse(File.ReadAllText(f)), "reviewed");
        }
    }

    public bool HostRunning() => M.Host.Process.Length > 0 && Process.GetProcessesByName(M.Host.Process).Length > 0;
    bool HostOrStubRunning() => HostRunning() || (M.Host.Stub.Length > 0 && Process.GetProcessesByName(M.Host.Stub).Length > 0);
    public bool LinkUp() => M.Guest != null && M.Guest.LinkPort > 0 &&
        IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(e => e.Port == M.Guest.LinkPort);

    void L(string s)
    {
        try
        {
            Directory.CreateDirectory(Paths.Logs);
            File.AppendAllText(Path.Combine(Paths.Logs, "launcher.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + "[" + M.Id + "] " + s + "\r\n");
        }
        catch (IOException) { }
        Log(s);
    }

    string RequireGame()
    {
        return GameDir ?? throw new InvalidOperationException("Choose " + M.Host.Name + "'s folder first (the one with " + M.Host.Exe + " in it).");
    }

    /// <summary>Every file each copy step would add, as game-relative paths.</summary>
    IEnumerable<(string rel, string source)> CopyFiles(Step s)
    {
        string src = Installer.Inside(M.Dir, s.From);
        if (File.Exists(src))
            yield return (Path.GetFileName(src), src);
        else if (Directory.Exists(src))
            foreach (string f in Directory.GetFiles(src, "*", SearchOption.AllDirectories).OrderBy(f => f))
                yield return (f.Substring(src.TrimEnd('\\').Length + 1), f);
        else
            throw new ManifestException("Copy source '" + s.From + "' is not in the mashup");
    }

    public InstallPlan Plan()
    {
        string game = GameDir;
        var p = new InstallPlan { GameDir = game, AntiCheatFound = AntiCheatPresent(game), AntiCheatNote = M.Host.AntiCheatNote, OfflineArgs = M.Host.OfflineArgs, Reviewed = Reviewed };
        foreach (var s in M.Install)
        {
            if (s.Kind == "copy")
                p.Files.AddRange(CopyFiles(s).Select(f => f.rel));
            else if (s.Kind == "download")
            {
                p.Downloads.Add((s.Page ? "latest file from " + s.Url : s.Url, new Uri(s.Url).Host));
                if (s.Extract != null) p.Files.AddRange(s.Extract.Values);
                else if (s.Save != null) p.Files.Add(s.Save);
                else if (!s.Page) p.Files.Add(Path.GetFileName(new Uri(s.Url).LocalPath));
            }
            else if (s.Kind == "minecraft-profile")
            {
                p.MinecraftProfile = true;
                p.Downloads.Add(("Fabric loader " + M.Guest?.FabricLoader, "meta.fabricmc.net"));
                p.Downloads.Add(("Fabric API " + M.Guest?.FabricApi, "modrinth.com"));
            }
        }
        return p;
    }

    public void TurnOn(Settings settings, IEnumerable<Engine> others, Func<string, byte[]> get = null)
    {
        string game = RequireGame();
        if (M.Install.Count == 0)
            throw new InvalidOperationException(M.Name + " is a draft: it has no install steps yet. Build it with universal-modder (see PROMPT.md in its folder).");
        if (AntiCheatPresent(game) && string.IsNullOrEmpty(M.Host.OfflineArgs))
            throw new InvalidOperationException(M.Host.Name + " uses anti-cheat, and " + M.Name + " doesn't say how it keeps the game offline. Refusing to install.");
        var busy = others.FirstOrDefault(o => o != this && o.State() != FileState.Off && string.Equals(o.GameDir, game, StringComparison.OrdinalIgnoreCase));
        if (busy != null)
            throw new InvalidOperationException(busy.M.Name + " is ON in the same game folder. Turn it OFF first.");
        if (HostRunning())
            throw new InvalidOperationException(M.Host.Name + " is running. Close it, then turn ON.");

        try
        {
            Installer.TrackRuntime(M.Id, game, M.RuntimeFiles);
            foreach (var s in M.Install)
                RunStep(s, game, get);
            Installer.Finish(M.Id);
        }
        catch (Exception)
        {
            L("Install failed, undoing what was added...");
            Installer.RemoveAll(M.Id, game);
            if (MinecraftProfile.Installed(M)) MinecraftProfile.Remove(M);
            throw;
        }
        L("Installed.");

        if (settings["startGuest"] && M.Guest?.Kind == "minecraft-fabric")
        {
            L("Opening the Minecraft Launcher: pick the \"" + M.Name + "\" profile and press Play.");
            MinecraftProfile.OpenLauncher();
        }
        if (settings["startHost"] && M.Host.Launch.Length > 0)
            StartHost();
        L("ON.");
    }

    void RunStep(Step s, string game, Func<string, byte[]> get)
    {
        switch (s.Kind)
        {
            case "copy":
                foreach (var (rel, source) in CopyFiles(s))
                {
                    L("Adding " + rel);
                    Installer.AddFile(M.Id, game, rel, tmp => File.Copy(source, tmp, true));
                }
                break;
            case "download":
            {
                string url = s.Page ? Downloads.FindLinkOnPage(s.Url, s.Pattern) : s.Url;
                L("Downloading " + url);
                byte[] data = get != null ? get(url) : Downloads.Verified(url, s.Sha256, s.Page ? s.Url : null);
                if (get != null && !string.IsNullOrEmpty(s.Sha256) && !Downloads.Sha256(data).Equals(s.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Download does not match its pinned SHA-256");
                var files = s.Extract != null ? Downloads.Extract(data, s.Extract)
                    : new Dictionary<string, byte[]> { [s.Save ?? Manifest.SafeRelative(Path.GetFileName(new Uri(url).LocalPath))] = data };
                foreach (var f in files)
                {
                    L("Adding " + f.Key);
                    Installer.AddFile(M.Id, game, f.Key, tmp => File.WriteAllBytes(tmp, f.Value));
                }
                break;
            }
            case "minecraft-profile":
                L("Setting up the Minecraft profile...");
                byte[] jar = s.Jar != null ? File.ReadAllBytes(Installer.Inside(M.Dir, s.Jar)) : new byte[0];
                MinecraftProfile.Install(M, jar, null, get);
                break;
        }
    }

    static bool WaitFor(Func<bool> condition, int seconds)
    {
        for (int i = 0; i < seconds * 2; ++i)
        {
            if (condition()) return true;
            Thread.Sleep(500);
        }
        return condition();
    }

    // while the last host (or its launcher stub) is still exiting, Steam thinks the game runs and ignores a launch
    void StartHost()
    {
        if (HostOrStubRunning())
        {
            L("Waiting for the last " + M.Host.Name + " to finish exiting...");
            if (!WaitFor(() => !HostOrStubRunning(), 60))
                throw new InvalidOperationException(M.Host.Name + " is still exiting. Wait a moment, then turn ON again.");
            Thread.Sleep(5000); // Steam drops its "running" state a few seconds after the exit
        }
        for (int attempt = 1; attempt <= 2; ++attempt)
        {
            L(attempt == 1 ? "Starting " + M.Host.Name + ". " + M.Hint : M.Host.Name + " didn't start, asking again...");
            Process.Start(new ProcessStartInfo(M.Host.Launch) { UseShellExecute = true });
            if (WaitFor(HostOrStubRunning, 90)) return;
        }
        throw new InvalidOperationException(M.Host.Name + " didn't start. Start it yourself; the mashup is installed.");
    }

    public void TurnOff(Settings settings)
    {
        var host = M.Host.Process.Length > 0 ? Process.GetProcessesByName(M.Host.Process) : new Process[0];
        if (host.Length > 0)
        {
            L("Closing " + M.Host.Name + "...");
            foreach (var p in host) p.CloseMainWindow();
            foreach (var p in host)
                if (!p.WaitForExit(20000)) { L(M.Host.Name + " did not close in 20 s, ending it."); p.Kill(); p.WaitForExit(10000); }
            Thread.Sleep(1500); // let Windows release the game's file locks
        }
        string game = GameDir;
        if (game != null)
        {
            L("Removing the mashup's files from " + M.Host.Name + "...");
            Installer.RemoveAll(M.Id, game);
        }
        if (MinecraftProfile.Installed(M))
        {
            L("Removing the Minecraft profile...");
            MinecraftProfile.Remove(M);
        }
        if (State() != FileState.Off)
            throw new InvalidOperationException("Some files are still there. Close " + M.Host.Name + " and press OFF again.");
        L("OFF. " + M.Host.Name + " is stock.");
    }
}
