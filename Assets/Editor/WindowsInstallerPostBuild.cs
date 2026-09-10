using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// After a Windows player build, compiles an Inno Setup installer and leaves
/// only Setup.exe (plus Setup.exe.sha256) in the output folder.
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
        try
        {
            if (!Application.isBatchMode)
                EditorUtility.DisplayProgressBar("A-Math installer", "Compiling Setup.exe with Inno Setup…", 0.35f);

            if (!WindowsInstallerBuilder.TryCompileInnoSetup(projectRoot, outputDir, PlayerSettings.bundleVersion, out string error))
            {
                Debug.LogError("A-Math Windows installer: " + error);
                return;
            }

            Debug.Log("A-Math Windows installer: wrote Setup.exe to " + outputDir);
        }
        finally
        {
            if (!Application.isBatchMode)
                EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("A-Math/Windows/Build Player and Setup.exe")]
    private static void BuildPlayerFromMenu()
    {
        BuildReport report = WindowsPlayerBuilder.Build();
        bool ok = report != null && report.summary.result == BuildResult.Succeeded;
        EditorUtility.DisplayDialog(
            "A-Math Windows build",
            ok ? "Built Setup.exe in Build/Windows." : "Windows build failed. See the Console.",
            "OK");
    }
}

internal static class WindowsInstallerBuilder
{
    public static bool TryCompileInnoSetup(string projectRoot, string outputDir, string appVersion, out string error)
    {
        error = null;
        outputDir = Path.GetFullPath(outputDir);
        string installerDir = Path.Combine(projectRoot, "installer");
        string scriptPath = Path.Combine(installerDir, "A-Math.iss");
        string gameExe = Path.Combine(outputDir, "A-Math.exe");

        string iscc = FindIscc();
        if (iscc == null)
        {
            error = "Inno Setup compiler (ISCC.exe) was not found. Install Inno Setup 6.5 or later from https://jrsoftware.org/isdl.php "
                    + "and keep ISCC.exe on PATH, or in Program Files\\Inno Setup 6. Windows builds will not produce Setup.exe until then.";
            return false;
        }

        if (!File.Exists(scriptPath))
        {
            error = "Missing installer script: " + scriptPath;
            return false;
        }

        if (!File.Exists(gameExe))
        {
            error = "A-Math.exe not found in " + outputDir;
            return false;
        }

        string version = string.IsNullOrWhiteSpace(appVersion) ? "1.0" : appVersion.Trim();
        string packedDir = Path.Combine(Path.GetTempPath(), "amath-inno-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(packedDir);

        try
        {
            var info = new ProcessStartInfo
            {
                FileName = iscc,
                WorkingDirectory = installerDir,
                Arguments = BuildIsccArguments(scriptPath, outputDir, packedDir, version),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(info))
            {
                if (process == null)
                {
                    error = "Failed to start ISCC.exe.";
                    return false;
                }

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    error = "ISCC.exe failed:\n" + stdout + stderr;
                    return false;
                }
            }

            string packedSetup = Path.Combine(packedDir, "Setup.exe");
            if (!File.Exists(packedSetup))
            {
                error = "ISCC.exe finished but Setup.exe was not created in " + packedDir;
                return false;
            }

            string hashText;
            if (!TryBuildSha256Line(packedSetup, out hashText, out error))
                return false;

            string setupTemp = Path.Combine(Path.GetTempPath(), "amath-setup-out-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(packedSetup, setupTemp, true);
            ClearDirectoryLeavingNothing(outputDir);
            string finalSetup = Path.Combine(outputDir, "Setup.exe");
            File.Copy(setupTemp, finalSetup, true);
            File.WriteAllText(Path.Combine(outputDir, "Setup.exe.sha256"), hashText, new UTF8Encoding(false));
            TryDeleteFile(setupTemp);
            return true;
        }
        catch (Exception ex)
        {
            error = "Failed to compile Setup.exe: " + ex.Message;
            return false;
        }
        finally
        {
            TryDeleteDirectory(packedDir);
        }
    }

    private static string BuildIsccArguments(string scriptPath, string sourceDir, string outputDir, string appVersion)
    {
        return Quote("/DSourceDir=" + ToInnoPath(sourceDir))
               + " " + Quote("/DOutputDir=" + ToInnoPath(outputDir))
               + " " + Quote("/DAppVersion=" + appVersion)
               + " " + Quote(ToInnoPath(scriptPath));
    }

    private static string ToInnoPath(string path)
    {
        return Path.GetFullPath(path).TrimEnd('\\', '/').Replace('\\', '/');
    }

    private static bool TryBuildSha256Line(string setupPath, out string line, out string error)
    {
        line = null;
        error = null;
        try
        {
            byte[] hash;
            using (var sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(setupPath))
                hash = sha.ComputeHash(stream);

            var hex = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                hex.Append(hash[i].ToString("x2"));

            line = hex + "  Setup.exe" + Environment.NewLine;
            return true;
        }
        catch (Exception ex)
        {
            error = "Failed to hash Setup.exe: " + ex.Message;
            return false;
        }
    }

    private static string FindIscc()
    {
        string fromPath = FindOnPath("ISCC.exe") ?? FindOnPath("iscc.exe");
        if (fromPath != null)
            return fromPath;

        var folders = new List<string>();
        AddIfNotEmpty(folders, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        AddIfNotEmpty(folders, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));

        foreach (string root in folders)
        {
            string candidate = Path.Combine(root, "Inno Setup 6", "ISCC.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            string wingetCandidate = Path.Combine(localAppData, "Programs", "Inno Setup 6", "ISCC.exe");
            if (File.Exists(wingetCandidate))
                return wingetCandidate;
        }

        return null;
    }

    private static string FindOnPath(string fileName)
    {
        string path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
            return null;

        foreach (string dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir))
                continue;
            string candidate = Path.Combine(dir.Trim().Trim('"'), fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static void AddIfNotEmpty(List<string> list, string value)
    {
        if (!string.IsNullOrEmpty(value))
            list.Add(value);
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static void ClearDirectoryLeavingNothing(string outputDir)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            foreach (string file in Directory.GetFiles(outputDir))
                TryDeleteFile(file);
            foreach (string dir in Directory.GetDirectories(outputDir))
                TryDeleteDirectory(dir);

            if (Directory.GetFiles(outputDir).Length == 0 && Directory.GetDirectories(outputDir).Length == 0)
                return;

            System.Threading.Thread.Sleep(250);
        }

        Debug.LogWarning("A-Math Windows installer: some player files could not be deleted from " + outputDir);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                return;

            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); }
                catch { }
            }

            Directory.Delete(path, true);
        }
        catch
        {
        }
    }
}

internal static class WindowsPlayerBuilder
{
    public static void BuildFromCli()
    {
        BuildReport report = Build();
        bool ok = report != null && report.summary.result == BuildResult.Succeeded;
        EditorApplication.Exit(ok ? 0 : 1);
    }

    public static BuildReport Build()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string outputExe = Path.Combine(projectRoot, "Build", "Windows", "A-Math.exe");
        string outputDir = Path.GetDirectoryName(outputExe);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        var scenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                scenes.Add(scene.path);
        }

        if (scenes.Count == 0)
        {
            Debug.LogError("[A-Math] Windows build aborted: enable at least one scene in Build Settings.");
            return null;
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = outputExe,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.CompressWithLz4HC
        };
        return BuildPipeline.BuildPlayer(options);
    }
}
