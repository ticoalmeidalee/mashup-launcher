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

class MainForm : Form
{
    readonly List<Mashup> mashups = Mashup.LoadAll();
    readonly List<Engine> engines;
    readonly List<Card> cards = new List<Card>();
    readonly Panel detail = new Panel();
    readonly PowerButton power = new PowerButton();
    readonly Label title = new Label(), tagline = new Label(), desc = new Label(), files = new Label(), guest = new Label(), host = new Label(), hint = new Label();
    readonly Panel settingsBox = new Panel();
    readonly ListBox log = new ListBox();
    Engine eng;
    Settings set;
    bool busy, hostSeen;
    const int LibW = 300;

    public MainForm()
    {
        engines = mashups.Select(m => new Engine(m)).ToList();
        Text = "Mashup Launcher"; BackColor = Ui.Bg; ForeColor = Ui.Text; Font = Ui.Font(9.5f);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(LibW + 460, 720);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

        // library column
        Controls.Add(new Label { Text = "LIBRARY", ForeColor = Ui.Sub, Font = Ui.Font(8, FontStyle.Bold), AutoSize = true, Location = new Point(18, 22) });
        Controls.Add(new Label { Text = mashups.Count + (mashups.Count == 1 ? " mashup" : " mashups"), ForeColor = Ui.Sub, Font = Ui.Font(8), AutoSize = true, Location = new Point(LibW - 90, 22) });
        var list = new FlowLayoutPanel { Location = new Point(14, 48), Size = new Size(LibW - 20, ClientSize.Height - 100), AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Ui.Bg };
        foreach (var m in mashups)
        {
            var c = new Card(m) { Margin = new Padding(0, 0, 0, 10) };
            c.Click += (o, e) => Select(cards.IndexOf(c));
            cards.Add(c); list.Controls.Add(c);
        }
        Controls.Add(list);
        var openFolder = new LinkLabel { Text = "Open mashups folder", AutoSize = true, Location = new Point(18, ClientSize.Height - 36), LinkColor = Ui.Sub, ActiveLinkColor = Ui.Text };
        openFolder.Click += (o, e) => { Directory.CreateDirectory(Paths.Mashups); Process.Start("explorer.exe", Paths.Mashups); };
        Controls.Add(openFolder);
        Controls.Add(new Panel { BackColor = Ui.Panel, Location = new Point(LibW, 0), Size = new Size(1, ClientSize.Height) });

        // detail column
        detail.Location = new Point(LibW + 1, 0); detail.Size = new Size(ClientSize.Width - LibW - 1, ClientSize.Height); detail.BackColor = Ui.Bg;
        Controls.Add(detail);
        title.Font = Ui.Font(18, FontStyle.Bold); title.AutoSize = true; title.Location = new Point(24, 18);
        tagline.ForeColor = Ui.Sub; tagline.AutoSize = true; tagline.Location = new Point(26, 54);
        desc.ForeColor = Ui.Sub; desc.AutoSize = false; desc.Size = new Size(410, 34); desc.Location = new Point(26, 76); desc.Font = Ui.Font(8.5f);
        detail.Controls.AddRange(new Control[] { title, tagline, desc });
        power.Location = new Point((detail.Width - power.Width) / 2, 112);
        power.Click += async (o, e) => await Flip();
        detail.Controls.Add(power);
        int y = 292;
        foreach (var l in new[] { files, guest, host }) { l.AutoSize = false; l.Size = new Size(410, 22); l.Location = new Point(24, y); detail.Controls.Add(l); y += 24; }
        settingsBox.BackColor = Ui.Panel; settingsBox.Location = new Point(24, y + 10);
        detail.Controls.Add(settingsBox);
        hint.ForeColor = Ui.Amber; hint.AutoSize = false; hint.Size = new Size(410, 34); detail.Controls.Add(hint);
        log.BackColor = Ui.Panel; log.ForeColor = Ui.Sub; log.BorderStyle = BorderStyle.None; log.Font = new Font("Consolas", 8.5f); log.IntegralHeight = false;
        detail.Controls.Add(log);

        if (mashups.Count == 0)
        {
            title.Text = "No mashups yet";
            tagline.Text = "Add a folder with a mashup.ini to " + Paths.Mashups;
            power.Visible = false;
        }

        var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += async (o, e) => await Tick();
        timer.Start();
        Shown += async (o, e) =>
        {
            int last = 0;
            try { last = Math.Max(0, mashups.FindIndex(m => m.Id == File.ReadAllText(Path.Combine(Paths.Mashups, ".last")).Trim())); } catch (Exception) { }
            if (mashups.Count > 0) Select(last);
            await Tick();
        };
    }

    void Select(int i)
    {
        if (busy || i < 0 || i >= mashups.Count) return;
        for (int k = 0; k < cards.Count; ++k) { cards[k].Selected = k == i; cards[k].Invalidate(); }
        eng = engines[i]; set = new Settings(eng.M); hostSeen = false;
        try { File.WriteAllText(Path.Combine(Paths.Mashups, ".last"), eng.M.Id); } catch (Exception) { }
        var m = eng.M;
        title.Text = m.Name; tagline.Text = m.Tagline; desc.Text = m.Description;
        hint.Text = m.Hint;
        // settings, with this mashup's game names
        settingsBox.Controls.Clear();
        settingsBox.Controls.Add(new Label { Text = "SETTINGS", ForeColor = Ui.Sub, Font = Ui.Font(8, FontStyle.Bold), AutoSize = true, Location = new Point(14, 10) });
        var all = Settings.All(m);
        settingsBox.Size = new Size(410, 30 + all.Length * 38);
        int ty = 34;
        foreach (var s in all)
        {
            var t = new Toggle { Checked = set[s.key], Location = new Point(352, ty), OnColor = m.Accent };
            var key = s.key; t.Changed += (o, e) => set[key] = t.Checked;
            settingsBox.Controls.Add(new Label { Text = s.label, AutoSize = true, Location = new Point(14, ty + 3), ForeColor = Ui.Text });
            settingsBox.Controls.Add(t); ty += 38;
        }
        hint.Location = new Point(24, settingsBox.Bottom + 10);
        log.Location = new Point(24, hint.Bottom + 4); log.Size = new Size(410, detail.Height - hint.Bottom - 20);
        log.Items.Clear();
        foreach (var e in engines) e.Log = s => { };
        eng.Log = s => BeginInvoke((Action)(() => { log.Items.Add(DateTime.Now.ToString("HH:mm:ss  ") + s); log.TopIndex = log.Items.Count - 1; }));
        _ = Tick();
    }

    static void Dot(Label l, string name, string state, Color c) { l.Text = "●  " + name + ":  " + state; l.ForeColor = c; }

    async Task Tick()
    {
        for (int k = 0; k < cards.Count; ++k)
        {
            bool on = engines[k].Files() != FileState.Off;
            if (cards[k].On != on) { cards[k].On = on; cards[k].Invalidate(); }
        }
        if (eng == null) return;
        var m = eng.M;
        var f = eng.Files(); bool h = eng.HostRunning(), link = eng.LinkUp(), g = eng.GuestRunning();
        Dot(files, "Mod files", f == FileState.On ? "installed" : f == FileState.Off ? "removed (" + m.HostName + " is stock)" : "partly installed, press FIX", f == FileState.On ? Ui.Green : f == FileState.Off ? Ui.Sub : Ui.Red);
        Dot(guest, m.GuestName, link ? "running, link ready" : g ? "starting..." : "not running", link ? Ui.Green : g ? Ui.Amber : Ui.Sub);
        Dot(host, m.HostName, h ? "running" : "not running", h ? Ui.Green : Ui.Sub);
        power.Busy = busy;
        power.Caption = f == FileState.On ? "ON" : f == FileState.Off ? "OFF" : "FIX";
        power.Fill = f == FileState.On ? m.Accent : f == FileState.Partial ? Ui.Red : Ui.Off;
        power.Invalidate();

        if (h && f == FileState.On) hostSeen = true;
        if (!busy && hostSeen && !h && f != FileState.Off && set["autoOff"])
        {
            hostSeen = false;
            eng.Log(m.HostName + " closed, turning OFF.");
            await Run(() => eng.TurnOff(set));
        }
    }

    async Task Flip()
    {
        if (busy || eng == null) return;
        // auto-OFF only counts a host seen running since this press: right after ON only the host's launcher stub
        // runs, and a host remembered from earlier must not read as "closed"
        hostSeen = false;
        if (eng.Files() != FileState.Off) await Run(() => eng.TurnOff(set));
        else await Run(() => eng.TurnOn(set, engines));
        hostSeen = false;
    }

    async Task Run(Action a)
    {
        busy = true; power.Busy = true; power.Invalidate();
        try { await Task.Run(a); }
        catch (Exception ex) { eng.Log("ERROR: " + ex.Message); MessageBox.Show(this, ex.Message, "Mashup Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { busy = false; await Tick(); }
    }
}
