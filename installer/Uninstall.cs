using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

// Obsolete: uninstall is handled by the Inno Setup uninstaller from installer/A-Math.iss.

internal static class UninstallProgram
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        bool silent = false;
        foreach (string arg in args)
        {
            if (string.Equals(arg, "/S", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "/silent", StringComparison.OrdinalIgnoreCase))
                silent = true;
        }

        if (silent)
        {
            try
            {
                string dir = Uninstaller.ThisDirectory();
                if (Uninstaller.LooksLikeGameFolder(dir))
                    Uninstaller.Run(dir, true, delegate { });
            }
            catch
            {
            }
            return;
        }

        Application.Run(new UninstallForm());
    }
}

internal sealed class UninstallForm : Form
{
    private readonly CheckBox _deleteData;
    private readonly Label _status;
    private readonly Button _uninstall;
    private readonly Button _cancel;
    private bool _busy;

    public UninstallForm()
    {
        Ui.StyleForm(this, I18n.T("Uninstall A-Math", "ถอนการติดตั้ง A-Math"), new Size(520, 340));

        var title = new Label();
        title.Text = I18n.T("Remove A-Math from this computer?", "ต้องการถอนการติดตั้ง A-Math จากเครื่องนี้หรือไม่?");
        title.Font = Theme.TitleFont;
        title.ForeColor = Theme.LightText;
        title.AutoSize = false;
        title.SetBounds(28, 24, 464, 70);

        var body = new Label();
        body.Text = I18n.T(
            "This removes the game files, Start Menu / desktop shortcuts, and the Apps & Features entry.",
            "จะลบไฟล์เกม ทางลัดบนเดสก์ท็อป / เมนูเริ่ม และรายการใน แอปและคุณลักษณะ");
        body.ForeColor = Theme.MutedText;
        body.AutoSize = false;
        body.SetBounds(28, 96, 464, 48);

        _deleteData = new CheckBox();
        _deleteData.Text = I18n.T(
            "Also delete saves, accounts, match history, and settings",
            "ลบเซฟเกม บัญชีผู้ใช้ ประวัติการแข่งขัน และการตั้งค่าด้วย");
        _deleteData.Checked = true;
        _deleteData.ForeColor = Theme.LightText;
        _deleteData.AutoSize = false;
        _deleteData.SetBounds(28, 152, 464, 36);

        _status = new Label();
        _status.ForeColor = Theme.Primary;
        _status.AutoSize = false;
        _status.SetBounds(28, 198, 464, 28);

        _uninstall = Ui.CreateButton(
            I18n.T("Uninstall", "ถอนการติดตั้ง"),
            Theme.Quit,
            Theme.QuitHover,
            180,
            48);
        _uninstall.Location = new Point(88, 250);
        _uninstall.Click += delegate { StartUninstall(); };

        _cancel = Ui.CreateButton(
            I18n.T("Cancel", "ยกเลิก"),
            Theme.Secondary,
            Color.FromArgb(84, 166, 255),
            180,
            48);
        _cancel.Location = new Point(284, 250);
        _cancel.Click += delegate { Close(); };

        Controls.Add(title);
        Controls.Add(body);
        Controls.Add(_deleteData);
        Controls.Add(_status);
        Controls.Add(_uninstall);
        Controls.Add(_cancel);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy)
            e.Cancel = true;
        base.OnFormClosing(e);
    }

    private void StartUninstall()
    {
        if (_busy)
            return;

        _busy = true;
        _uninstall.Enabled = false;
        _cancel.Enabled = false;
        _deleteData.Enabled = false;
        _status.Text = I18n.T("Uninstalling…", "กำลังถอนการติดตั้ง…");

        bool deleteData = _deleteData.Checked;
        string installDir = Uninstaller.ThisDirectory();
        if (!Uninstaller.LooksLikeGameFolder(installDir))
        {
            MessageBox.Show(
                this,
                I18n.T(
                    "uninstall.exe must sit next to A-Math.exe.",
                    "ต้องวาง uninstall.exe ไว้ในโฟลเดอร์เดียวกับ A-Math.exe"),
                AppInfo.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _busy = false;
            _uninstall.Enabled = true;
            _cancel.Enabled = true;
            _deleteData.Enabled = true;
            _status.Text = string.Empty;
            return;
        }

        var thread = new Thread(delegate ()
        {
            string error = null;
            try
            {
                Uninstaller.Run(installDir, deleteData, delegate (string message)
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
                    _uninstall.Enabled = true;
                    _cancel.Enabled = true;
                    _deleteData.Enabled = true;
                    return;
                }

                MessageBox.Show(
                    this,
                    I18n.T("A-Math was uninstalled.", "ถอนการติดตั้ง A-Math เรียบร้อยแล้ว"),
                    AppInfo.ProductName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                _busy = false;
                Application.Exit();
            }));
        });
        thread.IsBackground = true;
        thread.Start();
    }
}

internal static class Uninstaller
{
    public static string ThisDirectory()
    {
        return Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
    }

    public static bool LooksLikeGameFolder(string installDir)
    {
        return File.Exists(Path.Combine(installDir, AppInfo.ExeName));
    }

    public static void Run(string installDir, bool deleteUserData, Action<string> report)
    {
        if (report == null)
            report = delegate { };

        report(I18n.T("Closing A-Math if it is running…", "กำลังปิดเกมถ้ายังเปิดอยู่…"));
        KillGame();

        report(I18n.T("Removing shortcuts…", "กำลังลบทางลัด…"));
        Shortcuts.TryDelete(AppInfo.DesktopShortcutPath);
        Shortcuts.TryDelete(AppInfo.StartMenuShortcutPath);
        Shortcuts.TryDelete(AppInfo.StartMenuUninstallShortcutPath);
        TryDeleteDirectory(AppInfo.StartMenuFolder);

        report(I18n.T("Removing Apps & Features entry…", "กำลังลบรายการถอนการติดตั้ง…"));
        RegistryInstall.Remove();

        report(I18n.T("Removing firewall rules…", "กำลังลบกฎ Windows Firewall…"));
        WindowsFirewallHelper.RemoveRules();

        if (deleteUserData)
        {
            report(I18n.T("Deleting saved data…", "กำลังลบข้อมูลที่บันทึกไว้…"));
            TryDeleteDirectory(AppInfo.PersistentDataPath);
            TryDeletePlayerPrefs();
        }

        report(I18n.T("Removing game files…", "กำลังลบไฟล์เกม…"));
        ScheduleDirectoryDelete(installDir);
    }

    private static void KillGame()
    {
        string name = Path.GetFileNameWithoutExtension(AppInfo.ExeName);
        foreach (Process process in Process.GetProcessesByName(name))
        {
            try
            {
                process.Kill();
                process.WaitForExit(4000);
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void TryDeletePlayerPrefs()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(AppInfo.PlayerPrefsKey, false);
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
        }
    }

    private static void ScheduleDirectoryDelete(string installDir)
    {
        string fullDir = Path.GetFullPath(installDir).TrimEnd('\\', '/');
        string batchPath = Path.Combine(Path.GetTempPath(), "amath-uninstall-" + Guid.NewGuid().ToString("N") + ".bat");
        string batch =
            "@echo off" + Environment.NewLine +
            "ping 127.0.0.1 -n 3 > nul" + Environment.NewLine +
            "rd /s /q \"" + fullDir + "\"" + Environment.NewLine +
            "if exist \"" + fullDir + "\" rd /s /q \"" + fullDir + "\"" + Environment.NewLine +
            "del \"%~f0\"" + Environment.NewLine;
        File.WriteAllText(batchPath, batch);

        var info = new ProcessStartInfo();
        info.FileName = batchPath;
        info.CreateNoWindow = true;
        info.UseShellExecute = false;
        info.WindowStyle = ProcessWindowStyle.Hidden;
        Process.Start(info);
    }
}
