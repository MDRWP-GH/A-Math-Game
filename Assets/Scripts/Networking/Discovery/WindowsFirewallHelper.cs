using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AMath.Networking.Discovery
{
    /// <summary>
    /// Optional helper to register Windows Firewall rules for LAN discovery
    /// (UDP 47777) and KCP hosting (UDP 7778). Not called at startup: spawning
    /// netsh from an unsigned player is a common Defender false-positive.
    /// Players who cannot see LAN rooms can allow A-Math.exe manually.
    /// </summary>
    public static class WindowsFirewallHelper
    {
        public const string DiscoveryRuleName = "A-Math LAN Discovery";
        public const string GameRuleName = "A-Math LAN Game";

        /// <summary>UDP port used for LAN room discovery broadcasts.</summary>
        public const int DiscoveryPort = DiscoveryManager.DiscoveryPort;

        /// <summary>Default KCP game port.</summary>
        public const int GamePort = Transport.TransportConfigurator.DefaultPort;

        /// <summary>
        /// Ensures firewall rules exist for the running executable. Safe to call
        /// repeatedly; skips when not on a Windows player build.
        /// </summary>
        public static void EnsureRulesForCurrentProcess()
        {
            if (Application.isEditor)
                return;

            if (Application.platform != RuntimePlatform.WindowsPlayer)
                return;

            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath))
                    return;

                EnsureRules(exePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Discovery] Could not configure Windows Firewall: {ex.Message}");
            }
        }

        /// <summary>
        /// Ensures discovery and game rules exist for <paramref name="executablePath"/>.
        /// No-op on non-Windows platforms.
        /// </summary>
        public static void EnsureRules(string executablePath)
        {
            if (!IsWindows() || string.IsNullOrWhiteSpace(executablePath))
                return;

            EnsureUdpInboundRule(DiscoveryRuleName, DiscoveryPort, executablePath);
            EnsureUdpInboundRule(GameRuleName, GamePort, executablePath);
        }

        /// <summary>Removes the LAN rules created by <see cref="EnsureRules"/>.</summary>
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
                $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow " +
                $"protocol=UDP localport={port} program={quotedExe} enable=yes profile=private,domain";

            if (!TryRunNetsh(args, out string error))
                Debug.LogWarning($"[Discovery] Firewall rule '{ruleName}' was not added: {error}");
        }

        private static void TryDeleteRule(string ruleName)
        {
            TryRunNetsh($"advfirewall firewall delete rule name=\"{ruleName}\"", out _);
        }

        private static bool RuleExists(string ruleName)
        {
            if (!TryRunNetsh($"advfirewall firewall show rule name=\"{ruleName}\"", out string output))
                return false;

            return output.IndexOf(ruleName, StringComparison.OrdinalIgnoreCase) >= 0
                && output.IndexOf("No rules match", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool TryRunNetsh(string arguments, out string combinedOutput)
        {
            combinedOutput = string.Empty;
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using Process process = Process.Start(startInfo);
                if (process == null)
                    return false;

                combinedOutput = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit(10_000);
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                combinedOutput = ex.Message;
                return false;
            }
        }

        private static bool IsWindows() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        private static string Quote(string value) =>
            value.IndexOf(' ') >= 0 ? $"\"{value}\"" : value;
    }
}
