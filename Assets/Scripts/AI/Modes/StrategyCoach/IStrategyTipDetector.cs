using AMath.Core.Assistance.Context;

namespace AMath.AI.Modes.StrategyCoach
{
    /// <summary>
    /// Scripted tip rule. Detectors inspect safe context only and never call
    /// a language-model backend.
    /// </summary>
    public interface IStrategyTipDetector
    {
        /// <summary>Stable id for tests and ordering.</summary>
        string DetectorId { get; }

        /// <summary>
        /// Returns true when this tip applies. <paramref name="tip"/> is
        /// player-facing text already localized by the detector.
        /// </summary>
        bool TryDetect(GameContextSnapshot context, out string tip);
    }
}
