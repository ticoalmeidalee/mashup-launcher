using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

/// <summary>The library: mashup cards on the left; the selected one's details, game folder, power button and settings on the right.</summary>
partial class MainForm : Form
{
    List<Engine> engines = new List<Engine>();
    readonly List<Card> cards = new List<Card>();
    readonly FlowLayoutPanel list = new FlowLayoutPanel();
    readonly Label count = new Label();
    readonly Panel detail = new Panel();
    readonly PowerButton power = new PowerButton();
    readonly Label title = new Label(), tagline = new Label(), desc = new Label(), folder = new Label(), files = new Label(), guest = new Label(), host = new Label(), hint = new Label();
    readonly Button change = new Button();
    readonly Panel settingsBox = new Panel();
    readonly ListBox log = new ListBox();
    Engine eng;
    Settings set;
    bool busy, hostSeen;
    protected const int LibW = 300;

    public MainForm()
    {
        Text = "Mashup Launcher"; BackColor = Ui.Bg; ForeColor = Ui.Text; Font = Ui.Font(9.5f);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(LibW + 460, 740);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

        // library column
        Controls.Add(new Label { Text = "LIBRARY", ForeColor = Ui.Sub, Font = Ui.Font(8, FontStyle.Bold), AutoSize = true, Location = new Point(18, 22) });
        count.ForeColor = Ui.Sub; count.Font = Ui.Font(8); count.AutoSize = true; count.Location = new Point(LibW - 90, 22);
        Controls.Add(count);
        list.Location = new Point(14, 48); list.Size = new Size(LibW - 20, ClientSize.Height - 140);
        list.AutoScroll = true; list.FlowDirection = FlowDirection.TopDown; list.WrapContents = false; list.BackColor = Ui.Bg;
        Controls.Add(list);
        AddLibraryActions(); // Browse / Add / Create (MainForm.Library.cs)
        Controls.Add(new Panel { BackColor = Ui.Panel, Location = new Point(LibW, 0), Size = new Size(1, ClientSize.Height) });

        // detail column
        detail.Location = new Point(LibW + 1, 0); detail.Size = new Size(ClientSize.Width - LibW - 1, ClientSize.Height); detail.BackColor = Ui.Bg;
        Controls.Add(detail);
        title.Font = Ui.Font(18, FontStyle.Bold); title.AutoSize = true; title.Location = new Point(24, 18);
        tagline.ForeColor = Ui.Sub; tagline.AutoSize = true; tagline.Location = new Point(26, 54);
        desc.ForeColor = Ui.Sub; desc.AutoSize = false; desc.Size = new Size(410, 34); desc.Location = new Point(26, 76); desc.Font = Ui.Font(8.5f);
        folder.AutoSize = false; folder.Size = new Size(330, 34); folder.Location = new Point(26, 112); folder.Font = Ui.Font(8.5f);
        change.Text = "Change..."; change.FlatStyle = FlatStyle.Flat; change.ForeColor = Ui.Text; change.Size = new Size(80, 26); change.Location = new Point(356, 114);
        change.Click += (o, e) => PickFolder();
        detail.Controls.AddRange(new Control[] { title, tagline, desc, folder, change });
        power.Location = new Point((detail.Width - power.Width) / 2, 150);
        power.Click += async (o, e) => await Flip();
        detail.Controls.Add(power);
        int y = 330;
        foreach (var l in new[] { files, guest, host }) { l.AutoSize = false; l.Size = new Size(410, 22); l.Location = new Point(24, y); detail.Controls.Add(l); y += 24; }
        settingsBox.BackColor = Ui.Panel; settingsBox.Location = new Point(24, y + 10);
        detail.Controls.Add(settingsBox);
        hint.ForeColor = Ui.Amber; hint.AutoSize = false; hint.Size = new Size(410, 34); detail.Controls.Add(hint);
        log.BackColor = Ui.Panel; log.ForeColor = Ui.Sub; log.BorderStyle = BorderStyle.None; log.Font = new Font("Consolas", 8.5f); log.IntegralHeight = false;
        detail.Controls.Add(log);

        var timer = new Timer { Interval = 1000 };
        timer.Tick += async (o, e) => await Tick();
        timer.Start();
        Shown += (o, e) => Reload();
    }

    /// <summary>Re-reads mashups\ and rebuilds the cards, keeping the selection (or the last used mashup).</summary>
    public void Reload(string select = null)
    {
        if (busy) return;
        select = select ?? eng?.M.Id;
        if (select == null)
            try { select = File.ReadAllText(Path.Combine(Paths.Data, ".last")).Trim(); } catch (Exception) { }
        engines = Library.Local().Select(m => new Engine(m)).ToList();
        list.Controls.Clear(); cards.Clear();
        foreach (var e in engines)
        {
            var c = new Card(e.M) { Margin = new Padding(0, 0, 0, 10) };
            c.Click += (o, ev) => Select(cards.IndexOf(c));
            cards.Add(c); list.Controls.Add(c);
        }
        count.Text = engines.Count + (engines.Count == 1 ? " mashup" : " mashups");
        foreach (string p in Library.Problems) eng?.Log("Skipped " + p);
        int i = Math.Max(0, engines.FindIndex(e => e.M.Id == select));
        if (engines.Count > 0) Select(i);
        else ShowEmpty();
    }

    void ShowEmpty()
    {
        eng = null;
        title.Text = "No mashups yet";
        tagline.Text = "Browse the public library, add a mashup .zip, or create your own.";
        desc.Text = Library.Problems.Count > 0 ? "Skipped: " + string.Join("; ", Library.Problems) : "";
        foreach (var c in new Control[] { power, folder, change, files, guest, host, settingsBox, hint }) c.Visible = false;
    }

    void Select(int i)
    {
        if (busy || i < 0 || i >= engines.Count) return;
        foreach (var c in new Control[] { power, folder, change, files, guest, host, settingsBox, hint }) c.Visible = true;
        for (int k = 0; k < cards.Count; ++k) { cards[k].Selected = k == i; cards[k].Invalidate(); }
        eng = engines[i]; set = new Settings(eng.M); hostSeen = false;
        try { Directory.CreateDirectory(Paths.Data); File.WriteAllText(Path.Combine(Paths.Data, ".last"), eng.M.Id); } catch (Exception) { }
        var m = eng.M;
        title.Text = m.Name; tagline.Text = m.Tagline + (m.Authors.Length > 0 ? "  ·  by " + string.Join(", ", m.Authors) : "") + "  ·  v" + m.Version;
        desc.Text = m.Description; hint.Text = m.Hint;
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

    void PickFolder()
    {
        if (eng == null || busy) return;
        if (eng.State() != FileState.Off) { MessageBox.Show(this, "Turn the mashup OFF before changing the game folder.", "Mashup Launcher"); return; }
        using (var d = new FolderBrowserDialog { Description = "Pick the folder " + eng.M.Host.Name + " is installed in (it contains " + eng.M.Host.Exe + ")", ShowNewFolderButton = false, SelectedPath = eng.GameDir ?? "" })
        {
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try { GameLocator.Choose(eng.M.Id, d.SelectedPath, eng.M.Host.Exe); eng.Log("Game folder: " + d.SelectedPath); }
            catch (InvalidOperationException ex) { MessageBox.Show(this, ex.Message, "Mashup Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        _ = Tick();
    }

    static void Dot(Label l, string name, string state, Color c) { l.Text = "●  " + name + ":  " + state; l.ForeColor = c; }

    async Task Tick()
    {
        for (int k = 0; k < cards.Count; ++k)
        {
            bool on = engines[k].State() != FileState.Off;
            if (cards[k].On != on) { cards[k].On = on; cards[k].Invalidate(); }
        }
        if (eng == null) return;
        var m = eng.M;
        string game = eng.GameDir;
        folder.Text = game == null ? m.Host.Name + " folder not found: choose it" : m.Host.Name + " folder:  " + game;
        folder.ForeColor = game == null ? Ui.Amber : Ui.Sub;
        var f = eng.State(); bool h = eng.HostRunning(), link = eng.LinkUp();
        Dot(files, "Mod files", f == FileState.On ? "installed" : f == FileState.Off ? "not installed (" + m.Host.Name + " is stock)" : "partly installed: press FIX", f == FileState.On ? Ui.Green : f == FileState.Off ? Ui.Sub : Ui.Red);
        guest.Visible = m.Guest != null;
        if (m.Guest != null) Dot(guest, m.Guest.Name, link ? "running, link ready" : "not running", link ? Ui.Green : Ui.Sub);
        Dot(host, m.Host.Name, h ? "running" : "not running", h ? Ui.Green : Ui.Sub);
        power.Busy = busy;
        power.Caption = f == FileState.On ? "ON" : f == FileState.Off ? "OFF" : "FIX";
        power.Fill = f == FileState.On ? m.Accent : f == FileState.Partial ? Ui.Red : Ui.Off;
        power.Invalidate();

        if (h && f == FileState.On) hostSeen = true;
        if (!busy && hostSeen && !h && f != FileState.Off && set["autoOff"])
        {
            hostSeen = false;
            eng.Log(m.Host.Name + " closed, turning OFF.");
            await Run(() => eng.TurnOff(set));
        }
    }

    async Task Flip()
    {
        if (busy || eng == null) return;
        // auto-OFF only counts a host seen running since this press: right after ON only the host's launcher stub
        // runs, and a host remembered from earlier must not read as "closed"
        hostSeen = false;
        if (eng.State() != FileState.Off) await Run(() => eng.TurnOff(set));
        else
        {
            if (eng.GameDir == null) { PickFolder(); if (eng.GameDir == null) return; }
            InstallPlan plan;
            try { plan = eng.Plan(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Mashup Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (!TrustDialog.Confirm(this, eng.M, plan)) return;
            await Run(() => eng.TurnOn(set, engines));
        }
        hostSeen = false;
    }

    async Task Run(Action a)
    {
        busy = true; power.Busy = true; power.Invalidate();
        try { await Task.Run(a); }
        catch (Exception ex) { eng?.Log("ERROR: " + ex.Message); MessageBox.Show(this, ex.Message, "Mashup Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { busy = false; await Tick(); }
    }
}
