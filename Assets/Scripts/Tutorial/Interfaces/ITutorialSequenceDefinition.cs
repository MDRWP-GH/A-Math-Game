using System.Collections.Generic;

namespace AMath.Tutorial.Interfaces
{
    /// <summary>
    /// Authoring contract for one complete tutorial: an ordered list of
    /// scripted steps. The match itself is driven by
    /// <c>ScriptedTutorialMatchScript</c>, not by an AI mover or assistant.
    /// </summary>
    public interface ITutorialSequenceDefinition
    {
        /// <summary>Stable id for this tutorial (used by <see cref="ITutorialSaveStore"/>).</summary>
        string TutorialId { get; }

        /// <summary>Localization key for this tutorial's display title.</summary>
        string TitleKey { get; }

        /// <summary>
        /// Localization keys for the learner-facing milestones shown in the
        /// coach panel. Several mechanical steps may belong to one milestone.
        /// </summary>
        IReadOnlyList<string> MilestoneTitleKeys { get; }

        /// <summary>Steps in play order.</summary>
        IReadOnlyList<ITutorialStepDefinition> Steps { get; }
    }
}
