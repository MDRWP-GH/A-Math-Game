namespace AMath.Core
{
    /// <summary>
    /// Host-selected per-turn limit for lobby matches. Values map to the
    /// standard Rush → Long ladder used in casual and club play.
    /// </summary>
    public enum TurnTimePreset : byte
    {
        Rush = 0,
        Short = 1,
        Normal = 2,
        Long = 3
    }

    public static class TurnTimePresetExtensions
    {
        public static int ToTurnSeconds(this TurnTimePreset preset) =>
            GameRules.TurnSecondsFor(preset);

        public static bool IsDefined(byte value) =>
            value <= (byte)TurnTimePreset.Long;
    }
}
