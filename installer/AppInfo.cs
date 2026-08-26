using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class AppInfo
{
    public const string ProductName = "A-Math";
    public const string CompanyName = "DefaultCompany";
    public const string Version = "1.0";
    public const string Publisher = "A-Math";
    public const string ExeName = "A-Math.exe";
    public const string UninstallExeName = "uninstall.exe";
    public const string SetupExeName = "Setup.exe";
    public const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\A-Math";

    public static string DefaultInstallDir
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                ProductName);
        }
    }

    public static string PersistentDataPath
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData",
                "LocalLow",
                CompanyName,
                ProductName);
        }
    }

    public static string PlayerPrefsKey
    {
        get { return @"Software\" + CompanyName + @"\" + ProductName; }
    }

    public static string DesktopShortcutPath
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                ProductName + ".lnk");
        }
    }

    public static string StartMenuFolder
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                ProductName);
        }
    }

    public static string StartMenuShortcutPath
    {
        get { return Path.Combine(StartMenuFolder, ProductName + ".lnk"); }
    }

    public static string StartMenuUninstallShortcutPath
    {
        get { return Path.Combine(StartMenuFolder, "Uninstall " + ProductName + ".lnk"); }
    }
}

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(13, 20, 51);
    public static readonly Color Panel = Color.FromArgb(28, 41, 87);
    public static readonly Color Primary = Color.FromArgb(250, 148, 41);
    public static readonly Color PrimaryHover = Color.FromArgb(255, 178, 69);
    public static readonly Color Quit = Color.FromArgb(156, 71, 105);
    public static readonly Color QuitHover = Color.FromArgb(196, 97, 133);
    public static readonly Color Secondary = Color.FromArgb(51, 135, 224);
    public static readonly Color LightText = Color.FromArgb(240, 247, 255);
    public static readonly Color MutedText = Color.FromArgb(176, 196, 235);

    public static readonly Font TitleFont = new Font("Segoe UI", 18f, FontStyle.Bold);
    public static readonly Font BodyFont = new Font("Segoe UI", 10.5f, FontStyle.Regular);
    public static readonly Font ButtonFont = new Font("Segoe UI", 11f, FontStyle.Bold);
}

internal static class I18n
{
    private static readonly bool Thai =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "th";

    public static string T(string en, string th)
    {
        return Thai ? th : en;
    }
}

internal static class Ui
{
    public static Button CreateButton(string text, Color back, Color hover, int width, int height)
    {
        var button = new Button();
        button.Text = text;
        button.Width = width;
        button.Height = height;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = back;
        button.ForeColor = Theme.LightText;
        button.Font = Theme.ButtonFont;
        button.Cursor = Cursors.Hand;
        button.TabStop = false;
        button.MouseEnter += delegate { button.BackColor = hover; };
        button.MouseLeave += delegate { button.BackColor = back; };
        return button;
    }

    public static void StyleForm(Form form, string title, Size size)
    {
        form.Text = title;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.StartPosition = FormStartPosition.CenterScreen;
        form.ClientSize = size;
        form.BackColor = Theme.Background;
        form.ForeColor = Theme.LightText;
        form.Font = Theme.BodyFont;
        form.ShowInTaskbar = true;
    }
}

internal static class Shortcuts
{
    public static void Create(string shortcutPath, string targetPath, string arguments, string workingDir, string iconPath)
    {
        string dir = Path.GetDirectoryName(shortcutPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(shellType);
        object shortcut = shellType.InvokeMember(
            "CreateShortcut",
            System.Reflection.BindingFlags.InvokeMethod,
            null,
            shell,
            new object[] { shortcutPath });

        Type shortcutType = shortcut.GetType();
        shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
        shortcutType.InvokeMember("Arguments", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { arguments ?? string.Empty });
        shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { workingDir ?? string.Empty });
        shortcutType.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { iconPath ?? targetPath });
        shortcutType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}

internal static class RegistryInstall
{
    public static void Write(string installDir)
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(AppInfo.UninstallRegKey))
        {
            if (key == null)
                return;

            string uninstallPath = Path.Combine(installDir, AppInfo.UninstallExeName);
            string exePath = Path.Combine(installDir, AppInfo.ExeName);
            key.SetValue("DisplayName", AppInfo.ProductName);
            key.SetValue("DisplayVersion", AppInfo.Version);
            key.SetValue("Publisher", AppInfo.Publisher);
            key.SetValue("InstallLocation", installDir);
            key.SetValue("DisplayIcon", exePath);
            key.SetValue("UninstallString", "\"" + uninstallPath + "\"");
            key.SetValue("QuietUninstallString", "\"" + uninstallPath + "\" /S");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("EstimatedSize", EstimateSizeKb(installDir), RegistryValueKind.DWord);
        }
    }

    public static void Remove()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(AppInfo.UninstallRegKey, false);
        }
        catch
        {
        }
    }

    public static string ReadInstallLocation()
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AppInfo.UninstallRegKey))
        {
            if (key == null)
                return null;
            return key.GetValue("InstallLocation") as string;
        }
    }

    private static int EstimateSizeKb(string dir)
    {
        try
        {
            long bytes = 0;
            foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                try { bytes += new FileInfo(file).Length; }
                catch { }
            }
            return (int)Math.Max(1, bytes / 1024);
        }
        catch
        {
            return 1;
        }
    }
}
