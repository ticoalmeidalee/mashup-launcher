using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

/// <summary>Shown before a mashup is turned ON: every file it adds, every download and where from, and who vouches for it.</summary>
class TrustDialog : Form
{
    public static bool Confirm(IWin32Window owner, Manifest m, InstallPlan plan)
    {
        using (var d = new TrustDialog(m, plan))
            return d.ShowDialog(owner) == DialogResult.OK;
    }

    TrustDialog(Manifest m, InstallPlan plan)
    {
        Text = "Turn ON " + m.Name + "?";
        BackColor = Ui.Bg; ForeColor = Ui.Text; Font = Ui.Font(9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 560);

        var badge = new Label
        {
            AutoSize = false, Size = new Size(512, 40), Location = new Point(24, 18), Padding = new Padding(10, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft, Font = Ui.Font(9.5f, FontStyle.Bold),
            Text = plan.Reviewed ? "✔  Reviewed public mashup" : "!  Local mashup: not reviewed. Only continue if you trust where it came from.",
            BackColor = plan.Reviewed ? Color.FromArgb(28, 61, 44) : Color.FromArgb(70, 52, 20),
            ForeColor = plan.Reviewed ? Ui.Green : Ui.Amber,
        };
        Controls.Add(badge);

        var lines = new System.Collections.Generic.List<string>();
        lines.Add("GAME FOLDER");
        lines.Add("   " + plan.GameDir);
        lines.Add("");
        lines.Add("FILES IT ADDS TO THE GAME FOLDER (originals are backed up and restored on OFF)");
        lines.AddRange(plan.Files.Select(f => "   " + f));
        if (plan.Files.Count == 0 && plan.Downloads.Count > 0) lines.Add("   (named by the downloads below)");
        if (plan.Downloads.Count > 0)
        {
            lines.Add("");
            lines.Add("DOWNLOADS");
            foreach (var d in plan.Downloads)
            {
                lines.Add("   " + d.host + (d.pinned ? "   [pinned SHA-256]" : "   [not pinned]"));
                lines.Add("      " + d.what);
            }
        }
        if (plan.RuntimeFiles.Count > 0)
        {
            lines.Add("");
            lines.Add("FILES THE MOD WRITES WHILE PLAYING (OFF deletes them unless they were there before)");
            lines.AddRange(plan.RuntimeFiles.Select(f => "   " + f));
        }
        if (plan.Launch != null)
        {
            lines.Add("");
            lines.Add("STARTS");
            lines.Add("   " + plan.Launch);
        }
        if (plan.MinecraftProfile)
        {
            lines.Add("");
            lines.Add("MINECRAFT");
            lines.Add("   Adds a \"" + m.Name + "\" profile to your Minecraft Launcher (in %APPDATA%\\.minecraft), with its own");
            lines.Add("   worlds and mods folder inside the launcher's data folder.");
            lines.Add("   You play it with your own account. OFF removes the profile.");
        }
        if (plan.AntiCheatFound)
        {
            lines.Add("");
            lines.Add("ANTI-CHEAT");
            lines.Add("   This game has anti-cheat. The mashup keeps it offline with: " + plan.OfflineArgs);
            if (!string.IsNullOrEmpty(plan.AntiCheatNote)) lines.Add("   " + plan.AntiCheatNote);
            lines.Add("   Never play online while the mashup is ON.");
        }
        var list = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
            BackColor = Ui.Panel, ForeColor = Ui.Text, Font = new Font("Consolas", 9f),
            Location = new Point(24, 72), Size = new Size(512, 410), Text = string.Join("\r\n", lines),
        };
        Controls.Add(list);

        var ok = new Button { Text = "Turn ON", DialogResult = DialogResult.OK, Location = new Point(332, 504), Size = new Size(100, 32), FlatStyle = FlatStyle.Flat, BackColor = m.Accent, ForeColor = Color.Black };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(440, 504), Size = new Size(96, 32), FlatStyle = FlatStyle.Flat, ForeColor = Ui.Text };
        ok.FlatAppearance.BorderSize = 0;
        Controls.Add(ok); Controls.Add(cancel);
        AcceptButton = ok; CancelButton = cancel;
        Shown += (o, e) => cancel.Focus(); // nothing happens on a stray Enter
    }
}
