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

class Toggle : Control
{
    bool on;
    public event EventHandler Changed;
    public bool Checked { get => on; set { on = value; Invalidate(); } }
    public Color OnColor = Ui.Green;
    public Toggle() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true); Size = new Size(44, 24); Cursor = Cursors.Hand; BackColor = Color.Transparent; }
    protected override void OnClick(EventArgs e) { Checked = !on; Changed?.Invoke(this, e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = Ui.Round(new Rectangle(0, 0, Width - 1, Height - 1), Height / 2))
        using (var b = new SolidBrush(on ? OnColor : Ui.Off)) g.FillPath(b, path);
        int d = Height - 6, x = on ? Width - d - 3 : 3;
        g.FillEllipse(Brushes.White, x, 3, d, d);
    }
}

class PowerButton : Control
{
    public string Caption = "OFF"; public Color Fill = Ui.Off; public bool Busy;
    public PowerButton() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true); Size = new Size(170, 170); Cursor = Cursors.Hand; BackColor = Color.Transparent; }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var ring = new Pen(Color.FromArgb(60, Fill), 10)) g.DrawEllipse(ring, 6, 6, Width - 13, Height - 13);
        using (var b = new SolidBrush(Busy ? Ui.Amber : Fill)) g.FillEllipse(b, 16, 16, Width - 33, Height - 33);
        using (var p = new Pen(Color.White, 6) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            int c = Width / 2, r = 26;
            g.DrawArc(p, c - r, c - r - 10, 2 * r, 2 * r, -60, 300);
            g.DrawLine(p, c, c - r - 18, c, c - 14);
        }
        TextRenderer.DrawText(g, Busy ? "..." : Caption, Ui.Font(12, FontStyle.Bold), new Rectangle(0, Height / 2 + 18, Width, 28), Color.White, TextFormatFlags.HorizontalCenter);
    }
}

/// <summary>A library tile: the mashup's cover (or its two-colour gradient), name, tagline and ON/OFF state.</summary>
class Card : Control
{
    public readonly Manifest M;
    public readonly Image Cover; // cover.png in the mashup folder, or null (a gradient with initials)
    public string Badge = ""; // e.g. "UPDATE", shown top-right
    public bool Selected, On;
    public Card(Manifest m) { M = m; Cover = Ui.LoadCover(m); SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); Size = new Size(268, 96); Cursor = Cursors.Hand; BackColor = Ui.Bg; }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); }
    bool hover;
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = Ui.Round(r, 10))
        {
            using (var bg = new SolidBrush(Selected ? Ui.PanelHi : hover ? Ui.PanelHover : Ui.Panel)) g.FillPath(bg, path);
            var art = new Rectangle(8, 8, 80, Height - 17);
            using (var artPath = Ui.Round(art, 7))
            {
                g.SetClip(artPath);
                if (Cover != null) g.DrawImage(Cover, art);
                else
                    using (var grad = new LinearGradientBrush(art, M.Accent2, M.Accent, 45f)) g.FillRectangle(grad, art);
                g.ResetClip();
            }
            if (Cover == null)
                TextRenderer.DrawText(g, Ui.Initials(M.Name), Ui.Font(18, FontStyle.Bold), art, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (Selected)
                using (var pen = new Pen(M.Accent, 2)) g.DrawPath(pen, path);
        }
        TextRenderer.DrawText(g, M.Name, Ui.Font(11, FontStyle.Bold), new Rectangle(98, 14, Width - 104, 22), Ui.Text, TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, M.Tagline, Ui.Font(8.5f), new Rectangle(98, 37, Width - 104, 18), Ui.Sub, TextFormatFlags.EndEllipsis);
        using (var dot = new SolidBrush(On ? Ui.Green : Ui.Off)) g.FillEllipse(dot, 99, 66, 9, 9);
        TextRenderer.DrawText(g, On ? "ON" : "OFF", Ui.Font(8, FontStyle.Bold), new Point(112, 62), On ? Ui.Green : Ui.Sub);
    }
}
