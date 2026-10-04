using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

/// <summary>The library column's actions. Task 8 adds Browse and Add .zip, Task 9 Create.</summary>
partial class MainForm
{
    void AddLibraryActions()
    {
        var openFolder = new LinkLabel { Text = "Open mashups folder", AutoSize = true, Location = new Point(18, ClientSize.Height - 36), LinkColor = Ui.Sub, ActiveLinkColor = Ui.Text };
        openFolder.Click += (o, e) => { Directory.CreateDirectory(Paths.Mashups); Process.Start("explorer.exe", Paths.Mashups); };
        Controls.Add(openFolder);
    }
}
