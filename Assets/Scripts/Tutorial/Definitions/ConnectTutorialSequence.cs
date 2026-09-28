using System.Collections.Generic;
using AMath.Core.Assistance;
using AMath.Gameplay.Board;
using AMath.Core.Commands;
using AMath.Tutorial.Conditions;
using AMath.Tutorial.Dialogue;
using AMath.Tutorial.Events;
using AMath.Tutorial.Highlights;
using AMath.Tutorial.Interfaces;
using static AMath.Gameplay.Board.AMathTileSet;

namespace AMath.Tutorial.Definitions
{
    /// <summary>
    /// Chapter 2: extend an existing board line with a connecting equation (3+4=7).
    /// </summary>
    public sealed class ConnectTutorialSequence : ITutorialSequenceDefinition
    {
        public const string ConnectTutorialId = "connect";
        public const string WelcomeStepId = "connect-welcome";
        public const string BoardStepId = "connect-board";
        public const string SelectPlusStepId = "connect-select-plus";
        public const string PlacePlusStepId = "connect-place-plus";
        public const string Select4StepId = "connect-select-4";
        public const string Place4StepId = "connect-place-4";
        public const string SelectEqualsStepId = "connect-select-equals";
        public const string PlaceEqualsStepId = "connect-place-equals";
        public const string Select7StepId = "connect-select-7";
        public const string Place7StepId = "connect-place-7";
        public const string ConfirmStepId = "connect-confirm";
        public const string CompleteStepId = "connect-complete";

        private readonly IReadOnlyList<ITutorialStepDefinition> _steps;
        private static readonly string[] Milestones =
        {
            "tutorial.connect.milestone.read",
            "tutorial.connect.milestone.extend",
            "tutorial.connect.milestone.confirm",
            "tutorial.connect.milestone.complete"
        };

        public ConnectTutorialSequence(ILocalizedTextProvider textProvider)
        {
            _steps = BuildSteps(textProvider);
        }

        public string TutorialId => ConnectTutorialId;

        public string TitleKey => "tutorial.connect.title";

        public IReadOnlyList<string> MilestoneTitleKeys => Milestones;

        public IReadOnlyList<ITutorialStepDefinition> Steps => _steps;

        private static IReadOnlyList<ITutorialStepDefinition> BuildSteps(ILocalizedTextProvider textProvider)
        {
            return new ITutorialStepDefinition[]
            {
                DialogueStep(textProvider, WelcomeStepId, "tutorial.connect.step1.objective", "tutorial.connect.step1.dialogue", "tutorial.connect.step1.hint", 4f),
                DialogueStep(textProvider, BoardStepId, "tutorial.connect.step2.objective", "tutorial.connect.step2.dialogue", "tutorial.connect.step2.hint", 5f, IntroTutorialSequence.DemoBoardTargetId),
                SelectTileStep(textProvider, SelectPlusStepId, "tutorial.connect.select_plus.objective", "tutorial.connect.select_plus.dialogue", "tutorial.connect.select_plus.hint", Plus),
                PlaceCellStep(textProvider, PlacePlusStepId, "tutorial.connect.place_plus.objective", "tutorial.connect.place_plus.dialogue", "tutorial.connect.place_plus.hint", TutorialAuthoredBoard.Connect.HumanTurn, 0),
                SelectTileStep(textProvider, Select4StepId, "tutorial.connect.select4.objective", "tutorial.connect.select4.dialogue", "tutorial.connect.select4.hint", 4),
                PlaceCellStep(textProvider, Place4StepId, "tutorial.connect.place4.objective", "tutorial.connect.place4.dialogue", "tutorial.connect.place4.hint", TutorialAuthoredBoard.Connect.HumanTurn, 1),
                SelectTileStep(textProvider, SelectEqualsStepId, "tutorial.connect.select_equals.objective", "tutorial.connect.select_equals.dialogue", "tutorial.connect.select_equals.hint", EqualsSign),
                PlaceCellStep(textProvider, PlaceEqualsStepId, "tutorial.connect.place_equals.objective", "tutorial.connect.place_equals.dialogue", "tutorial.connect.place_equals.hint", TutorialAuthoredBoard.Connect.HumanTurn, 2),
                SelectTileStep(textProvider, Select7StepId, "tutorial.connect.select7.objective", "tutorial.connect.select7.dialogue", "tutorial.connect.select7.hint", 7),
                PlaceCellStep(textProvider, Place7StepId, "tutorial.connect.place7.objective", "tutorial.connect.place7.dialogue", "tutorial.connect.place7.hint", TutorialAuthoredBoard.Connect.HumanTurn, 3),
                new TutorialStepDefinition(
                    ConfirmStepId,
                    "tutorial.connect.confirm.objective",
                    new ITutorialAction[]
                    {
                        new TutorialDialogue(textProvider, "tutorial.connect.confirm.dialogue", waitForDismiss: false),
                        new TutorialHighlight(IntroTutorialSequence.DemoConfirmTargetId)
                    },
                    new TutorialCondition(CommandType.PlaceTiles),
                    hints: new[] { new TutorialHintDefinition(5f, "tutorial.connect.confirm.hint") },
                    milestoneIndex: MilestoneFor(ConfirmStepId)),
                DialogueStep(textProvider, CompleteStepId, "tutorial.connect.complete.objective", "tutorial.connect.complete.dialogue", null, 0f)
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
            var actions = new List<ITutorialAction> { new TutorialDialogue(textProvider, dialogueKey) };
            if (!string.IsNullOrEmpty(highlightTargetId))
                actions.Add(new TutorialHighlight(highlightTargetId));

            var hints = string.IsNullOrEmpty(hintKey)
                ? System.Array.Empty<TutorialHintDefinition>()
                : new[] { new TutorialHintDefinition(hintDelay, hintKey) };

            return new TutorialStepDefinition(
                stepId,
                objectiveKey,
                actions,
                new TutorialCondition(TutorialGameplaySignalKind.ButtonPressed, IntroTutorialSequence.ContinueButtonId),
                hints,
                MilestoneFor(stepId));
        }

        private static TutorialStepDefinition SelectTileStep(
            ILocalizedTextProvider textProvider,
            string stepId,
            string objectiveKey,
            string dialogueKey,
            string hintKey,
            byte tileId) =>
            new TutorialStepDefinition(
                stepId,
                objectiveKey,
                new ITutorialAction[]
                {
                    new TutorialDialogue(textProvider, dialogueKey, waitForDismiss: false),
                    new TutorialHighlight(IntroTutorialSequence.DemoGuidedTileTargetId)
                },
                new TutorialCondition(TutorialGameplaySignalKind.TileDragged, tileId),
                hints: new[] { new TutorialHintDefinition(5f, hintKey) },
                milestoneIndex: MilestoneFor(stepId));

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
            int y) =>
            new TutorialStepDefinition(
                stepId,
                objectiveKey,
                new ITutorialAction[]
                {
                    new TutorialDialogue(textProvider, dialogueKey, waitForDismiss: false),
                    new TutorialHighlight(IntroTutorialSequence.CellTargetId(x, y))
                },
                new TutorialCondition(TutorialGameplaySignalKind.TilePlacedOnBoard, IntroTutorialSequence.CellSignalId(x, y)),
                hints: new[] { new TutorialHintDefinition(5f, hintKey) },
                milestoneIndex: MilestoneFor(stepId));

        private static int MilestoneFor(string stepId)
        {
            if (stepId == CompleteStepId) return 3;
            if (stepId == ConfirmStepId) return 2;
            if (stepId == WelcomeStepId || stepId == BoardStepId) return 0;
            return 1;
        }
    }
}
