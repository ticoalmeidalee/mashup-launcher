using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

static class Program
{
    [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);

    /// <summary>
    /// No arguments: the library window. Headless (prints to the console): --list, --on/--off/--status [id],
    /// --choose id folder, --test. --root folder overrides where the launcher keeps its data.
    /// </summary>
    [STAThread]
    static int Main(string[] args)
    {
        int rootAt = Array.IndexOf(args, "--root");
        if (rootAt >= 0 && rootAt + 1 < args.Length)
        {
            Paths.Root = System.IO.Path.GetFullPath(args[rootAt + 1]).TrimEnd('\\');
            args = args.Where((a, i) => i != rootAt && i != rootAt + 1).ToArray(); // --root alone still opens the window
        }
        if (args.Length > 0 && args[0] == "--test") { AttachConsole(-1); return Tests.Run(); }
        if (args.Length > 0)
        {
            AttachConsole(-1); // print into the console that started us
            var engines = Library.Local().Select(m => new Engine(m)).ToList();
            foreach (string p in Library.Problems) Console.WriteLine("skipped " + p);
            if (args[0] == "--list")
            {
                foreach (var e in engines) Console.WriteLine(e.M.Id + "  " + e.M.Name + " " + e.M.Version + "  [" + e.State() + "]  " + (e.GameDir ?? "(game folder not found)"));
                return 0;
            }
            if (engines.Count == 0) { Console.WriteLine("ERROR: no mashups in " + Paths.Mashups); return 1; }
            var eng = args.Length > 1 ? engines.FirstOrDefault(e => e.M.Id == args[1]) : engines[0];
            if (eng == null) { Console.WriteLine("ERROR: no mashup '" + args[1] + "'. Have: " + string.Join(", ", engines.Select(e => e.M.Id))); return 1; }
            eng.Log = s => Console.WriteLine(s);
            try
            {
                if (args[0] == "--choose" && args.Length > 2) GameLocator.Choose(eng.M.Id, args[2], eng.M.Host.Exe);
                else if (args[0] == "--on") eng.TurnOn(new Settings(eng.M), engines);
                else if (args[0] == "--off") eng.TurnOff(new Settings(eng.M));
                Console.WriteLine("[" + eng.M.Id + "] state=" + eng.State() + " game=" + (eng.GameDir ?? "?") + " host=" + eng.HostRunning() + " link=" + eng.LinkUp());
                return 0;
            }
            catch (Exception ex) { Console.WriteLine("ERROR: " + ex.Message); return 1; }
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
        return 0;
    }
}
