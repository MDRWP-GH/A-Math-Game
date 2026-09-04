using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

// Obsolete: Setup.exe is produced by installer/A-Math.iss (Inno Setup).
// Do not compile this self-extracting stub — Windows Defender flags the appended-zip layout.

internal static class SetupProgram
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SetupForm());
    }
}

internal sealed class SetupForm : Form
{
    private readonly TextBox _folder;
    private readonly Label _status;
    private readonly Button _install;
    private readonly Button _cancel;
    private bool _busy;

    public SetupForm()
    {
        Ui.StyleForm(this, I18n.T("Install A-Math", "ติดตั้ง A-Math"), new Size(560, 360));

        var title = new Label();
        title.Text = I18n.T("Install A-Math", "ติดตั้ง A-Math");
        title.Font = Theme.TitleFont;
        title.ForeColor = Theme.LightText;
        title.AutoSize = false;
        title.SetBounds(28, 22, 504, 40);

        var body = new Label();
        body.Text = I18n.T(
            "Installs the game on this computer, creates shortcuts, and adds uninstall.exe so you can remove it later.",
            "ติดตั้งเกมลงเครื่อง สร้างทางลัด และใส่ uninstall.exe เพื่อถอนการติดตั้งภายหลัง");
        body.ForeColor = Theme.MutedText;
        body.AutoSize = false;
        body.SetBounds(28, 68, 504, 48);

        var folderLabel = new Label();
        folderLabel.Text = I18n.T("Install folder", "โฟลเดอร์ติดตั้ง");
        folderLabel.ForeColor = Theme.MutedText;
        folderLabel.AutoSize = false;
        folderLabel.SetBounds(28, 124, 504, 22);

        _folder = new TextBox();
        _folder.Text = AppInfo.DefaultInstallDir;
        _folder.BackColor = Theme.Panel;
        _folder.ForeColor = Theme.LightText;
        _folder.BorderStyle = BorderStyle.FixedSingle;
        _folder.SetBounds(28, 148, 392, 32);

        var browse = Ui.CreateButton(I18n.T("Browse…", "เลือก…"), Theme.Secondary, Color.FromArgb(84, 166, 255), 100, 32);
        browse.Location = new Point(432, 148);
        browse.Font = Theme.BodyFont;
        browse.Click += delegate { Browse(); };

        _status = new Label();
        _status.ForeColor = Theme.Primary;
        _status.AutoSize = false;
        _status.SetBounds(28, 196, 504, 32);

        _install = Ui.CreateButton(
            I18n.T("Install", "ติดตั้ง"),
            Theme.Primary,
            Theme.PrimaryHover,
            180,
            48);
        _install.ForeColor = Color.FromArgb(26, 33, 56);
        _install.Location = new Point(108, 260);
        _install.Click += delegate { StartInstall(); };

        _cancel = Ui.CreateButton(
            I18n.T("Cancel", "ยกเลิก"),
            Theme.Quit,
            Theme.QuitHover,
            180,
            48);
        _cancel.Location = new Point(304, 260);
        _cancel.Click += delegate { Close(); };

        Controls.Add(title);
        Controls.Add(body);
        Controls.Add(folderLabel);
        Controls.Add(_folder);
        Controls.Add(browse);
        Controls.Add(_status);
        Controls.Add(_install);
        Controls.Add(_cancel);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy)
            e.Cancel = true;
        base.OnFormClosing(e);
    }

    private void Browse()
    {
        using (var dialog = new FolderBrowserDialog())
        {
            dialog.Description = I18n.T("Choose the install folder", "เลือกโฟลเดอร์ติดตั้ง");
            dialog.SelectedPath = Directory.Exists(_folder.Text) ? _folder.Text : AppInfo.DefaultInstallDir;
            if (dialog.ShowDialog(this) == DialogResult.OK)
                _folder.Text = Path.Combine(dialog.SelectedPath, AppInfo.ProductName);
        }
    }

    private void StartInstall()
    {
        if (_busy)
            return;

        string sourceDir = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
        if (!PackedPayload.HasPayload() && !File.Exists(Path.Combine(sourceDir, AppInfo.ExeName)))
        {
            MessageBox.Show(
                this,
                I18n.T(
                    "This Setup.exe has no game files. Build a Windows player from Unity to create the installer.",
                    "Setup.exe นี้ไม่มีไฟล์เกมในตัว ให้ Build เกม Windows จาก Unity เพื่อสร้างตัวติดตั้ง"),
                AppInfo.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        string destDir;
        try
        {
            destDir = Path.GetFullPath(_folder.Text.Trim());
        }
        catch
        {
            MessageBox.Show(this, I18n.T("That folder path is not valid.", "พาธโฟลเดอร์ไม่ถูกต้อง"), AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _busy = true;
        _install.Enabled = false;
        _cancel.Enabled = false;
        _folder.Enabled = false;
        _status.Text = I18n.T("Installing…", "กำลังติดตั้ง…");

        var thread = new Thread(delegate ()
        {
            string error = null;
            try
            {
                Installer.Run(sourceDir, destDir, delegate (string message)
                {
                    BeginInvoke(new Action(delegate { _status.Text = message; }));
                });
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            BeginInvoke(new Action(delegate
            {
                _busy = false;
                if (error != null)
                {
                    _status.ForeColor = Theme.QuitHover;
                    _status.Text = error;
                    _install.Enabled = true;
                    _cancel.Enabled = true;
                    _folder.Enabled = true;
                    return;
                }

                var result = MessageBox.Show(
                    this,
                    I18n.T(
                        "A-Math is installed. Launch now?",
                        "ติดตั้ง A-Math เรียบร้อยแล้ว เปิดเกมเลยไหม?"),
                    AppInfo.ProductName,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);
                if (result == DialogResult.Yes)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(Path.Combine(destDir, AppInfo.ExeName))
                        {
                            WorkingDirectory = destDir,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, ex.Message, AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }

                Application.Exit();
            }));
        });
        thread.IsBackground = true;
        thread.Start();
    }
}

internal static class Installer
{
    public static void Run(string sourceDir, string destDir, Action<string> report)
    {
        if (report == null)
            report = delegate { };

        sourceDir = Path.GetFullPath(sourceDir).TrimEnd('\\', '/');
        destDir = Path.GetFullPath(destDir).TrimEnd('\\', '/');

        Directory.CreateDirectory(destDir);

        if (PackedPayload.HasPayload())
        {
            report(I18n.T("Extracting files…", "กำลังแตกไฟล์…"));
            PackedPayload.ExtractTo(destDir, report);
        }
        else
        {
            bool sameDir = string.Equals(sourceDir, destDir, StringComparison.OrdinalIgnoreCase);
            if (!sameDir)
            {
                if (destDir.StartsWith(sourceDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(I18n.T(
                        "Choose a folder outside the game build folder.",
                        "เลือกโฟลเดอร์ที่อยู่นอกโฟลเดอร์บิลด์ของเกม"));

                report(I18n.T("Copying files…", "กำลังคัดลอกไฟล์…"));
                CopyDirectory(sourceDir, destDir);
            }
        }

        string gameExe = Path.Combine(destDir, AppInfo.ExeName);
        if (!File.Exists(gameExe))
        {
            throw new InvalidOperationException(I18n.T(
                "The installer did not contain " + AppInfo.ExeName + ".",
                "ตัวติดตั้งไม่มีไฟล์ " + AppInfo.ExeName));
        }

        string uninstallExe = Path.Combine(destDir, AppInfo.UninstallExeName);
        if (!File.Exists(uninstallExe))
        {
            string bundled = Path.Combine(sourceDir, AppInfo.UninstallExeName);
            if (File.Exists(bundled))
                File.Copy(bundled, uninstallExe, true);
        }

        if (!File.Exists(uninstallExe))
        {
            throw new InvalidOperationException(I18n.T(
                "The installer did not contain " + AppInfo.UninstallExeName + ".",
                "ตัวติดตั้งไม่มีไฟล์ " + AppInfo.UninstallExeName));
        }

        report(I18n.T("Creating shortcuts…", "กำลังสร้างทางลัด…"));
        Shortcuts.Create(AppInfo.DesktopShortcutPath, gameExe, string.Empty, destDir, gameExe);
        Shortcuts.Create(AppInfo.StartMenuShortcutPath, gameExe, string.Empty, destDir, gameExe);
        if (File.Exists(uninstallExe))
            Shortcuts.Create(AppInfo.StartMenuUninstallShortcutPath, uninstallExe, string.Empty, destDir, uninstallExe);

        report(I18n.T("Registering uninstaller…", "กำลังลงทะเบียนตัวถอนการติดตั้ง…"));
        RegistryInstall.Write(destDir);

        report(I18n.T("Configuring Windows Firewall…", "กำลังตั้งค่า Windows Firewall…"));
        WindowsFirewallHelper.EnsureRules(destDir);
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (string file in Directory.GetFiles(source))
        {
            string name = Path.GetFileName(file);
            if (string.Equals(name, AppInfo.SetupExeName, StringComparison.OrdinalIgnoreCase))
                continue;
            File.Copy(file, Path.Combine(dest, name), true);
        }

        foreach (string dir in Directory.GetDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }
    }
}
