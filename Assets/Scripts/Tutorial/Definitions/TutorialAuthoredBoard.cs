using System.Collections.Generic;
using AMath.Gameplay.Board;
using static AMath.Gameplay.Board.AMathTileSet;

namespace AMath.Tutorial.Definitions
{
    /// <summary>
    /// Single source of truth for scripted tutorial board cells, racks, and turns.
    /// Sequences, routing, and match scripts must read from here.
    /// </summary>
    internal static class TutorialAuthoredBoard
    {
        public static class Intro
        {
            public static readonly TilePlacement[] HumanTurn =
            {
                P(1, 5, 7),
                P(Plus, 6, 7),
                P(2, 7, 7),
                P(EqualsSign, 8, 7),
                P(3, 9, 7)
            };

            public static readonly TilePlacement[] BotTurn =
            {
                P(Plus, 7, 8),
                P(2, 7, 9),
                P(EqualsSign, 7, 10),
                P(4, 7, 11)
            };
        }

        public static class Connect
        {
            public static readonly TilePlacement[] Initial =
            {
                P(1, 5, 7),
                P(Plus, 6, 7),
                P(2, 7, 7),
                P(EqualsSign, 8, 7),
                P(3, 9, 7)
            };

            public static readonly TilePlacement[] HumanTurn =
            {
                P(Plus, 9, 8),
                P(4, 9, 9),
                P(EqualsSign, 9, 10),
                P(7, 9, 11)
            };
        }

        public static class Premium
        {
            public const int PremiumCellX = 8;
            public const int PremiumCellY = 8;

            public static readonly TilePlacement[] Initial =
            {
                P(1, 4, 6),
                P(Plus, 5, 6),
                P(2, 6, 6),
                P(EqualsSign, 7, 6),
                P(3, 8, 6)
            };

            public static readonly TilePlacement[] HumanTurn =
            {
                P(Plus, 8, 7),
                P(2, PremiumCellX, PremiumCellY),
                P(EqualsSign, 8, 9),
                P(5, 8, 10)
            };

            public static readonly TilePlacement[] LessonBoard = Combine(Initial, HumanTurn);

            public static readonly byte[] PassRack = { 8, 8, 9, 9, 10, 10, 11, 12 };

            public static readonly byte[] ExchangeRack = { 13, 14, 15, 16, 17, 18, 19, 20 };

            public static readonly int[] ExchangeIndices = { 0, 1 };
        }

        internal static (int x, int y) Cell(IReadOnlyList<TilePlacement> turn, int index) =>
            (turn[index].X, turn[index].Y);

        internal static bool TryGetHumanTurnCell(
            IReadOnlyList<TilePlacement> turn,
            int index,
            out int x,
            out int y,
            out byte tileId)
        {
            x = 0;
            y = 0;
            tileId = 0;
            if (turn == null || index < 0 || index >= turn.Count)
                return false;

            TilePlacement placement = turn[index];
            x = placement.X;
            y = placement.Y;
            tileId = placement.TileId;
            return true;
        }

        private static TilePlacement[] Combine(
            IReadOnlyList<TilePlacement> left,
            IReadOnlyList<TilePlacement> right)
        {
            var combined = new TilePlacement[left.Count + right.Count];
            for (int i = 0; i < left.Count; i++)
                combined[i] = left[i];
            for (int i = 0; i < right.Count; i++)
                combined[left.Count + i] = right[i];
            return combined;
        }

        private static TilePlacement P(byte tileId, int x, int y) => new()
        {
            TileId = tileId,
            X = (byte)x,
            Y = (byte)y,
            DeclaredAs = TilePlacement.NoDeclaration
        };
    }
}
