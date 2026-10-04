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

static class Paths
{
    // everything lives next to the exe, so the folder can move (--root overrides it: the test build runs from %TEMP%)
    public static string Root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
    public const string Bash = @"C:\Program Files\Git\bin\bash.exe";
    public static string Mashups => Root + @"\mashups";
    public static string Data => Root + @"\data";      // install records, per-user game folders, guest data
    public static string Backups => Data + @"\backups"; // originals a mashup replaced, restored on OFF
    public static string Logs => Root + @"\logs";
}
