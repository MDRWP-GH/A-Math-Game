using System.Collections.Generic;
using AMath.Tutorial.Interfaces;

namespace AMath.Tutorial.Definitions
{
    /// <summary>
    /// In-memory step definition used by bootstrap sequences and tests.
    /// </summary>
    public sealed class TutorialStepDefinition : ITutorialStepDefinition
    {
        public TutorialStepDefinition(
            string stepId,
            string objectiveTextKey,
            IReadOnlyList<ITutorialAction> actions,
            ITutorialCondition advanceCondition,
            IReadOnlyList<TutorialHintDefinition> hints = null,
            int milestoneIndex = 0)
        {
            StepId = stepId;
            ObjectiveTextKey = objectiveTextKey;
            MilestoneIndex = milestoneIndex;
            Actions = actions ?? System.Array.Empty<ITutorialAction>();
            AdvanceCondition = advanceCondition;
            Hints = hints ?? System.Array.Empty<TutorialHintDefinition>();
        }

        /// <inheritdoc />
        public string StepId { get; }

        /// <inheritdoc />
        public string ObjectiveTextKey { get; }

        /// <inheritdoc />
        public int MilestoneIndex { get; }

        /// <inheritdoc />
        public IReadOnlyList<ITutorialAction> Actions { get; }

        /// <inheritdoc />
        public ITutorialCondition AdvanceCondition { get; }

        /// <inheritdoc />
        public IReadOnlyList<TutorialHintDefinition> Hints { get; }
    }
}
