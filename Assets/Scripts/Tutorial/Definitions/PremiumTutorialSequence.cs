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
    /// Chapter 3: premium squares, then Pass and Exchange when the rack is stuck.
    /// </summary>
    public sealed class PremiumTutorialSequence : ITutorialSequenceDefinition
    {
        public const string PremiumTutorialId = "premium";
        public const string WelcomeStepId = "premium-welcome";
        public const string PremiumIntroStepId = "premium-intro";
        public const string SelectPlusStepId = "premium-select-plus";
        public const string PlacePlusStepId = "premium-place-plus";
        public const string Select2StepId = "premium-select-2";
        public const string Place2StepId = "premium-place-2";
        public const string SelectEqualsStepId = "premium-select-equals";
        public const string PlaceEqualsStepId = "premium-place-equals";
        public const string Select5StepId = "premium-select-5";
        public const string Place5StepId = "premium-place-5";
        public const string ConfirmStepId = "premium-confirm";
        public const string PassIntroStepId = "premium-pass-intro";
        public const string PassStepId = "premium-pass";
        public const string ExchangeIntroStepId = "premium-exchange-intro";
        public const string ExchangeStepId = "premium-exchange";
        public const string CompleteStepId = "premium-complete";

        public static int PremiumCellX => TutorialAuthoredBoard.Premium.PremiumCellX;
        public static int PremiumCellY => TutorialAuthoredBoard.Premium.PremiumCellY;

        private readonly IReadOnlyList<ITutorialStepDefinition> _steps;
        private static readonly string[] Milestones =
        {
            "tutorial.premium.milestone.premium",
            "tutorial.premium.milestone.pass",
            "tutorial.premium.milestone.exchange",
            "tutorial.premium.milestone.complete"
        };

        public PremiumTutorialSequence(ILocalizedTextProvider textProvider)
        {
            _steps = BuildSteps(textProvider);
        }

        public string TutorialId => PremiumTutorialId;

        public string TitleKey => "tutorial.premium.title";

        public IReadOnlyList<string> MilestoneTitleKeys => Milestones;

        public IReadOnlyList<ITutorialStepDefinition> Steps => _steps;

        private static IReadOnlyList<ITutorialStepDefinition> BuildSteps(ILocalizedTextProvider textProvider)
        {
            return new ITutorialStepDefinition[]
            {
                DialogueStep(textProvider, WelcomeStepId, "tutorial.premium.step1.objective", "tutorial.premium.step1.dialogue", "tutorial.premium.step1.hint", 4f),
                DialogueStep(
                    textProvider,
                    PremiumIntroStepId,
                    "tutorial.premium.premium_intro.objective",
                    "tutorial.premium.premium_intro.dialogue",
                    "tutorial.premium.premium_intro.hint",
                    5f,
                    IntroTutorialSequence.CellTargetId(PremiumCellX, PremiumCellY)),
                SelectTileStep(textProvider, SelectPlusStepId, "tutorial.premium.select_plus.objective", "tutorial.premium.select_plus.dialogue", "tutorial.premium.select_plus.hint", Plus),
                PlaceCellStep(textProvider, PlacePlusStepId, "tutorial.premium.place_plus.objective", "tutorial.premium.place_plus.dialogue", "tutorial.premium.place_plus.hint", TutorialAuthoredBoard.Premium.HumanTurn, 0),
                SelectTileStep(textProvider, Select2StepId, "tutorial.premium.select2.objective", "tutorial.premium.select2.dialogue", "tutorial.premium.select2.hint", 2),
                PlaceCellStep(textProvider, Place2StepId, "tutorial.premium.place2.objective", "tutorial.premium.place2.dialogue", "tutorial.premium.place2.hint", TutorialAuthoredBoard.Premium.HumanTurn, 1),
                SelectTileStep(textProvider, SelectEqualsStepId, "tutorial.premium.select_equals.objective", "tutorial.premium.select_equals.dialogue", "tutorial.premium.select_equals.hint", EqualsSign),
                PlaceCellStep(textProvider, PlaceEqualsStepId, "tutorial.premium.place_equals.objective", "tutorial.premium.place_equals.dialogue", "tutorial.premium.place_equals.hint", TutorialAuthoredBoard.Premium.HumanTurn, 2),
                SelectTileStep(textProvider, Select5StepId, "tutorial.premium.select5.objective", "tutorial.premium.select5.dialogue", "tutorial.premium.select5.hint", 5),
                PlaceCellStep(textProvider, Place5StepId, "tutorial.premium.place5.objective", "tutorial.premium.place5.dialogue", "tutorial.premium.place5.hint", TutorialAuthoredBoard.Premium.HumanTurn, 3),
                new TutorialStepDefinition(
                    ConfirmStepId,
                    "tutorial.premium.confirm.objective",
                    new ITutorialAction[]
                    {
                        new TutorialDialogue(textProvider, "tutorial.premium.confirm.dialogue", waitForDismiss: false),
                        new TutorialHighlight(IntroTutorialSequence.DemoConfirmTargetId)
                    },
                    new TutorialCondition(CommandType.PlaceTiles),
                    hints: new[] { new TutorialHintDefinition(5f, "tutorial.premium.confirm.hint") },
                    milestoneIndex: MilestoneFor(ConfirmStepId)),
                DialogueStep(textProvider, PassIntroStepId, "tutorial.premium.pass_intro.objective", "tutorial.premium.pass_intro.dialogue", "tutorial.premium.pass_intro.hint", 4f),
                new TutorialStepDefinition(
                    PassStepId,
                    "tutorial.premium.pass.objective",
                    new ITutorialAction[]
                    {
                        new TutorialDialogue(textProvider, "tutorial.premium.pass.dialogue", waitForDismiss: false),
                        new TutorialHighlight(IntroTutorialSequence.DemoPassTargetId)
                    },
                    new TutorialCondition(CommandType.PassTurn),
                    hints: new[] { new TutorialHintDefinition(5f, "tutorial.premium.pass.hint") },
                    milestoneIndex: MilestoneFor(PassStepId)),
                DialogueStep(textProvider, ExchangeIntroStepId, "tutorial.premium.exchange_intro.objective", "tutorial.premium.exchange_intro.dialogue", "tutorial.premium.exchange_intro.hint", 4f),
                new TutorialStepDefinition(
                    ExchangeStepId,
                    "tutorial.premium.exchange.objective",
                    new ITutorialAction[]
                    {
                        new TutorialDialogue(textProvider, "tutorial.premium.exchange.dialogue", waitForDismiss: false),
                        new TutorialHighlight(IntroTutorialSequence.DemoExchangeTargetId)
                    },
                    new TutorialCondition(CommandType.ExchangeTiles),
                    hints: new[] { new TutorialHintDefinition(5f, "tutorial.premium.exchange.hint") },
                    milestoneIndex: MilestoneFor(ExchangeStepId)),
                DialogueStep(textProvider, CompleteStepId, "tutorial.premium.complete.objective", "tutorial.premium.complete.dialogue", null, 0f)
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
            if (stepId == PassIntroStepId || stepId == PassStepId) return 1;
            if (stepId == ExchangeIntroStepId || stepId == ExchangeStepId) return 2;
            return 0;
        }
    }
}
