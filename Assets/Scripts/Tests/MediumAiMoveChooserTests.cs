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

        /// <summary>
        /// The bug this covers: mapping rejected every occupied cell, so once the
        /// centre was taken the AI could not continue an existing equation and
        /// fell through to exchange/pass for the rest of the match.
        /// </summary>
        [Test]
        public void Choose_MidGame_HooksOntoATileAlreadyOnTheBoard()
        {
            var board = new BoardManager();
            Place(board, tileId: 2, x: 7, y: 7);

            var chooser = new MediumAiMoveChooser();

            // Only one 2 in hand: the second 2 of "2+2=4" has to be the board's.
            var rack = new List<byte> { Plus, 2, EqualsSign, 4, Times, Minus, Divide, Plus };

            IGameCommand command = chooser.Choose(board, rack, bagCount: 40, choiceSeed: 7);

            Assert.IsInstanceOf<PlaceTilesCommand>(
                command,
                "AI should hook onto the board tile instead of exchanging.");

            var place = (PlaceTilesCommand)command;
            PlacementValidation validation = board.Validate(rack, place.Placements);
            Assert.IsTrue(validation.IsValid, validation.Error);

            foreach (TilePlacement p in place.Placements)
                Assert.IsFalse(board.Grid.IsOccupied(p.X, p.Y), "AI must not overwrite a placed tile.");
        }

        [Test]
        public void Choose_MidGame_NeverProposesAnInvalidPlacement()
        {
            var board = new BoardManager();
            Place(board, tileId: 3, x: 7, y: 7);
            Place(board, tileId: EqualsSign, x: 8, y: 7);
            Place(board, tileId: 3, x: 9, y: 7);

            var chooser = new MediumAiMoveChooser();
            var rack = new List<byte> { 1, 2, Plus, EqualsSign, 5, 6, Minus, 4 };

            for (int seed = 0; seed < 12; seed++)
            {
                IGameCommand command = chooser.Choose(board, rack, bagCount: 30, seed);
                if (command is not PlaceTilesCommand place) continue;

                PlacementValidation validation = board.Validate(rack, place.Placements);
                Assert.IsTrue(validation.IsValid, $"seed {seed}: {validation.Error}");
            }
        }

        private static void Place(BoardManager board, byte tileId, byte x, byte y)
        {
            board.Grid.Place(new TilePlacement
            {
                TileId = tileId,
                X = x,
                Y = y,
                DeclaredAs = TilePlacement.NoDeclaration
            });
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
