using System.Collections.Generic;
using AMath.Core.Commands;
using AMath.Gameplay.Board;

namespace AMath.Tutorial.Definitions
{
    /// <summary>Maps tutorial step ids to gameplay constraints for <see cref="Scripted.TutorialMatchHost"/>.</summary>
    internal static class TutorialStepRouting
    {
        public static bool TryGetSelectTile(string stepId, out byte tileId)
        {
            tileId = 0;
            return TryGetSelectIndex(stepId, out int index)
                && TutorialAuthoredBoard.TryGetHumanTurnCell(ActiveHumanTurn(stepId), index, out _, out _, out tileId);
        }

        public static bool TryGetPlaceCell(string stepId, out int x, out int y)
        {
            x = 0;
            y = 0;
            return TryGetPlaceIndex(stepId, out int index)
                && TutorialAuthoredBoard.TryGetHumanTurnCell(ActiveHumanTurn(stepId), index, out x, out y, out _);
        }

        public static bool IsConfirmStep(string stepId) =>
            stepId == IntroTutorialSequence.ConfirmStepId
            || stepId == ConnectTutorialSequence.ConfirmStepId
            || stepId == PremiumTutorialSequence.ConfirmStepId;

        public static bool IsPassStep(string stepId) =>
            stepId == PremiumTutorialSequence.PassStepId;

        public static bool IsExchangeStep(string stepId) =>
            stepId == PremiumTutorialSequence.ExchangeStepId;

        public static bool IsOpponentStep(string stepId) =>
            stepId == IntroTutorialSequence.OpponentStepId;

        public static bool IsPassLessonRestart(string stepId) =>
            stepId == PremiumTutorialSequence.PassStepId
            || stepId == PremiumTutorialSequence.PassIntroStepId;

        public static bool IsExchangeLessonRestart(string stepId) =>
            stepId == PremiumTutorialSequence.ExchangeStepId
            || stepId == PremiumTutorialSequence.ExchangeIntroStepId;

        public static int CompletedTurnsBefore(string stepId)
        {
            if (stepId == IntroTutorialSequence.OpponentStepId) return 1;
            if (stepId == IntroTutorialSequence.CompleteStepId) return 2;
            if (stepId == ConnectTutorialSequence.CompleteStepId) return 1;
            if (stepId == PremiumTutorialSequence.PassStepId
                || stepId == PremiumTutorialSequence.PassIntroStepId) return 1;
            if (stepId == PremiumTutorialSequence.ExchangeStepId
                || stepId == PremiumTutorialSequence.ExchangeIntroStepId) return 1;
            if (stepId == PremiumTutorialSequence.CompleteStepId) return 1;
            return 0;
        }

        /// <summary>
        /// Number of scripted human placements that must already be present
        /// in the local draft when resuming or replaying this step.
        /// </summary>
        public static int PendingPlacementsBefore(string stepId)
        {
            if (TryGetSelectIndex(stepId, out int index) || TryGetPlaceIndex(stepId, out index))
                return index;

            if (IsConfirmStep(stepId))
                return ActiveHumanTurn(stepId).Count;

            return 0;
        }

        private static bool TryGetSelectIndex(string stepId, out int index)
        {
            index = -1;
            switch (stepId)
            {
                case IntroTutorialSequence.Select1StepId:
                    index = 0; return true;
                case IntroTutorialSequence.SelectPlusStepId:
                    index = 1; return true;
                case IntroTutorialSequence.Select2StepId:
                    index = 2; return true;
                case IntroTutorialSequence.SelectEqualsStepId:
                    index = 3; return true;
                case IntroTutorialSequence.Select3StepId:
                    index = 4; return true;

                case ConnectTutorialSequence.SelectPlusStepId:
                    index = 0; return true;
                case ConnectTutorialSequence.Select4StepId:
                    index = 1; return true;
                case ConnectTutorialSequence.SelectEqualsStepId:
                    index = 2; return true;
                case ConnectTutorialSequence.Select7StepId:
                    index = 3; return true;

                case PremiumTutorialSequence.SelectPlusStepId:
                    index = 0; return true;
                case PremiumTutorialSequence.Select2StepId:
                    index = 1; return true;
                case PremiumTutorialSequence.SelectEqualsStepId:
                    index = 2; return true;
                case PremiumTutorialSequence.Select5StepId:
                    index = 3; return true;
                default: return false;
            }
        }

        private static bool TryGetPlaceIndex(string stepId, out int index)
        {
            index = -1;
            switch (stepId)
            {
                case IntroTutorialSequence.Place1StepId: index = 0; return true;
                case IntroTutorialSequence.PlacePlusStepId: index = 1; return true;
                case IntroTutorialSequence.Place2StepId: index = 2; return true;
                case IntroTutorialSequence.PlaceEqualsStepId: index = 3; return true;
                case IntroTutorialSequence.Place3StepId: index = 4; return true;
                case ConnectTutorialSequence.PlacePlusStepId: index = 0; return true;
                case ConnectTutorialSequence.Place4StepId: index = 1; return true;
                case ConnectTutorialSequence.PlaceEqualsStepId: index = 2; return true;
                case ConnectTutorialSequence.Place7StepId: index = 3; return true;
                case PremiumTutorialSequence.PlacePlusStepId: index = 0; return true;
                case PremiumTutorialSequence.Place2StepId: index = 1; return true;
                case PremiumTutorialSequence.PlaceEqualsStepId: index = 2; return true;
                case PremiumTutorialSequence.Place5StepId: index = 3; return true;
                default: return false;
            }
        }

        private static IReadOnlyList<TilePlacement> ActiveHumanTurn(string stepId)
        {
            if (stepId.StartsWith("premium"))
                return TutorialAuthoredBoard.Premium.HumanTurn;
            if (stepId.StartsWith("connect"))
                return TutorialAuthoredBoard.Connect.HumanTurn;
            return TutorialAuthoredBoard.Intro.HumanTurn;
        }
    }
}
