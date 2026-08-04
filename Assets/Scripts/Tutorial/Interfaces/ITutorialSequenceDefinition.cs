using System.Collections.Generic;

namespace AMath.Tutorial.Interfaces
{
    /// <summary>
    /// Authoring contract for one complete tutorial: an ordered list of
    /// steps plus the configuration for when the "Ask AI" affordance may
    /// appear (see the Hint System design — AI is only offered after the
    /// scripted hints, if configured to do so at all).
    /// </summary>
    public interface ITutorialSequenceDefinition
    {
        /// <summary>Stable id for this tutorial (used by <see cref="ITutorialSaveStore"/>).</summary>
        string TutorialId { get; }

        /// <summary>Localization key for this tutorial's display title.</summary>
        string TitleKey { get; }

        /// <summary>Steps in play order.</summary>
        IReadOnlyList<ITutorialStepDefinition> Steps { get; }

        /// <summary>
        /// Number of scripted hints (across the current step) that must be
        /// shown before the AI button unlocks. -1 means the AI is never
        /// offered during this tutorial regardless of hints shown.
        /// </summary>
        int AiUnlockAfterHintCount { get; }
    }
}
