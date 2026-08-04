using System;
using UnityEngine;

namespace AMath.Utilities
{
    /// <summary>
    /// Stable per-installation identity. The GUID persists across sessions so
    /// a player who disconnects (or a migrated host) can be re-matched to
    /// their seat, score and rack when they reconnect.
    /// </summary>
    public static class LocalIdentity
    {
        private const string GuidKey = "amath.player.guid";
        private const string NameKey = "amath.player.name";

        private static string _cachedGuid;

        /// <summary>Persistent GUID for this installation (created on first use).</summary>
        public static string PersistentGuid
        {
            get
            {
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
            get => PlayerPrefs.GetString(NameKey, SystemInfo.deviceName);
            set
            {
                PlayerPrefs.SetString(NameKey, value);
                PlayerPrefs.Save();
            }
        }
    }
}
