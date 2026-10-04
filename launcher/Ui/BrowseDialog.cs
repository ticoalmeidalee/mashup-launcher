using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

/// <summary>The reviewed public mashups (library/index.json): install one, or update one you have.</summary>
class BrowseDialog : Form
{
    readonly FlowLayoutPanel rows = new FlowLayoutPanel();
    readonly Label status = new Label();
    public string Installed; // id of the last mashup installed here, for the library to select

    public BrowseDialog()
    {
        Text = "Public mashups";
        BackColor = Ui.Bg; ForeColor = Ui.Text; Font = Ui.Font(9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(600, 520);
        Controls.Add(new Label { Text = "REVIEWED PUBLIC MASHUPS", ForeColor = Ui.Sub, Font = Ui.Font(8, FontStyle.Bold), AutoSize = true, Location = new Point(20, 18) });
        Controls.Add(new Label { Text = "Each one was reviewed before it was listed, and its download is checked against the index's SHA-256.", ForeColor = Ui.Sub, Font = Ui.Font(8.5f), AutoSize = true, Location = new Point(20, 38) });
        rows.Location = new Point(16, 64); rows.Size = new Size(568, 410); rows.AutoScroll = true; rows.FlowDirection = FlowDirection.TopDown; rows.WrapContents = false;
        Controls.Add(rows);
        status.Location = new Point(20, 484); status.Size = new Size(560, 24); status.ForeColor = Ui.Sub; status.Text = "Loading the index from GitHub...";
        Controls.Add(status);
        Shown += async (o, e) => await LoadIndex();
    }

    async Task LoadIndex()
    {
        try
        {
            var entries = await Task.Run(() => Library.Public());
            var local = Library.Local().ToDictionary(m => m.Id, m => m.Version);
            rows.Controls.Clear();
            foreach (var e in entries) rows.Controls.Add(Row(e, local.TryGetValue(e.Id, out string v) ? v : null));
            status.Text = entries.Count == 0 ? "No public mashups yet." : entries.Count + " public mashup" + (entries.Count == 1 ? "" : "s");
        }
        catch (Exception ex) { status.Text = "Couldn't load the public index: " + ex.Message; status.ForeColor = Ui.Amber; }
    }

    Control Row(IndexEntry e, string localVersion)
    {
        var p = new Panel { Size = new Size(540, 70), BackColor = Ui.Panel, Margin = new Padding(0, 0, 0, 8) };
        p.Controls.Add(new Label { Text = e.Name + "  v" + e.Version, Font = Ui.Font(10.5f, FontStyle.Bold), AutoSize = true, Location = new Point(12, 10), ForeColor = Ui.Text });
        p.Controls.Add(new Label { Text = e.Tagline + (e.Authors.Length > 0 ? "  ·  by " + string.Join(", ", e.Authors) : ""), AutoSize = false, Size = new Size(400, 34), Location = new Point(12, 32), ForeColor = Ui.Sub, Font = Ui.Font(8.5f) });
        bool have = localVersion != null, newer = have && localVersion != e.Version;
        var b = new Button { Text = !have ? "Install" : newer ? "Update" : "Installed", Enabled = !have || newer, Size = new Size(96, 30), Location = new Point(430, 20), FlatStyle = FlatStyle.Flat, ForeColor = Ui.Text };
        b.Click += async (o, ev) =>
        {
            b.Enabled = false; b.Text = "...";
            try
            {
                var m = await Task.Run(() => Library.InstallPackage(e));
                Installed = m.Id; b.Text = "Installed";
                status.Text = m.Name + " installed. Close this window to see it in your library."; status.ForeColor = Ui.Green;
            }
            catch (Exception ex) { b.Enabled = true; b.Text = have ? "Update" : "Install"; status.Text = ex.Message; status.ForeColor = Ui.Amber; }
        };
        p.Controls.Add(b);
        return p;
    }
}
