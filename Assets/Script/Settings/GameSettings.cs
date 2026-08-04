using System;
using UnityEngine;

namespace AMath.Settings
{
    /// <summary>
    /// The player preferences shown on the General and Audio tabs. Volumes are stored and published
    /// here; audio sources read them once the game has sound to play.
    /// </summary>
    public static class GameSettings
    {
        private const string PlayerNameKey = "amath.general.playerName";
        private const string LanguageKey = "amath.general.language";
        private const string SoundEffectsKey = "amath.audio.soundEffects";
        private const string MusicKey = "amath.audio.music";

        private const string DefaultPlayerName = "Player";

        public static readonly string[] LanguageLabels = { "English", "ไทย" };

        private static string _playerName;
        private static int _languageIndex;
        private static float _soundEffectsVolume;
        private static float _musicVolume;

        /// <summary>Raised after any preference on this screen changed.</summary>
        public static event Action Changed;

        static GameSettings()
        {
            _playerName = PlayerPrefs.GetString(PlayerNameKey, DefaultPlayerName);
            _languageIndex = Mathf.Clamp(PlayerPrefs.GetInt(LanguageKey, 0), 0, LanguageLabels.Length - 1);
            _soundEffectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SoundEffectsKey, 0.8f));
            _musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 0.6f));
        }

        public static string PlayerName
        {
            get => _playerName;
            set
            {
                var trimmed = string.IsNullOrWhiteSpace(value) ? DefaultPlayerName : value.Trim();
                if (trimmed == _playerName)
                {
                    return;
                }

                _playerName = trimmed;
                PlayerPrefs.SetString(PlayerNameKey, _playerName);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
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
