using UnityEngine;

namespace AMath.UI
{
    /// <summary>
    /// One place for the colours the runtime-built screens share.
    /// </summary>
    internal static class UiPalette
    {
        public static readonly Color Background = new Color(0.05f, 0.08f, 0.20f, 1f);
        public static readonly Color Panel = new Color(0.11f, 0.16f, 0.34f, 0.98f);
        public static readonly Color PanelTranslucent = new Color(0.07f, 0.11f, 0.24f, 0.62f);
        public static readonly Color Overlay = new Color(0.02f, 0.03f, 0.10f, 0.72f);
        public static readonly Color Card = new Color(0.13f, 0.19f, 0.39f, 1f);

        /// <summary>Translucent glass used by Settings / Help / History-style panels.</summary>
        public static readonly Color Glass = new Color(0.05f, 0.05f, 0.07f, 0.62f);
        public static readonly Color GlassStrong = new Color(0.05f, 0.05f, 0.07f, 0.88f);
        public static readonly Color GlassRow = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color Field = new Color(0.12f, 0.12f, 0.14f, 0.92f);
        public static readonly Color FieldBorder = new Color(0f, 0f, 0f, 0.85f);

        public static readonly Color Primary = new Color(0.98f, 0.58f, 0.16f, 1f);
        public static readonly Color PrimaryHighlight = new Color(1f, 0.70f, 0.27f, 1f);
        public static readonly Color Secondary = new Color(0.20f, 0.53f, 0.88f, 1f);
        public static readonly Color SecondaryHighlight = new Color(0.33f, 0.65f, 1f, 1f);
        public static readonly Color Quit = new Color(0.61f, 0.28f, 0.41f, 1f);
        public static readonly Color QuitHighlight = new Color(0.77f, 0.38f, 0.52f, 1f);

        /// <summary>Mockup-style green Play accent.</summary>
        public static readonly Color Success = new Color(0.22f, 0.78f, 0.32f, 1f);
        public static readonly Color SuccessHighlight = new Color(0.38f, 0.90f, 0.46f, 1f);

        /// <summary>Mockup-style red Exit / danger accent.</summary>
        public static readonly Color Danger = new Color(0.92f, 0.22f, 0.22f, 1f);
        public static readonly Color DangerHighlight = new Color(1f, 0.38f, 0.38f, 1f);

        public static readonly Color WinnerGold = new Color(1f, 0.88f, 0.18f, 1f);
        public static readonly Color TimerWarning = new Color(0.95f, 0.28f, 0.22f, 1f);

        public static readonly Color LightText = new Color(0.94f, 0.97f, 1f, 1f);
        public static readonly Color MutedText = new Color(0.69f, 0.77f, 0.92f, 1f);
        public static readonly Color DarkText = new Color(0.10f, 0.13f, 0.22f, 1f);
        public static readonly Color HintText = new Color(0.38f, 0.43f, 0.53f, 1f);
        public static readonly Color HoverText = new Color(1f, 0.92f, 0.55f, 1f);
        public static readonly Color PressedText = new Color(1f, 0.78f, 0.32f, 1f);

        public static readonly Color Track = new Color(0.78f, 0.84f, 0.93f, 0.55f);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.28f);

        public static readonly Color BoardFrame = new Color(0.42f, 0.26f, 0.12f, 1f);
        public static readonly Color RackShelf = new Color(0.48f, 0.30f, 0.14f, 1f);
        public static readonly Color CellPlain = new Color(0.55f, 0.38f, 0.22f, 1f);
        public static readonly Color CellOccupied = new Color(0.92f, 0.55f, 0.18f, 1f);
        public static readonly Color CellDraftValid = new Color(0.28f, 0.72f, 0.38f, 1f);
        public static readonly Color CellDraftInvalid = new Color(0.78f, 0.28f, 0.24f, 1f);
        public static readonly Color CellHighlight = new Color(0.70f, 0.52f, 0.32f, 1f);
        public static readonly Color CellGuide = new Color(1f, 0.77f, 0.04f, 1f);
        public static readonly Color CellHover = new Color(0.82f, 0.68f, 0.42f, 1f);
        public static readonly Color CellSelected = new Color(1f, 0.92f, 0.55f, 1f);
        public static readonly Color TurnRing = new Color(1f, 0.85f, 0.20f, 1f);
    }
}
