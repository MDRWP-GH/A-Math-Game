using System.Collections.Generic;
using AMath.Core.Assistance;
using AMath.Core.Commands;
using AMath.Gameplay.Board;
using AMath.Tutorial.Conditions;
using AMath.Tutorial.Dialogue;
using AMath.Tutorial.Events;
using AMath.Tutorial.Highlights;
using AMath.Tutorial.Interfaces;
using static AMath.Gameplay.Board.AMathTileSet;

namespace AMath.Tutorial.Definitions
{
    /// <summary>
    /// Master Duel-style intro: spotlight-guided micro-steps that walk the
    /// player through one scripted opening equation.
    /// </summary>
    public sealed class IntroTutorialSequence : ITutorialSequenceDefinition
    {
        public const string IntroTutorialId = "intro";
        public const string ContinueButtonId = "demo.continue";
        public const string DemoBoardTargetId = "demo-board";
        public const string DemoRackTargetId = "demo-rack";
        public const string DemoGuidedTileTargetId = "demo-guided-tile";
        public const string DemoConfirmTargetId = "demo-confirm";
        public const string DemoPassTargetId = "demo-pass";
        public const string DemoExchangeTargetId = "demo-exchange";

        public const string WelcomeStepId = "intro-welcome";
        public const string BoardStepId = "intro-board";
        public const string RackStepId = "intro-rack";
        public const string Select1StepId = "intro-select-1";
        public const string Place1StepId = "intro-place-1";
        public const string SelectPlusStepId = "intro-select-plus";
        public const string PlacePlusStepId = "intro-place-plus";
        public const string Select2StepId = "intro-select-2";
        public const string Place2StepId = "intro-place-2";
        public const string SelectEqualsStepId = "intro-select-equals";
        public const string PlaceEqualsStepId = "intro-place-equals";
        public const string Select3StepId = "intro-select-3";
        public const string Place3StepId = "intro-place-3";
        public const string ConfirmStepId = "intro-confirm";
        public const string OpponentStepId = "intro-watch-opponent";
        public const string CompleteStepId = "intro-complete";

        /// <summary>First interactive placement step (used by tests and resume).</summary>
        public const string PlayerPlaceStepId = Select1StepId;

        private readonly IReadOnlyList<ITutorialStepDefinition> _steps;
        private static readonly string[] Milestones =
        {
            "tutorial.intro.milestone.orientation",
            "tutorial.intro.milestone.equation",
            "tutorial.intro.milestone.confirm",
            "tutorial.intro.milestone.complete"
        };

        public IntroTutorialSequence(ILocalizedTextProvider textProvider)
        {
            _steps = BuildSteps(textProvider);
        }

        /// <inheritdoc />
        public string TutorialId => IntroTutorialId;

        /// <inheritdoc />
        public string TitleKey => "tutorial.intro.title";

        /// <inheritdoc />
        public IReadOnlyList<string> MilestoneTitleKeys => Milestones;

        /// <inheritdoc />
        public IReadOnlyList<ITutorialStepDefinition> Steps => _steps;

        public static string CellTargetId(int x, int y) => $"demo-cell-{x}-{y}";

        public static string CellSignalId(int x, int y) => $"{x},{y}";

        private static IReadOnlyList<ITutorialStepDefinition> BuildSteps(ILocalizedTextProvider textProvider)
        {
            return new ITutorialStepDefinition[]
            {
                DialogueStep(
                    textProvider,
                    WelcomeStepId,
                    "tutorial.intro.step1.objective",
                    "tutorial.intro.step1.dialogue",
                    "tutorial.intro.step1.hint",
                    4f),
                DialogueStep(
                    textProvider,
                    BoardStepId,
                    "tutorial.intro.step2.objective",
                    "tutorial.intro.step2.dialogue",
                    "tutorial.intro.step2.hint",
                    5f,
                    DemoBoardTargetId),
                DialogueStep(
                    textProvider,
                    RackStepId,
                    "tutorial.intro.step2b.objective",
                    "tutorial.intro.step2b.dialogue",
                    "tutorial.intro.step2b.hint",
                    5f,
                    DemoRackTargetId),
                SelectTileStep(
                    textProvider,
                    Select1StepId,
                    "tutorial.intro.select1.objective",
                    "tutorial.intro.select1.dialogue",
                    "tutorial.intro.select1.hint",
                    1),
                PlaceCellStep(
                    textProvider,
                    Place1StepId,
                    "tutorial.intro.place1.objective",
                    "tutorial.intro.place1.dialogue",
                    "tutorial.intro.place1.hint",
                    TutorialAuthoredBoard.Intro.HumanTurn,
                    0),
                SelectTileStep(
                    textProvider,
                    SelectPlusStepId,
                    "tutorial.intro.select_plus.objective",
                    "tutorial.intro.select_plus.dialogue",
                    "tutorial.intro.select_plus.hint",
                    Plus),
                PlaceCellStep(
                    textProvider,
                    PlacePlusStepId,
                    "tutorial.intro.place_plus.objective",
                    "tutorial.intro.place_plus.dialogue",
                    "tutorial.intro.place_plus.hint",
                    TutorialAuthoredBoard.Intro.HumanTurn,
                    1),
                SelectTileStep(
                    textProvider,
                    Select2StepId,
                    "tutorial.intro.select2.objective",
                    "tutorial.intro.select2.dialogue",
                    "tutorial.intro.select2.hint",
                    2),
                PlaceCellStep(
                    textProvider,
                    Place2StepId,
                    "tutorial.intro.place2.objective",
                    "tutorial.intro.place2.dialogue",
                    "tutorial.intro.place2.hint",
                    TutorialAuthoredBoard.Intro.HumanTurn,
                    2),
                SelectTileStep(
                    textProvider,
                    SelectEqualsStepId,
                    "tutorial.intro.select_equals.objective",
                    "tutorial.intro.select_equals.dialogue",
                    "tutorial.intro.select_equals.hint",
                    EqualsSign),
                PlaceCellStep(
                    textProvider,
                    PlaceEqualsStepId,
                    "tutorial.intro.place_equals.objective",
                    "tutorial.intro.place_equals.dialogue",
                    "tutorial.intro.place_equals.hint",
                    TutorialAuthoredBoard.Intro.HumanTurn,
                    3),
                SelectTileStep(
                    textProvider,
                    Select3StepId,
                    "tutorial.intro.select3.objective",
                    "tutorial.intro.select3.dialogue",
                    "tutorial.intro.select3.hint",
                    3),
                PlaceCellStep(
                    textProvider,
                    Place3StepId,
                    "tutorial.intro.place3.objective",
                    "tutorial.intro.place3.dialogue",
                    "tutorial.intro.place3.hint",
                    TutorialAuthoredBoard.Intro.HumanTurn,
                    4),
                new TutorialStepDefinition(
                    stepId: ConfirmStepId,
                    objectiveTextKey: "tutorial.intro.confirm.objective",
                    actions: new ITutorialAction[]
                    {
                        new TutorialDialogue(textProvider, "tutorial.intro.confirm.dialogue", waitForDismiss: false),
                        new TutorialHighlight(DemoConfirmTargetId)
                    },
                    advanceCondition: new TutorialCondition(CommandType.PlaceTiles),
                    hints: new[]
                    {
                        new TutorialHintDefinition(5f, "tutorial.intro.confirm.hint")
                    },
                    milestoneIndex: MilestoneFor(ConfirmStepId)),
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
                    },
                    milestoneIndex: MilestoneFor(OpponentStepId)),
                DialogueStep(
                    textProvider,
                    CompleteStepId,
                    "tutorial.intro.step5.objective",
                    "tutorial.intro.step5.dialogue",
                    hintKey: null,
                    hintDelay: 0f)
            };
        }

        private static TutorialStepDefinition DialogueStep(
            ILocalizedTextProvider textProvider,
            string stepId,
            string objectiveKey,
            string dialogueKey,
            string hintKey,
            float hintDelay,
            string highlightTargetId = null)
        {
            var actions = new List<ITutorialAction>
            {
                new TutorialDialogue(textProvider, dialogueKey)
            };

            if (!string.IsNullOrEmpty(highlightTargetId))
                actions.Add(new TutorialHighlight(highlightTargetId));

            var hints = string.IsNullOrEmpty(hintKey)
                ? System.Array.Empty<TutorialHintDefinition>()
                : new[] { new TutorialHintDefinition(hintDelay, hintKey) };

            return new TutorialStepDefinition(
                stepId,
                objectiveKey,
                actions,
                new TutorialCondition(TutorialGameplaySignalKind.ButtonPressed, ContinueButtonId),
                hints,
                MilestoneFor(stepId));
        }

        private static TutorialStepDefinition SelectTileStep(
            ILocalizedTextProvider textProvider,
            string stepId,
            string objectiveKey,
            string dialogueKey,
            string hintKey,
            byte tileId)
        {
            return new TutorialStepDefinition(
                stepId,
                objectiveKey,
                new ITutorialAction[]
                {
                    new TutorialDialogue(textProvider, dialogueKey, waitForDismiss: false),
                    new TutorialHighlight(DemoGuidedTileTargetId)
                },
                new TutorialCondition(TutorialGameplaySignalKind.TileDragged, tileId),
                hints: new[] { new TutorialHintDefinition(5f, hintKey) },
                milestoneIndex: MilestoneFor(stepId));
        }

        private static TutorialStepDefinition PlaceCellStep(
            ILocalizedTextProvider textProvider,
            string stepId,
            string objectiveKey,
            string dialogueKey,
            string hintKey,
            IReadOnlyList<TilePlacement> turn,
            int index)
        {
            (int x, int y) = TutorialAuthoredBoard.Cell(turn, index);
            return PlaceCellStep(textProvider, stepId, objectiveKey, dialogueKey, hintKey, x, y);
        }

        private static TutorialStepDefinition PlaceCellStep(
            ILocalizedTextProvider textProvider,
            string stepId,
            string objectiveKey,
            string dialogueKey,
            string hintKey,
            int x,
            int y)
        {
            return new TutorialStepDefinition(
                stepId,
                objectiveKey,
                new ITutorialAction[]
                {
                    new TutorialDialogue(textProvider, dialogueKey, waitForDismiss: false),
                    new TutorialHighlight(CellTargetId(x, y))
                },
                new TutorialCondition(
                    TutorialGameplaySignalKind.TilePlacedOnBoard,
                    CellSignalId(x, y)),
                hints: new[] { new TutorialHintDefinition(5f, hintKey) },
                milestoneIndex: MilestoneFor(stepId));
        }

        private static int MilestoneFor(string stepId)
        {
            if (stepId == CompleteStepId) return 3;
            if (stepId == ConfirmStepId || stepId == OpponentStepId) return 2;
            if (stepId == WelcomeStepId || stepId == BoardStepId || stepId == RackStepId) return 0;
            return 1;
        }
    }
}
