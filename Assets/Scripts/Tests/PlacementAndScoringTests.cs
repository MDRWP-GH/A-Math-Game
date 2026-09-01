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

            PlacementScoreBreakdown detail = new ScoreCalculator().CalculateDetailed(validation.Lines, placements.Count);
            Assert.AreEqual(12, detail.Total);
            Assert.AreEqual(1, detail.Equations.Count);
            Assert.AreEqual("1+2=3", detail.Equations[0].EquationText);
            Assert.AreEqual(2, detail.Equations[0].EquationMultiplier);
        }

        [Test]
        public void TileFacePoints_MatchOfficialAMathValues()
        {
            Assert.AreEqual(1, PointsOf(0));
            Assert.AreEqual(1, PointsOf(1));
            Assert.AreEqual(2, PointsOf(4));
            Assert.AreEqual(3, PointsOf(10));
            Assert.AreEqual(7, PointsOf(19));
            Assert.AreEqual(2, PointsOf(Plus));
            Assert.AreEqual(1, PointsOf(EqualsSign));
            Assert.AreEqual(0, PointsOf(Blank));
        }

        /// <summary>
        /// A corrupt rack used to index the ownership table out of range and
        /// throw out of the middle of the command instead of failing it.
        /// </summary>
        [Test]
        public void CorruptRackTileId_FailsValidation_WithoutThrowing()
        {
            var board = new BoardManager();
            List<TilePlacement> placements = FirstEquation();
            List<byte> rack = Rack(1, Plus, 2, EqualsSign, 3, 200, 8, 9);

            PlacementValidation validation = null;
            Assert.DoesNotThrow(() => validation = board.Validate(rack, placements));
            Assert.IsFalse(validation.IsValid);
        }

        [Test]
        public void NullRack_FailsValidation_WithoutThrowing()
        {
            var board = new BoardManager();
            List<TilePlacement> placements = FirstEquation();

            PlacementValidation validation = null;
            Assert.DoesNotThrow(() => validation = board.Validate(null, placements));
            Assert.IsFalse(validation.IsValid);
        }

        [Test]
        public void UnknownTileId_ScoresNothingAndRendersAsUnknown()
        {
            Assert.AreEqual(0, PointsOf(200));
            Assert.AreEqual("?", SymbolOf(200));
            Assert.IsFalse(IsValidTileId(200));
            Assert.IsTrue(IsValidTileId(Blank));
        }

        [Test]
        public void InvalidEquation_IsRejected_WithMathError()
        {
            var board = new BoardManager();
            var placements = new List<TilePlacement>
            {
                P(1, 5, 7), P(Plus, 6, 7), P(2, 7, 7), P(EqualsSign, 8, 7), P(4, 9, 7)
            };

            PlacementValidation validation = board.Validate(Rack(1, Plus, 2, EqualsSign, 4), placements);
            Assert.IsFalse(validation.IsValid);
            StringAssert.Contains("equal", validation.Error.ToLowerInvariant());
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
