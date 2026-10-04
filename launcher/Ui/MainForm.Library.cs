using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

/// <summary>The library column's actions: browse the public mashups, add a .zip, create your own (Task 9), open the folder.</summary>
partial class MainForm
{
    protected Button browse, addZip;

    void AddLibraryActions()
    {
        browse = ActionButton("Browse public mashups", new Point(14, ClientSize.Height - 84), LibW - 20);
        browse.Click += (o, e) =>
        {
            using (var d = new BrowseDialog())
            {
                d.ShowDialog(this);
                if (d.Installed != null) Reload(d.Installed);
            }
        };
        addZip = ActionButton("Add .zip", new Point(14, ClientSize.Height - 50), 120);
        addZip.Click += (o, e) => AddZip();
        var openFolder = new LinkLabel { Text = "Open folder", AutoSize = true, Location = new Point(LibW - 86, ClientSize.Height - 44), LinkColor = Ui.Sub, ActiveLinkColor = Ui.Text };
        openFolder.Click += (o, e) => { Directory.CreateDirectory(Paths.Mashups); Process.Start("explorer.exe", Paths.Mashups); };
        Controls.Add(openFolder);
    }

    Button ActionButton(string text, Point at, int width)
    {
        var b = new Button { Text = text, Location = at, Size = new Size(width, 28), FlatStyle = FlatStyle.Flat, ForeColor = Ui.Text, BackColor = Ui.Panel };
        b.FlatAppearance.BorderColor = Ui.PanelHi;
        Controls.Add(b);
        return b;
    }

    void AddZip()
    {
        using (var d = new OpenFileDialog { Title = "Add a mashup (.zip)", Filter = "Mashup package (*.zip)|*.zip" })
        {
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try { Reload(Library.InstallLocalZip(d.FileName).Id); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Mashup Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }
}
