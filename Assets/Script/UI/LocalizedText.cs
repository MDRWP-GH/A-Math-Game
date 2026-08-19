using AMath.Art;
using AMath.Settings;
using AMath.UI.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>
    /// Keeps a <see cref="Text"/> bound to a localization key so switching the
    /// language on the settings screen re-labels every screen immediately,
    /// including screens that are currently inactive (they refresh on enable).
    /// Thai uses K2D with vowel adjustment; English keeps the font assigned at bind time.
    /// </summary>
    internal sealed class LocalizedText : MonoBehaviour
    {
        private Text _target;
        private string _key;
        private Font _latinFont;

        /// <summary>Binds (or rebinds) <paramref name="target"/> to a localization key.</summary>
        public static void Bind(Text target, string key)
        {
            if (target == null)
            {
                return;
            }

            var binder = target.GetComponent<LocalizedText>();
            if (binder == null)
            {
                binder = target.gameObject.AddComponent<LocalizedText>();
            }

            binder._target = target;
            binder._key = key;
            if (binder._latinFont == null)
            {
                binder._latinFont = target.font != null ? target.font : GameFonts.Jersey25;
            }

            binder.Apply();
        }

        private void OnEnable()
        {
            GameSettings.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            GameSettings.Changed -= Apply;
        }

        private void Apply()
        {
            if (_target == null || string.IsNullOrEmpty(_key))
            {
                return;
            }

            UiText.Set(_target, UiLocalizationProvider.Shared.GetText(_key), _latinFont);
        }
    }
}
