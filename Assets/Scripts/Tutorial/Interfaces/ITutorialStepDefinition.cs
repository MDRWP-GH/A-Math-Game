using System.Collections.Generic;

namespace AMath.Tutorial.Interfaces
{
    /// <summary>
    /// Authoring contract for one ordered tutorial step. Kept as an
    /// interface (rather than requiring a concrete ScriptableObject) so
    /// tests can supply a plain in-memory implementation without touching
    /// the Unity asset pipeline. The concrete authored representation is
    /// added later under <c>Tutorial/Definitions/</c>.
    /// </summary>
    public interface ITutorialStepDefinition
    {
        /// <summary>Stable id for this step (also used as an event/highlight/save key).</summary>
        string StepId { get; }

        /// <summary>Localization key for this step's objective text.</summary>
        string ObjectiveTextKey { get; }

        /// <summary>Zero-based learner-facing milestone that owns this mechanical step.</summary>
        int MilestoneIndex { get; }

        /// <summary>Actions run, in order, when this step becomes active.</summary>
        IReadOnlyList<ITutorialAction> Actions { get; }

        /// <summary>Condition that must be satisfied for this step to advance.</summary>
        ITutorialCondition AdvanceCondition { get; }

        /// <summary>Scripted hint schedule for this step, ordered by delay.</summary>
        IReadOnlyList<TutorialHintDefinition> Hints { get; }
    }
}
