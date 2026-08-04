using System.Collections.Generic;
using AMath.Core.Assistance;
using AMath.Tutorial.Conditions;
using AMath.Tutorial.Dialogue;
using AMath.Tutorial.Events;
using AMath.Tutorial.Highlights;
using AMath.Tutorial.Interfaces;

namespace AMath.Tutorial.Definitions
{
    /// <summary>
    /// First playable tutorial shipped with the bootstrap scene.
    /// </summary>
    public sealed class IntroTutorialSequence : ITutorialSequenceDefinition
    {
        public const string ContinueButtonId = "demo.continue";
        public const string DemoBoardTargetId = "demo-board";

        private readonly IReadOnlyList<ITutorialStepDefinition> _steps;

        public IntroTutorialSequence(ILocalizedTextProvider textProvider)
        {
            _steps = BuildSteps(textProvider);
        }

        /// <inheritdoc />
        public string TutorialId => "intro";

        /// <inheritdoc />
        public string TitleKey => "tutorial.intro.title";

        /// <inheritdoc />
        public IReadOnlyList<ITutorialStepDefinition> Steps => _steps;

        /// <inheritdoc />
        public int AiUnlockAfterHintCount => -1;

        private static IReadOnlyList<ITutorialStepDefinition> BuildSteps(ILocalizedTextProvider textProvider)
        {
            return new ITutorialStepDefinition[]
            {
                new TutorialStepDefinition(
                    stepId: "intro-welcome",
                    objectiveTextKey: "tutorial.intro.step1.objective",
                    actions: new ITutorialAction[]
                    {
                        new TutorialDialogue(textProvider, "tutorial.intro.step1.dialogue")
                    },
                    advanceCondition: new TutorialCondition(
                        TutorialGameplaySignalKind.ButtonPressed,
                        ContinueButtonId),
                    hints: new[]
                    {
                        new TutorialHintDefinition(4f, "tutorial.intro.step1.hint")
                    }),
                new TutorialStepDefinition(
                    stepId: "intro-board",
                    objectiveTextKey: "tutorial.intro.step2.objective",
                    actions: new ITutorialAction[]
                    {
                        new TutorialDialogue(textProvider, "tutorial.intro.step2.dialogue"),
                        new TutorialHighlight(DemoBoardTargetId)
                    },
                    advanceCondition: new TutorialCondition(
                        TutorialGameplaySignalKind.ButtonPressed,
                        ContinueButtonId),
                    hints: new[]
                    {
                        new TutorialHintDefinition(5f, "tutorial.intro.step2.hint")
                    }),
                new TutorialStepDefinition(
                    stepId: "intro-complete",
                    objectiveTextKey: "tutorial.intro.step3.objective",
                    actions: new ITutorialAction[]
                    {
                        new TutorialDialogue(textProvider, "tutorial.intro.step3.dialogue")
                    },
                    advanceCondition: new TutorialCondition(
                        TutorialGameplaySignalKind.ButtonPressed,
                        ContinueButtonId))
            };
        }
    }
}
