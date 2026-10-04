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

static class Ui
{
    public static readonly Color Bg = Color.FromArgb(15, 17, 21), Panel = Color.FromArgb(24, 27, 34), PanelHover = Color.FromArgb(30, 34, 43),
        PanelHi = Color.FromArgb(36, 41, 52), Text = Color.FromArgb(230, 232, 236), Sub = Color.FromArgb(139, 147, 161),
        Green = Color.FromArgb(61, 220, 132), Off = Color.FromArgb(58, 63, 75), Red = Color.FromArgb(229, 83, 75), Amber = Color.FromArgb(227, 179, 65);
    public static Font Font(float size, FontStyle st = FontStyle.Regular) => new Font("Segoe UI", size, st);
    public static GraphicsPath Round(Rectangle r, int radius)
    {
        var p = new GraphicsPath(); int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
    public static string Initials(string name) =>
        string.Concat(name.Split(new[] { ' ', 'x', 'X', '×' }, StringSplitOptions.RemoveEmptyEntries).Where(w => char.IsLetter(w[0])).Take(2).Select(w => char.ToUpper(w[0])));
}
