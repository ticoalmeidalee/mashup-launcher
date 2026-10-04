using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

enum FileState { Off, On, Partial }

class Engine
{
    public readonly Mashup M;
    public Action<string> Log = s => { };
    public Engine(Mashup m) { M = m; }

    string GuestPid => Path.Combine(Paths.Logs, M.Id + "-guest.pid");

    void L(string s)
    {
        Directory.CreateDirectory(Paths.Logs);
        File.AppendAllText(Paths.Logs + @"\launcher.log", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + "[" + M.Id + "] " + s + "\r\n");
        Log(s);
    }

    public FileState Files()
    {
        int n = M.Files.Count(f => File.Exists(Path.Combine(M.GameDir, f)) || Directory.Exists(Path.Combine(M.GameDir, f)));
        return n == 0 ? FileState.Off : n == M.Files.Length ? FileState.On : FileState.Partial;
    }
    public bool HostRunning() => M.HostProcess.Length > 0 && Process.GetProcessesByName(M.HostProcess).Length > 0;
    bool HostOrStubRunning() => HostRunning() || (M.HostStub.Length > 0 && Process.GetProcessesByName(M.HostStub).Length > 0);
    public bool LinkUp() => M.GuestPort > 0 && IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(e => e.Port == M.GuestPort);

    Process OurGuest()
    {
        try
        {
            var p = Process.GetProcessById(int.Parse(File.ReadAllText(GuestPid).Trim()));
            return p.ProcessName.Equals("cmd", StringComparison.OrdinalIgnoreCase) ? p : null; // pid reuse guard
        }
        catch (Exception) { return null; }
    }
    public bool GuestRunning() => OurGuest() != null || LinkUp();

    void Script(string arg)
    {
        var psi = new ProcessStartInfo(Paths.Bash, "\"" + M.Script + "\" " + arg)
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using (var p = Process.Start(psi))
        {
            string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit();
            foreach (var line in o.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0)) L(line);
        }
    }

    static bool WaitFor(Func<bool> condition, int seconds)
    {
        for (int i = 0; i < seconds * 2; ++i)
        {
            if (condition())
                return true;
            Thread.Sleep(500);
        }
        return condition();
    }

    public void TurnOn(Settings s, IEnumerable<Engine> others)
    {
        var on = others.FirstOrDefault(o => o != this && o.M.GameDir.Equals(M.GameDir, StringComparison.OrdinalIgnoreCase) && o.Files() != FileState.Off);
        if (on != null)
            throw new InvalidOperationException(on.M.Name + " is ON in the same game. Turn it OFF first.");
        if (HostRunning())
            throw new InvalidOperationException(M.HostName + " is already running. Close it, then turn ON.");
        L("Installing mod files into " + M.HostName + "...");
        Script("on");
        if (Files() != FileState.On) throw new InvalidOperationException("Mod files did not install. See logs\\launcher.log.");

        if (s["startGuest"] && M.GuestCommand.Length > 0)
        {
            if (GuestRunning()) L(M.GuestName + " is already running.");
            else
            {
                L("Starting " + M.GuestName + " (its first start can take a few minutes)...");
                // .\ : cmd may not run programs from the current folder (NoDefaultCurrentDirectoryInExePath)
                var psi = new ProcessStartInfo("cmd.exe", "/c .\\" + M.GuestCommand + " > \"" + Path.Combine(Paths.Logs, M.Id + "-guest.log") + "\" 2>&1")
                { WorkingDirectory = M.GuestDir, UseShellExecute = false, CreateNoWindow = true };
                foreach (var e in M.GuestEnv) psi.EnvironmentVariables[e.Key] = e.Value;
                Directory.CreateDirectory(Paths.Logs);
                File.WriteAllText(GuestPid, Process.Start(psi).Id.ToString());
            }
        }
        if (s["startHost"] && M.HostLaunch.Length > 0)
            StartHost();
        L("ON.");
    }

    // while the last host (or its launcher stub) is still exiting, Steam thinks the game runs and ignores a launch
    void StartHost()
    {
        if (HostOrStubRunning())
        {
            L("Waiting for the last " + M.HostName + " to finish exiting...");
            if (!WaitFor(() => !HostOrStubRunning(), 60))
                throw new InvalidOperationException(M.HostName + " is still exiting. Wait a moment, then turn ON again.");
            Thread.Sleep(5000); // Steam drops its "running" state a few seconds after the exit
        }
        for (int attempt = 1; attempt <= 2; ++attempt)
        {
            L(attempt == 1 ? "Starting " + M.HostName + ". " + M.Hint : M.HostName + " didn't start, asking again...");
            Process.Start(new ProcessStartInfo(M.HostLaunch) { UseShellExecute = true });
            if (WaitFor(HostOrStubRunning, 90))
                return;
        }
        throw new InvalidOperationException(M.HostName + " didn't start. Start it yourself; the mod files are installed.");
    }

    public void TurnOff(Settings s)
    {
        var host = M.HostProcess.Length > 0 ? Process.GetProcessesByName(M.HostProcess) : new Process[0];
        if (host.Length > 0)
        {
            L("Closing " + M.HostName + "...");
            foreach (var p in host) p.CloseMainWindow();
            foreach (var p in host)
                if (!p.WaitForExit(20000)) { L(M.HostName + " did not close in 20 s, ending it."); p.Kill(); p.WaitForExit(10000); }
        }
        if (s["closeGuest"])
        {
            var guest = OurGuest();
            if (guest != null)
            {
                L("Closing " + M.GuestName + "...");
                // the guest runs under the cmd wrapper we started: end exactly that tree, by PID
                Process.Start(new ProcessStartInfo("taskkill", "/PID " + guest.Id + " /T /F") { UseShellExecute = false, CreateNoWindow = true }).WaitForExit();
                File.Delete(GuestPid);
            }
            else if (LinkUp()) L(M.GuestName + " was started outside this app, leaving it open.");
        }
        Thread.Sleep(1500); // let Windows release the host's file locks
        L("Removing mod files from " + M.HostName + "...");
        Script("off");
        if (Files() != FileState.Off) throw new InvalidOperationException("Some mod files are still there. Close " + M.HostName + " and press OFF again.");
        L("OFF. " + M.HostName + " is stock.");
    }
}
