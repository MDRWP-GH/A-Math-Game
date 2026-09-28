using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace AMath.Tests
{
    public sealed class InstallerSourceTests
    {
        [Test]
        public void Uninstaller_DeletesPlayerDataOnlyAfterMainUninstallStep()
        {
            string script = File.ReadAllText(ProjectPath("installer", "A-Math.iss"));

            StringAssert.Contains("CurUninstallStep = usPostUninstall", script);
            StringAssert.DoesNotContain("CurUninstallStep = usUninstall then", script);
            StringAssert.Contains("DelTree(ExpandConstant('{app}\\save')", script);
            StringAssert.Contains("Excludes: \"save\\*", script);
        }

        [Test]
        public void Installer_AlwaysOffersDestinationDirectory()
        {
            string script = File.ReadAllText(ProjectPath("installer", "A-Math.iss"));

            StringAssert.Contains("DisableDirPage=no", script);
        }

        [Test]
        public void Installer_DesktopShortcutIsOptionalAndOffByDefault()
        {
            string script = File.ReadAllText(ProjectPath("installer", "A-Math.iss"));

            StringAssert.Contains("Name: \"desktopicon\"; Description: \"{cm:CreateDesktopIcon}\"; GroupDescription: \"{cm:AdditionalIcons}\"; Flags: unchecked", script);
            StringAssert.Contains("Name: \"{autodesktop}\\{#MyAppName}\"; Filename: \"{app}\\{#MyAppExeName}\"; Tasks: desktopicon; Check: not WizardNoIcons", script);
        }

        [Test]
        public void WindowsPostBuild_FailsClosedAndVerifiesArtifacts()
        {
            string source = File.ReadAllText(ProjectPath("Assets", "Editor", "WindowsInstallerPostBuild.cs"));

            StringAssert.Contains("throw new BuildFailedException", source);
            StringAssert.Contains("TryValidateInstallerArtifacts", source);
            StringAssert.Contains("RemoveInstallerArtifacts", source);
            StringAssert.Contains("Path.GetFileName(dir), \"save\"", source);
        }

        [Test]
        public void ManualInstallerBuild_UsesProjectVersionInsteadOfStaleFallback()
        {
            string script = File.ReadAllText(ProjectPath("installer", "A-Math.iss"));
            string batch = File.ReadAllText(ProjectPath("installer", "build.bat"));

            StringAssert.Contains("#error AppVersion must be defined", script);
            StringAssert.Contains("bundleVersion:", batch);
            StringAssert.Contains("/DAppVersion=%APP_VERSION%", batch);
        }

        private static string ProjectPath(params string[] parts)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            foreach (string part in parts)
                path = Path.Combine(path, part);
            return path;
        }
    }
}
