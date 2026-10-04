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

static class Program
{
    [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);

    [STAThread]
    static int Main(string[] args)
    {
        int rootAt = Array.IndexOf(args, "--root");
        if (rootAt >= 0 && rootAt + 1 < args.Length)
            Paths.Root = System.IO.Path.GetFullPath(args[rootAt + 1]).TrimEnd('\\');
        if (args.Length > 0 && args[0] == "--test") { AttachConsole(-1); return Tests.Run(); }
        if (args.Length > 0)
        {
            AttachConsole(-1); // print into the console that started us
            var engines = Mashup.LoadAll().Select(m => new Engine(m)).ToList();
            if (engines.Count == 0) { Console.WriteLine("ERROR: no mashups in " + Paths.Mashups); return 1; }
            var eng = args.Length > 1 ? engines.FirstOrDefault(e => e.M.Id == args[1]) : engines[0];
            if (eng == null) { Console.WriteLine("ERROR: no mashup '" + args[1] + "'. Have: " + string.Join(", ", engines.Select(e => e.M.Id))); return 1; }
            eng.Log = s => Console.WriteLine(s);
            try
            {
                if (args[0] == "--on") eng.TurnOn(new Settings(eng.M), engines);
                else if (args[0] == "--off") eng.TurnOff(new Settings(eng.M));
                else if (args[0] == "--list") foreach (var e in engines) Console.WriteLine(e.M.Id + "  " + e.M.Name + "  [" + e.Files() + "]");
                Console.WriteLine("[" + eng.M.Id + "] files=" + eng.Files() + " host=" + eng.HostRunning() + " link=" + eng.LinkUp());
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
