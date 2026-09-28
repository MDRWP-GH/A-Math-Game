using System;
using AMath.Core.Identity;
using AMath.Accounts;
using UnityEngine;

namespace AMath.Settings
{
    /// <summary>
    /// The player preferences shown on the General and Audio tabs. Volumes are stored and published
    /// here; audio sources read them once the game has sound to play.
    /// </summary>
    public static class GameSettings
    {
        private const string LegacyPlayerNameKey = "amath.general.playerName";
        private const string PlayerNameKeyPrefix = "amath.general.playerName.profile.";
        private const string PlayerNameMigrationKey = "amath.general.playerName.profileMigrated";
        private const string LanguageKey = "amath.general.language";
        private const string SoundEffectsKey = "amath.audio.soundEffects";
        private const string MusicKey = "amath.audio.music";

        public static readonly string[] LanguageLabels = { "English", "ไทย" };

        private static string _playerName;
        private static int _languageIndex;
        private static float _soundEffectsVolume;
        private static float _musicVolume;
        private static string _playerProfileId;

        /// <summary>Raised after any preference on this screen changed.</summary>
        public static event Action Changed;

        static GameSettings()
        {
            _playerName = string.Empty;
            _languageIndex = Mathf.Clamp(PlayerPrefs.GetInt(LanguageKey, 0), 0, LanguageLabels.Length - 1);
            _soundEffectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SoundEffectsKey, 0.8f));
            _musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 0.6f));
        }

        public static string PlayerName
        {
            get => _playerName;
            set
            {
                TrySetPlayerName(value, out _);
            }
        }

        public static void UsePlayerProfile(string accountId)
        {
            string profileId = accountId?.Trim() ?? string.Empty;
            if (profileId == _playerProfileId)
                return;

            _playerProfileId = profileId;
            _playerName = string.Empty;
            if (!string.IsNullOrEmpty(profileId))
            {
                string stored;
                if (AccountSession.IsAuthenticated && AccountSession.AccountId == profileId)
                {
                    stored = PortableProfile.TryLoad(AccountSession.ProfileRoot, out PortableProfileData profile, out _)
                        ? profile.PlayerName : string.Empty;
                }
                else
                {
                    string key = PlayerNameKeyPrefix + profileId;
                    MigrateLegacyPlayerNameOnce(key);
                    stored = PlayerPrefs.GetString(key, string.Empty);
                }
                if (PlayerNameValidator.TryNormalize(stored, out string normalized, out _))
                    _playerName = normalized;
            }

            Changed?.Invoke();
        }

        public static void ClearPlayerProfile()
        {
            if (string.IsNullOrEmpty(_playerProfileId) && string.IsNullOrEmpty(_playerName))
                return;

            _playerProfileId = null;
            _playerName = string.Empty;
            Changed?.Invoke();
        }

        public static bool TrySetPlayerName(string value, out PlayerNameValidationError error)
        {
            if (!PlayerNameValidator.TryNormalize(value, out string normalized, out error))
                return false;
            if (string.IsNullOrEmpty(_playerProfileId))
            {
                error = PlayerNameValidationError.Required;
                return false;
            }
            if (normalized == _playerName)
                return true;

            if (AccountSession.IsAuthenticated && AccountSession.AccountId == _playerProfileId)
            {
                if (!PortableProfile.TryLoad(AccountSession.ProfileRoot, out PortableProfileData profile, out _))
                    return false;
                profile.PlayerName = normalized;
                if (!PortableProfile.TrySave(AccountSession.ProfileRoot, profile, out _))
                    return false;
            }
            else
            {
                PlayerPrefs.SetString(PlayerNameKeyPrefix + _playerProfileId, normalized);
                PlayerPrefs.Save();
            }
            _playerName = normalized;
            Changed?.Invoke();
            return true;
        }

        public static int LanguageIndex
        {
            get => _languageIndex;
            set
            {
                var clamped = Mathf.Clamp(value, 0, LanguageLabels.Length - 1);
                if (clamped == _languageIndex)
                {
                    return;
                }

                _languageIndex = clamped;
                PlayerPrefs.SetInt(LanguageKey, _languageIndex);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static string LanguageLabel => LanguageLabels[_languageIndex];

        public static float SoundEffectsVolume
        {
            get => _soundEffectsVolume;
            set => SetVolume(ref _soundEffectsVolume, value, SoundEffectsKey);
        }

        public static float MusicVolume
        {
            get => _musicVolume;
            set => SetVolume(ref _musicVolume, value, MusicKey);
        }

        /// <summary>
        /// Writes pending preferences to disk. Volume sliders change every frame while dragging, so
        /// they only mark the value dirty and rely on this being called when the screen closes.
        /// </summary>
        public static void Flush() => PlayerPrefs.Save();

        private static void MigrateLegacyPlayerNameOnce(string profileKey)
        {
            if (PlayerPrefs.GetInt(PlayerNameMigrationKey, 0) != 0)
                return;

            if (!PlayerPrefs.HasKey(profileKey) && PlayerPrefs.HasKey(LegacyPlayerNameKey))
            {
                string legacy = PlayerPrefs.GetString(LegacyPlayerNameKey, string.Empty);
                if (PlayerNameValidator.TryNormalize(legacy, out string normalized, out _))
                    PlayerPrefs.SetString(profileKey, normalized);
            }

            PlayerPrefs.SetInt(PlayerNameMigrationKey, 1);
            PlayerPrefs.Save();
        }

        private static void SetVolume(ref float field, float value, string key)
        {
            var clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(clamped, field))
            {
                return;
            }

            field = clamped;
            PlayerPrefs.SetFloat(key, clamped);
            Changed?.Invoke();
        }
    }
}
