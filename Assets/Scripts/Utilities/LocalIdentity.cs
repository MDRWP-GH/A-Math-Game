using System;
using UnityEngine;

namespace AMath.Utilities
{
    /// <summary>
    /// Stable per-installation identity. The GUID persists across sessions so
    /// a player who disconnects (or a migrated host) can be re-matched to
    /// their seat, score and rack when they reconnect.
    ///
    /// The host treats the GUID as "one player", so two processes that report
    /// the same GUID cannot sit in the same room. Two things would otherwise
    /// break local LAN testing, because PlayerPrefs are keyed by
    /// company/product name and therefore shared by every build on a machine:
    ///  - the editor gets its own key, so host-in-editor + client-in-build works;
    ///  - <c>-amath-guid</c> / <c>-amath-name</c> override both values, so
    ///    several copies of the same build can join one room.
    /// </summary>
    public static class LocalIdentity
    {
#if UNITY_EDITOR
        private const string GuidKey = "amath.player.guid.editor";
        private const string NameKey = "amath.player.name.editor";
#else
        private const string GuidKey = "amath.player.guid";
        private const string NameKey = "amath.player.name";
#endif

        private const string GuidArgument = "-amath-guid";
        private const string NameArgument = "-amath-name";

        private static string _cachedGuid;
        private static string _overrideName;
        private static bool _argumentsParsed;

        /// <summary>Persistent GUID for this installation (created on first use).</summary>
        public static string PersistentGuid
        {
            get
            {
                if (!string.IsNullOrEmpty(_cachedGuid))
                    return _cachedGuid;

                EnsureArgumentsParsed();
                if (!string.IsNullOrEmpty(_cachedGuid))
                    return _cachedGuid;

                _cachedGuid = PlayerPrefs.GetString(GuidKey, string.Empty);
                if (string.IsNullOrEmpty(_cachedGuid))
                {
                    _cachedGuid = Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString(GuidKey, _cachedGuid);
                    PlayerPrefs.Save();
                }

                return _cachedGuid;
            }
        }

        /// <summary>Display name chosen by the player (defaults to the device name).</summary>
        public static string DisplayName
        {
            get
            {
                EnsureArgumentsParsed();
                return string.IsNullOrEmpty(_overrideName)
                    ? PlayerPrefs.GetString(NameKey, SystemInfo.deviceName)
                    : _overrideName;
            }
            set
            {
                EnsureArgumentsParsed();

                // A command-line name is an explicit testing override; do not
                // let the menu quietly write over it.
                if (!string.IsNullOrEmpty(_overrideName))
                    return;

                PlayerPrefs.SetString(NameKey, value);
                PlayerPrefs.Save();
            }
        }

        private static void EnsureArgumentsParsed()
        {
            if (_argumentsParsed) return;
            _argumentsParsed = true;

            string[] args;
            try
            {
                args = Environment.GetCommandLineArgs();
            }
            catch (NotSupportedException)
            {
                return;
            }

            if (args == null) return;

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], GuidArgument, StringComparison.OrdinalIgnoreCase))
                    _cachedGuid = args[i + 1];
                else if (string.Equals(args[i], NameArgument, StringComparison.OrdinalIgnoreCase))
                    _overrideName = args[i + 1];
            }

            if (!string.IsNullOrEmpty(_cachedGuid) || !string.IsNullOrEmpty(_overrideName))
            {
                Debug.Log(
                    $"[Identity] Command-line override active (guid: {(string.IsNullOrEmpty(_cachedGuid) ? "-" : _cachedGuid)}, " +
                    $"name: {(string.IsNullOrEmpty(_overrideName) ? "-" : _overrideName)}).");
            }
        }
    }
}
