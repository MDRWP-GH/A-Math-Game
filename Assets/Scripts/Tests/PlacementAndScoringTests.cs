using System.Collections.Generic;
using AMath.Gameplay.Board;
using NUnit.Framework;
using static AMath.Gameplay.Board.AMathTileSet;

namespace AMath.Tests
{
    /// <summary>
    /// Exercises the exact validation + scoring pipeline the host runs on
    /// every client request — including the anti-cheat rack ownership check.
    /// </summary>
    public sealed class PlacementAndScoringTests
    {
        private static TilePlacement P(byte tile, int x, int y, byte declaredAs = TilePlacement.NoDeclaration) =>
            new() { TileId = tile, X = (byte)x, Y = (byte)y, DeclaredAs = declaredAs };

        private static List<byte> Rack(params byte[] tiles) => new(tiles);

        // "1 + 2 = 3" horizontally through the center square.
        private static List<TilePlacement> FirstEquation() => new()
        {
            P(1, 5, 7), P(Plus, 6, 7), P(2, 7, 7), P(EqualsSign, 8, 7), P(3, 9, 7)
        };

        [Test]
        public void ValidFirstMove_IsAcceptedAndScored()
        {
            var board = new BoardManager();
            List<TilePlacement> placements = FirstEquation();

            PlacementValidation validation = board.Validate(Rack(1, Plus, 2, EqualsSign, 3, 7, 8, 9), placements);
            Assert.IsTrue(validation.IsValid, validation.Error);

            // Tile points: 1(1) + '+'(2) + 2(1) + '='(1) + 3(1) = 6;
            // the center square doubles the equation => 12.
            int score = board.Commit(validation, placements);
            Assert.AreEqual(12, score);
        }

        [Test]
        public void FirstMove_MissingCenter_IsRejected()
        {
            var board = new BoardManager();
            var placements = new List<TilePlacement>
            {
                P(1, 0, 0), P(Plus, 1, 0), P(2, 2, 0), P(EqualsSign, 3, 0), P(3, 4, 0)
            };

            Assert.IsFalse(board.Validate(Rack(1, Plus, 2, EqualsSign, 3), placements).IsValid);
        }

        [Test]
        public void TilesNotInRack_AreRejected_NoTileSpawning()
        {
            var board = new BoardManager();
            // Rack lacks the '3' the request tries to place.
            PlacementValidation validation = board.Validate(Rack(1, Plus, 2, EqualsSign), FirstEquation());

            Assert.IsFalse(validation.IsValid);
            StringAssert.Contains("own", validation.Error);
        }

        [Test]
        public void DisconnectedSecondMove_IsRejected()
        {
            var board = new BoardManager();
            List<TilePlacement> first = FirstEquation();
            PlacementValidation validation = board.Validate(Rack(1, Plus, 2, EqualsSign, 3), first);
            board.Commit(validation, first);

            // Far away from the existing crossword.
            var second = new List<TilePlacement>
            {
                P(4, 0, 0), P(EqualsSign, 1, 0), P(4, 2, 0)
            };
            Assert.IsFalse(board.Validate(Rack(4, EqualsSign, 4), second).IsValid);
        }

        [Test]
        public void BlankTile_MustDeclare_AndScoresZero()
        {
            var board = new BoardManager();

            // Blank used as the '3' in "1+2=3": undeclared must fail.
            var undeclared = FirstEquation();
            undeclared[4] = P(Blank, 9, 7);
            Assert.IsFalse(board.Validate(Rack(1, Plus, 2, EqualsSign, Blank), undeclared).IsValid);

            // Declared as 3: valid, but the blank contributes 0 points.
            var declared = FirstEquation();
            declared[4] = P(Blank, 9, 7, declaredAs: 3);
            PlacementValidation validation = board.Validate(Rack(1, Plus, 2, EqualsSign, Blank), declared);
            Assert.IsTrue(validation.IsValid, validation.Error);

            int score = board.Commit(validation, declared);
            Assert.AreEqual(10, score); // (1+2+1+1+0) x2 center
        }
    }
}
