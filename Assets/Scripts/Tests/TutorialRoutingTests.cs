using AMath.Tutorial.Definitions;
using AMath.Tutorial.Scripted;
using NUnit.Framework;

namespace AMath.Tests
{
    public sealed class TutorialRoutingTests
    {
        [Test]
        public void RoutingPlaceCells_MatchAuthoredHumanTurns()
        {
            AssertPlaceStep(IntroTutorialSequence.Place1StepId, TutorialAuthoredBoard.Intro.HumanTurn, 0);
            AssertPlaceStep(IntroTutorialSequence.PlacePlusStepId, TutorialAuthoredBoard.Intro.HumanTurn, 1);
            AssertPlaceStep(IntroTutorialSequence.Place2StepId, TutorialAuthoredBoard.Intro.HumanTurn, 2);
            AssertPlaceStep(IntroTutorialSequence.PlaceEqualsStepId, TutorialAuthoredBoard.Intro.HumanTurn, 3);
            AssertPlaceStep(IntroTutorialSequence.Place3StepId, TutorialAuthoredBoard.Intro.HumanTurn, 4);

            AssertPlaceStep(ConnectTutorialSequence.PlacePlusStepId, TutorialAuthoredBoard.Connect.HumanTurn, 0);
            AssertPlaceStep(ConnectTutorialSequence.Place4StepId, TutorialAuthoredBoard.Connect.HumanTurn, 1);
            AssertPlaceStep(ConnectTutorialSequence.PlaceEqualsStepId, TutorialAuthoredBoard.Connect.HumanTurn, 2);
            AssertPlaceStep(ConnectTutorialSequence.Place7StepId, TutorialAuthoredBoard.Connect.HumanTurn, 3);

            AssertPlaceStep(PremiumTutorialSequence.PlacePlusStepId, TutorialAuthoredBoard.Premium.HumanTurn, 0);
            AssertPlaceStep(PremiumTutorialSequence.Place2StepId, TutorialAuthoredBoard.Premium.HumanTurn, 1);
            AssertPlaceStep(PremiumTutorialSequence.PlaceEqualsStepId, TutorialAuthoredBoard.Premium.HumanTurn, 2);
            AssertPlaceStep(PremiumTutorialSequence.Place5StepId, TutorialAuthoredBoard.Premium.HumanTurn, 3);
        }

        [Test]
        public void SelectionAndPlacementSteps_AreMutuallyExclusive()
        {
            string[] selectSteps =
            {
                IntroTutorialSequence.Select1StepId, IntroTutorialSequence.SelectPlusStepId,
                IntroTutorialSequence.Select2StepId, IntroTutorialSequence.SelectEqualsStepId,
                IntroTutorialSequence.Select3StepId, ConnectTutorialSequence.SelectPlusStepId,
                ConnectTutorialSequence.Select4StepId, ConnectTutorialSequence.SelectEqualsStepId,
                ConnectTutorialSequence.Select7StepId, PremiumTutorialSequence.SelectPlusStepId,
                PremiumTutorialSequence.Select2StepId, PremiumTutorialSequence.SelectEqualsStepId,
                PremiumTutorialSequence.Select5StepId
            };
            string[] placeSteps =
            {
                IntroTutorialSequence.Place1StepId, IntroTutorialSequence.PlacePlusStepId,
                IntroTutorialSequence.Place2StepId, IntroTutorialSequence.PlaceEqualsStepId,
                IntroTutorialSequence.Place3StepId, ConnectTutorialSequence.PlacePlusStepId,
                ConnectTutorialSequence.Place4StepId, ConnectTutorialSequence.PlaceEqualsStepId,
                ConnectTutorialSequence.Place7StepId, PremiumTutorialSequence.PlacePlusStepId,
                PremiumTutorialSequence.Place2StepId, PremiumTutorialSequence.PlaceEqualsStepId,
                PremiumTutorialSequence.Place5StepId
            };

            foreach (string step in selectSteps)
            {
                Assert.IsTrue(TutorialStepRouting.TryGetSelectTile(step, out _), step);
                Assert.IsFalse(TutorialStepRouting.TryGetPlaceCell(step, out _, out _), step);
            }
            foreach (string step in placeSteps)
            {
                Assert.IsTrue(TutorialStepRouting.TryGetPlaceCell(step, out _, out _), step);
                Assert.IsFalse(TutorialStepRouting.TryGetSelectTile(step, out _), step);
            }
        }

        [Test]
        public void ScriptHumanTurns_MatchAuthoredBoard()
        {
            CollectionAssert.AreEqual(
                TutorialAuthoredBoard.Intro.HumanTurn,
                ScriptedTutorialMatchScript.Intro().Turns[0].Placements);
            CollectionAssert.AreEqual(
                TutorialAuthoredBoard.Connect.HumanTurn,
                ScriptedTutorialMatchScript.Connect().Turns[0].Placements);
            CollectionAssert.AreEqual(
                TutorialAuthoredBoard.Premium.HumanTurn,
                ScriptedTutorialMatchScript.PremiumSkills().Turns[0].Placements);
        }

        private static void AssertPlaceStep(
            string stepId,
            System.Collections.Generic.IReadOnlyList<AMath.Gameplay.Board.TilePlacement> turn,
            int index)
        {
            Assert.IsTrue(TutorialStepRouting.TryGetPlaceCell(stepId, out int x, out int y), stepId);
            Assert.AreEqual(turn[index].X, x, stepId);
            Assert.AreEqual(turn[index].Y, y, stepId);
        }
    }
}
