using AMath.Art;
using AMath.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>
    /// Applies the active UI language font and Thai vowel adjustment to a Text.
    /// </summary>
    internal static class UiText
    {
        private const float ThaiLineSpacing = 1.25f;

        public static bool IsThai => GameSettings.LanguageIndex == 1;

        public static void Set(Text target, string value, Font latinFont = null)
        {
            if (target == null)
            {
                return;
            }

            var latin = latinFont != null ? latinFont : GameFonts.Jersey25;

            if (IsThai)
            {
                var adjusted = ThaiFontAdjuster.Adjust(value ?? string.Empty);
                target.font = GameFonts.K2D;
                target.lineSpacing = ThaiLineSpacing;
                // Force the dynamic font atlas to include raised PUA mark glyphs.
                GameFonts.K2D.RequestCharactersInTexture(adjusted, target.fontSize, target.fontStyle);
                target.text = adjusted;
            }
            else
            {
                target.font = latin;
                target.lineSpacing = 1f;
                target.text = value ?? string.Empty;
            }
        }
    }
}
