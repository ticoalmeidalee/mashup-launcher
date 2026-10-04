using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

/// <summary>Create mashup: name it, pick the host game folder (and optionally a guest), get a draft + a universal-modder prompt.</summary>
class CreateDialog : Form
{
    readonly TextBox name = new TextBox();
    readonly TextBox hostDir = new TextBox(), guestDir = new TextBox();
    readonly ComboBox hostExe = new ComboBox(), guestExe = new ComboBox();
    readonly Label result = new Label();
    readonly Button create = new Button(), copyPrompt = new Button(), openFolder = new Button(), openClaude = new Button();
    public string Created; // id of the created draft

    public CreateDialog()
    {
        Text = "Create a mashup";
        BackColor = Ui.Bg; ForeColor = Ui.Text; Font = Ui.Font(9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 470);

        Add(new Label { Text = "Pick the games: the launcher makes a draft mashup, and universal-modder (an AI modding toolkit) builds it.", AutoSize = false, Size = new Size(520, 36), Location = new Point(20, 16), ForeColor = Ui.Sub });
        Field("Name", 60, name);
        name.Text = "My mashup";
        GamePicker("Host game (the world you play in)", 110, hostDir, hostExe);
        GamePicker("Guest game (optional: what you bring into it)", 200, guestDir, guestExe);

        create.Text = "Create draft"; Style(create, new Point(20, 292), 130); create.BackColor = Ui.Green; create.ForeColor = Color.Black;
        create.Click += (o, e) => DoCreate();
        result.Location = new Point(20, 334); result.Size = new Size(520, 70); result.ForeColor = Ui.Sub;
        Add(result);
        copyPrompt.Text = "Copy prompt"; Style(copyPrompt, new Point(20, 412), 120);
        openFolder.Text = "Open folder"; Style(openFolder, new Point(148, 412), 110);
        openClaude.Text = "Open in Claude Code"; Style(openClaude, new Point(266, 412), 160);
        foreach (var b in new[] { copyPrompt, openFolder, openClaude }) b.Visible = false;
        copyPrompt.Click += (o, e) => Clipboard.SetText(File.ReadAllText(Path.Combine(Paths.Mashups, Created, "PROMPT.md")));
        openFolder.Click += (o, e) => Process.Start("explorer.exe", Path.Combine(Paths.Mashups, Created));
        openClaude.Click += (o, e) => Process.Start(new ProcessStartInfo("cmd.exe", "/c start \"Claude Code\" claude \"Read PROMPT.md and build this mashup.\"")
            { WorkingDirectory = Path.Combine(Paths.Mashups, Created), UseShellExecute = false });
    }

    void Add(Control c) => Controls.Add(c);

    void Style(Button b, Point at, int width)
    {
        b.Location = at; b.Size = new Size(width, 30); b.FlatStyle = FlatStyle.Flat; b.ForeColor = Ui.Text; b.BackColor = Ui.Panel;
        b.FlatAppearance.BorderColor = Ui.PanelHi;
        Add(b);
    }

    void Field(string label, int y, TextBox box)
    {
        Add(new Label { Text = label, AutoSize = true, Location = new Point(20, y), ForeColor = Ui.Sub, Font = Ui.Font(8.5f) });
        box.Location = new Point(20, y + 20); box.Size = new Size(520, 24); box.BackColor = Ui.Panel; box.ForeColor = Ui.Text; box.BorderStyle = BorderStyle.FixedSingle;
        Add(box);
    }

    void GamePicker(string label, int y, TextBox dir, ComboBox exe)
    {
        Add(new Label { Text = label, AutoSize = true, Location = new Point(20, y), ForeColor = Ui.Sub, Font = Ui.Font(8.5f) });
        dir.Location = new Point(20, y + 20); dir.Size = new Size(420, 24); dir.ReadOnly = true; dir.BackColor = Ui.Panel; dir.ForeColor = Ui.Text; dir.BorderStyle = BorderStyle.FixedSingle;
        Add(dir);
        var pick = new Button(); pick.Text = "Folder..."; Style(pick, new Point(448, y + 18), 92);
        exe.Location = new Point(20, y + 50); exe.Size = new Size(420, 24); exe.DropDownStyle = ComboBoxStyle.DropDownList; exe.BackColor = Ui.Panel; exe.ForeColor = Ui.Text;
        Add(exe);
        pick.Click += (o, e) =>
        {
            using (var d = new FolderBrowserDialog { Description = "Pick the game's install folder", ShowNewFolderButton = false })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                var exes = Directory.GetFiles(d.SelectedPath, "*.exe").Select(Path.GetFileName)
                    .Where(f => !Regex.IsMatch(f, "(?i)unins|crash|setup|redist|launcher|report|helper|vcredist")).ToArray();
                if (exes.Length == 0) { MessageBox.Show(this, "No game .exe in that folder. Pick the folder the game's .exe is in.", "Create a mashup"); return; }
                dir.Text = d.SelectedPath;
                exe.Items.Clear(); exe.Items.AddRange(exes); exe.SelectedIndex = 0;
            }
        };
    }

    void DoCreate()
    {
        try
        {
            string dir = Scaffold.Create(name.Text, hostDir.Text, hostExe.SelectedItem as string,
                guestDir.Text.Length > 0 ? guestDir.Text : null, guestExe.SelectedItem as string);
            Created = Path.GetFileName(dir);
            create.Enabled = false;
            result.ForeColor = Ui.Green;
            result.Text = "Draft created: " + dir + "\nNext: give PROMPT.md to universal-modder (Claude Code, Codex, Cursor...). When it has built the\nmashup's files, turn it ON from the library.";
            copyPrompt.Visible = openFolder.Visible = true;
            openClaude.Visible = OnPath("claude.cmd") || OnPath("claude.exe") || OnPath("claude");
        }
        catch (Exception ex) { result.ForeColor = Ui.Amber; result.Text = ex.Message; }
    }

    static bool OnPath(string file) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';').Any(p => { try { return File.Exists(Path.Combine(p.Trim(), file)); } catch (Exception) { return false; } });
}
