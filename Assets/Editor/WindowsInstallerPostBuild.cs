using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// After a Windows player build, compiles and copies Setup.exe / uninstall.exe
/// next to A-Math.exe so the shipped folder can be installed and removed.
/// </summary>
internal sealed class WindowsInstallerPostBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 100;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows &&
            report.summary.platform != BuildTarget.StandaloneWindows64)
            return;

        string outputDir = Path.GetDirectoryName(report.summary.outputPath);
        if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
            return;

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (!WindowsInstallerBuilder.TryBuild(projectRoot, out string error))
        {
            Debug.LogWarning("A-Math Windows installer: " + error);
            return;
        }

        CopyIfExists(Path.Combine(projectRoot, "installer", "uninstall.exe"), Path.Combine(outputDir, "uninstall.exe"));
        CopyIfExists(Path.Combine(projectRoot, "installer", "Setup.exe"), Path.Combine(outputDir, "Setup.exe"));
        Debug.Log("A-Math Windows installer: copied Setup.exe and uninstall.exe to " + outputDir);
    }

    [MenuItem("A-Math/Windows/Build Setup and Uninstall")]
    private static void BuildFromMenu()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (!WindowsInstallerBuilder.TryBuild(projectRoot, out string error))
        {
            EditorUtility.DisplayDialog("A-Math installer", error, "OK");
            return;
        }

        EditorUtility.DisplayDialog(
            "A-Math installer",
            "Built installer/Setup.exe and installer/uninstall.exe.\nThey are also copied into a Windows player build automatically.",
            "OK");
    }

    private static void CopyIfExists(string source, string dest)
    {
        if (!File.Exists(source))
            return;
        File.Copy(source, dest, true);
    }
}

internal static class WindowsInstallerBuilder
{
    public static bool TryBuild(string projectRoot, out string error)
    {
        error = null;
        string installerDir = Path.Combine(projectRoot, "installer");
        string csc = FindCsc();
        if (csc == null)
        {
            error = "csc.exe not found. Build the Windows player on a machine with .NET Framework 4.x.";
            return false;
        }

        if (!Compile(csc, installerDir, "uninstall.exe", "Uninstall.cs", "AppInfo.cs", out error))
            return false;
        if (!Compile(csc, installerDir, "Setup.exe", "Setup.cs", "AppInfo.cs", out error))
            return false;
        return true;
    }

    private static bool Compile(string csc, string installerDir, string outputName, string sourceA, string sourceB, out string error)
    {
        error = null;
        var info = new ProcessStartInfo
        {
            FileName = csc,
            WorkingDirectory = installerDir,
            Arguments = "/nologo /target:winexe /optimize+ /out:" + outputName +
                        " /reference:System.Windows.Forms.dll /reference:System.Drawing.dll " +
                        sourceA + " " + sourceB,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (var process = Process.Start(info))
        {
            if (process == null)
            {
                error = "Failed to start csc.exe.";
                return false;
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode == 0)
                return true;

            error = "Failed to compile " + outputName + ":\n" + stdout + stderr;
            return false;
        }
    }

    private static string FindCsc()
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string[] candidates =
        {
            Path.Combine(windows, @"Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
            Path.Combine(windows, @"Microsoft.NET\Framework\v4.0.30319\csc.exe")
        };

        foreach (string path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }
}
