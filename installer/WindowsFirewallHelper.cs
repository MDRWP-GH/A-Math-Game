using System;
using System.Diagnostics;
using System.IO;

// Obsolete: the shipping installer does not add firewall rules (Defender false-positive).

internal static class WindowsFirewallHelper
{
    public const string DiscoveryRuleName = "A-Math LAN Discovery";
    public const string GameRuleName = "A-Math LAN Game";
    public const int DiscoveryPort = 47777;
    public const int GamePort = 7778;

    public static void EnsureRules(string installDir)
    {
        if (!IsWindows() || string.IsNullOrWhiteSpace(installDir))
            return;

        string exePath = Path.Combine(installDir, AppInfo.ExeName);
        if (!File.Exists(exePath))
            return;

        EnsureUdpInboundRule(DiscoveryRuleName, DiscoveryPort, exePath);
        EnsureUdpInboundRule(GameRuleName, GamePort, exePath);
    }

    public static void RemoveRules()
    {
        if (!IsWindows())
            return;

        TryDeleteRule(DiscoveryRuleName);
        TryDeleteRule(GameRuleName);
    }

    private static void EnsureUdpInboundRule(string ruleName, int port, string executablePath)
    {
        if (RuleExists(ruleName))
            return;

        string quotedExe = Quote(executablePath);
        string args =
            "advfirewall firewall add rule name=\"" + ruleName + "\" dir=in action=allow " +
            "protocol=UDP localport=" + port + " program=" + quotedExe + " enable=yes profile=private,domain";

        TryRunNetsh(args);
    }

    private static void TryDeleteRule(string ruleName)
    {
        TryRunNetsh("advfirewall firewall delete rule name=\"" + ruleName + "\"");
    }

    private static bool RuleExists(string ruleName)
    {
        string output;
        if (!TryRunNetsh("advfirewall firewall show rule name=\"" + ruleName + "\"", out output))
            return false;

        return output.IndexOf(ruleName, StringComparison.OrdinalIgnoreCase) >= 0
            && output.IndexOf("No rules match", StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static bool TryRunNetsh(string arguments)
    {
        string ignored;
        return TryRunNetsh(arguments, out ignored);
    }

    private static bool TryRunNetsh(string arguments, out string combinedOutput)
    {
        combinedOutput = string.Empty;
        try
        {
            var startInfo = new ProcessStartInfo();
            startInfo.FileName = "netsh";
            startInfo.Arguments = arguments;
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                    return false;

                combinedOutput = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit(10000);
                return process.ExitCode == 0;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool IsWindows()
    {
        return Environment.OSVersion.Platform == PlatformID.Win32NT;
    }

    private static string Quote(string value)
    {
        return value.IndexOf(' ') >= 0 ? "\"" + value + "\"" : value;
    }
}
