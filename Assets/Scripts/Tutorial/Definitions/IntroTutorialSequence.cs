using System.Collections.Generic;
using AMath.Core.Assistance;
using AMath.Core.Commands;
using AMath.Tutorial.Conditions;
using AMath.Tutorial.Dialogue;
using AMath.Tutorial.Events;
using AMath.Tutorial.Highlights;
using AMath.Tutorial.Interfaces;

namespace AMath.Tutorial.Definitions
{
    /// <summary>
    /// First playable tutorial: a scripted two-turn match. The player places
    /// tiles by hand but only onto the authored equation; the opponent is a
    /// bot that plays the script with no search or AI assistant.
    /// </summary>
    public sealed class IntroTutorialSequence : ITutorialSequenceDefinition
    {
        public const string ContinueButtonId = "demo.continue";
        public const string DemoBoardTargetId = "demo-board";
        public const string WelcomeStepId = "intro-welcome";
        public const string BoardStepId = "intro-board";
        public const string PlayerPlaceStepId = "intro-place-first";
        public const string OpponentStepId = "intro-watch-opponent";
        public const string CompleteStepId = "intro-complete";

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

        private static IReadOnlyList<ITutorialStepDefinition> BuildSteps(ILocalizedTextProvider textProvider)
        {
            return new ITutorialStepDefinition[]
            {
                new TutorialStepDefinition(
                    stepId: WelcomeStepId,
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
                    stepId: BoardStepId,
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
                    stepId: PlayerPlaceStepId,
                    objectiveTextKey: "tutorial.intro.step3.objective",
                    actions: new ITutorialAction[]
                    {
                        new TutorialDialogue(textProvider, "tutorial.intro.step3.dialogue", waitForDismiss: false),
                        new TutorialHighlight(DemoBoardTargetId)
                    },
                    advanceCondition: new TutorialCondition(CommandType.PlaceTiles),
                    hints: new[]
                    {
                        new TutorialHintDefinition(6f, "tutorial.intro.step3.hint")
                    }),
                new TutorialStepDefinition(
                    stepId: OpponentStepId,
                    objectiveTextKey: "tutorial.intro.step4.objective",
                    actions: new ITutorialAction[]
                    {
                        new TutorialDialogue(textProvider, "tutorial.intro.step4.dialogue", waitForDismiss: false)
                    },
                    advanceCondition: new TutorialCondition(CommandType.PlaceTiles),
                    hints: new[]
                    {
                        new TutorialHintDefinition(4f, "tutorial.intro.step4.hint")
                    }),
                new TutorialStepDefinition(
                    stepId: CompleteStepId,
                    objectiveTextKey: "tutorial.intro.step5.objective",
                    actions: new ITutorialAction[]
                    {
                        new TutorialDialogue(textProvider, "tutorial.intro.step5.dialogue")
                    },
                    advanceCondition: new TutorialCondition(
                        TutorialGameplaySignalKind.ButtonPressed,
                        ContinueButtonId))
            };
        }
    }
}
