using System.Collections.Generic;
using AMath.Core.Commands;
using AMath.Gameplay.AI;
using AMath.Gameplay.Board;
using NUnit.Framework;
using static AMath.Gameplay.Board.AMathTileSet;

namespace AMath.Tests
{
    public sealed class MediumAiMoveChooserTests
    {
        [Test]
        public void Choose_FirstMove_PlacesValidEquationThroughCenter()
        {
            var board = new BoardManager();
            var chooser = new MediumAiMoveChooser();
            var rack = new List<byte> { 1, Plus, 2, EqualsSign, 3, 4, 5, 6 };

            IGameCommand command = chooser.Choose(board, rack, bagCount: 50, choiceSeed: 42);

            Assert.IsInstanceOf<PlaceTilesCommand>(command);
            var place = (PlaceTilesCommand)command;
            PlacementValidation validation = board.Validate(rack, place.Placements);
            Assert.IsTrue(validation.IsValid, validation.Error);

            bool coversCenter = false;
            foreach (TilePlacement p in place.Placements)
                coversCenter |= p.X == 7 && p.Y == 7;
            Assert.IsTrue(coversCenter);
        }

        [Test]
        public void Choose_WhenNoEquationPossible_PassesOrExchanges()
        {
            var board = new BoardManager();
            var chooser = new MediumAiMoveChooser();
            // Operators only — cannot form a = b equation with numbers.
            var rack = new List<byte> { Plus, Minus, Times, Divide, Plus, Minus, Times, Divide };

            IGameCommand withBag = chooser.Choose(board, rack, bagCount: 10, choiceSeed: 1);
            Assert.IsTrue(withBag is ExchangeTilesCommand || withBag is PassTurnCommand);

            IGameCommand emptyBag = chooser.Choose(board, rack, bagCount: 0, choiceSeed: 1);
            Assert.IsInstanceOf<PassTurnCommand>(emptyBag);
        }

        [Test]
        public void Choose_MediumBand_IsDeterministicForSameSeed()
        {
            var board = new BoardManager();
            var chooser = new MediumAiMoveChooser();
            var rack = new List<byte> { 1, Plus, 2, EqualsSign, 3, 4, EqualsSign, 5 };

            var a = (PlaceTilesCommand)chooser.Choose(board, rack, 50, choiceSeed: 99);
            var b = (PlaceTilesCommand)chooser.Choose(board, rack, 50, choiceSeed: 99);

            Assert.AreEqual(a.Placements.Count, b.Placements.Count);
            for (int i = 0; i < a.Placements.Count; i++)
                Assert.AreEqual(a.Placements[i], b.Placements[i]);
        }
    }
}
