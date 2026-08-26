using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// After a Windows player build, packs the game and uninstall.exe into a single
/// self-extracting Setup.exe and removes the unpacked player files from the output folder.
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
            EditorUtility.DisplayProgressBar("A-Math installer", "Compiling Setup.exe…", 0.1f);
            if (!WindowsInstallerBuilder.TryBuild(projectRoot, out string error))
            {
                Debug.LogWarning("A-Math Windows installer: " + error);
                return;
            }

            EditorUtility.DisplayProgressBar("A-Math installer", "Packing game into Setup.exe…", 0.35f);
            if (!WindowsInstallerBuilder.TryPackSelfExtractingSetup(projectRoot, outputDir, out error))
            {
                Debug.LogWarning("A-Math Windows installer: " + error);
                CopyIfExists(Path.Combine(projectRoot, "installer", "uninstall.exe"), Path.Combine(outputDir, "uninstall.exe"));
                CopyIfExists(Path.Combine(projectRoot, "installer", "Setup.exe"), Path.Combine(outputDir, "Setup.exe"));
                return;
            }

            Debug.Log("A-Math Windows installer: wrote self-extracting Setup.exe to " + outputDir);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
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
            "Built installer stubs in installer/.\nA Windows player build packs the game into a single Setup.exe automatically.",
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
    private const string PayloadMagic = "AMTHZIP1";
    private const int PayloadFooterSize = 16;

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

        if (!Compile(csc, installerDir, "uninstall.exe", null, out error, "Uninstall.cs", "AppInfo.cs"))
            return false;

        string compressionDll = Path.Combine(Path.GetDirectoryName(csc) ?? string.Empty, "System.IO.Compression.dll");
        if (!File.Exists(compressionDll))
        {
            error = "System.IO.Compression.dll not found next to csc.exe.";
            return false;
        }

        if (!Compile(
                csc,
                installerDir,
                "Setup.exe",
                "/reference:\"" + compressionDll + "\"",
                out error,
                "Setup.cs",
                "PackedPayload.cs",
                "AppInfo.cs"))
            return false;

        return true;
    }

    public static bool TryPackSelfExtractingSetup(string projectRoot, string outputDir, out string error)
    {
        error = null;
        outputDir = Path.GetFullPath(outputDir);
        string installerDir = Path.Combine(projectRoot, "installer");
        string stubPath = Path.Combine(installerDir, "Setup.exe");
        string uninstallPath = Path.Combine(installerDir, "uninstall.exe");
        string gameExe = Path.Combine(outputDir, "A-Math.exe");

        if (!File.Exists(stubPath) || !File.Exists(uninstallPath))
        {
            error = "Setup.exe / uninstall.exe stubs were not compiled.";
            return false;
        }

        if (!File.Exists(gameExe))
        {
            error = "A-Math.exe not found in " + outputDir;
            return false;
        }

        File.Copy(uninstallPath, Path.Combine(outputDir, "uninstall.exe"), true);

        string zipPath = Path.Combine(Path.GetTempPath(), "amath-payload-" + Guid.NewGuid().ToString("N") + ".zip");
        string packedPath = Path.Combine(Path.GetTempPath(), "amath-setup-" + Guid.NewGuid().ToString("N") + ".exe");
        try
        {
            CreatePayloadZip(outputDir, zipPath);
            AppendPayload(stubPath, zipPath, packedPath);
            ClearDirectoryLeavingNothing(outputDir);
            File.Copy(packedPath, Path.Combine(outputDir, "Setup.exe"), true);
            return true;
        }
        catch (Exception ex)
        {
            error = "Failed to pack Setup.exe: " + ex.Message;
            return false;
        }
        finally
        {
            TryDeleteFile(zipPath);
            TryDeleteFile(packedPath);
        }
    }

    private static void CreatePayloadZip(string outputDir, string zipPath)
    {
        var files = new List<string>();
        foreach (string file in Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories))
        {
            if (ShouldSkipPayloadFile(outputDir, file))
                continue;
            files.Add(file);
        }

        if (files.Count == 0)
            throw new InvalidOperationException("No game files were found to pack.");

        using (FileStream zipStream = File.Create(zipPath))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            for (int i = 0; i < files.Count; i++)
            {
                string file = files[i];
                string relative = MakeRelative(outputDir, file).Replace('\\', '/');
                EditorUtility.DisplayProgressBar(
                    "A-Math installer",
                    "Packing " + relative,
                    0.35f + (0.5f * (i + 1) / files.Count));

                ZipArchiveEntry entry = archive.CreateEntry(relative, System.IO.Compression.CompressionLevel.Optimal);
                using (Stream entryStream = entry.Open())
                using (FileStream input = File.OpenRead(file))
                    input.CopyTo(entryStream);
            }
        }
    }

    private static void AppendPayload(string stubPath, string zipPath, string packedPath)
    {
        using (FileStream output = File.Create(packedPath))
        {
            using (FileStream stub = File.OpenRead(stubPath))
                stub.CopyTo(output);

            long payloadOffset = output.Position;
            using (FileStream zip = File.OpenRead(zipPath))
                zip.CopyTo(output);

            byte[] offsetBytes = BitConverter.GetBytes(payloadOffset);
            output.Write(offsetBytes, 0, 8);
            byte[] magic = Encoding.ASCII.GetBytes(PayloadMagic);
            if (magic.Length != 8)
                throw new InvalidOperationException("Payload magic must be 8 bytes.");
            output.Write(magic, 0, 8);
            if (PayloadFooterSize != 16)
                throw new InvalidOperationException("Payload footer must be 16 bytes.");
        }
    }

    private static bool ShouldSkipPayloadFile(string outputDir, string filePath)
    {
        string name = Path.GetFileName(filePath);
        if (name.Equals("Setup.exe", StringComparison.OrdinalIgnoreCase))
            return true;

        string relative = MakeRelative(outputDir, filePath);
        string[] parts = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            if (part.IndexOf("DoNotShip", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (part.IndexOf("DontShipItWithYourGame", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static string MakeRelative(string root, string path)
    {
        string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                          + Path.DirectorySeparatorChar;
        string pathFull = Path.GetFullPath(path);
        if (!pathFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path is outside the build folder: " + path);
        return pathFull.Substring(rootFull.Length);
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

    private static bool Compile(string csc, string installerDir, string outputName, string extraArgs, out string error, params string[] sources)
    {
        error = null;
        string sourceArgs = string.Join(" ", sources);
        string extra = string.IsNullOrEmpty(extraArgs) ? string.Empty : extraArgs + " ";
        var info = new ProcessStartInfo
        {
            FileName = csc,
            WorkingDirectory = installerDir,
            Arguments = "/nologo /target:winexe /optimize+ /out:" + outputName +
                        " /reference:System.Windows.Forms.dll /reference:System.Drawing.dll " +
                        extra + sourceArgs,
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
