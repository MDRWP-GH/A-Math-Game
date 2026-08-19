using UnityEngine;

namespace AMath.Art
{
    /// <summary>
    /// Loads UI fonts from Assets/Resources/Fonts at runtime.
    /// </summary>
    public static class GameFonts
    {
        private const string Jersey25Resource = "Fonts/Jersey25-Regular";
        private const string JainiPurvaResource = "Fonts/JainiPurva-Regular";
        private const string K2DResource = "Fonts/K2D-Regular";

        private static Font _jersey25;
        private static Font _jainiPurva;
        private static Font _k2d;

        /// <summary>Jersey 25 — primary English UI / button label font.</summary>
        public static Font Jersey25 => Load(ref _jersey25, Jersey25Resource);

        /// <summary>Jaini Purva — main-menu title font (English brand lockup).</summary>
        public static Font JainiPurva => Load(ref _jainiPurva, JainiPurvaResource);

        /// <summary>K2D — Thai UI font (includes C90 PUA glyphs for vowel stacking).</summary>
        public static Font K2D => Load(ref _k2d, K2DResource);

        private static Font Load(ref Font cache, string resourcePath)
        {
            if (cache == null)
            {
                cache = Resources.Load<Font>(resourcePath);
            }

            return cache != null
                ? cache
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
