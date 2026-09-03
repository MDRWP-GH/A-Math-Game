using System.Collections.Generic;
using AMath.Core;

namespace AMath.Gameplay.Board
{
    /// <summary>Premium multiplier types on board cells.</summary>
    public enum PremiumType : byte
    {
        None = 0,
        TileX2 = 1,
        TileX3 = 2,
        EquationX2 = 3,
        EquationX3 = 4
    }

    /// <summary>A tile that has been committed to a board cell.</summary>
    public struct PlacedTile
    {
        public bool Occupied;
        public byte TileId;
        public byte DeclaredAs;

        /// <summary>Effective id for equation math (declaration wins).</summary>
        public readonly byte EffectiveTileId => DeclaredAs == TilePlacement.NoDeclaration ? TileId : DeclaredAs;
    }

    /// <summary>
    /// Pure 15x15 board model: cell contents plus the static premium layout.
    /// No Unity scene objects and no networking — the same grid instance is
    /// used live, in replay reconstruction and in host-migration restores.
    /// </summary>
    public sealed class BoardGrid
    {
        #region Premium layout

        // Standard A-Math premium pattern (same geometry as the official board).
        // '3' = equation x3, '2' = equation x2, 'T' = tile x3, 'D' = tile x2,
        // '*' = center (counts as equation x2), '.' = plain.
        private static readonly string[] LayoutRows =
        {
            "3..D...3...D..3",
            ".2...T...T...2.",
            "..2...D.D...2..",
            "D..2...D...2..D",
            "....2.....2....",
            ".T...T...T...T.",
            "..D...D.D...D..",
            "3..D...*...D..3",
            "..D...D.D...D..",
            ".T...T...T...T.",
            "....2.....2....",
            "D..2...D...2..D",
            "..2...D.D...2..",
            ".2...T...T...2.",
            "3..D...3...D..3"
        };

        private static readonly PremiumType[] Premiums = BuildPremiums();

        private static PremiumType[] BuildPremiums()
        {
            var premiums = new PremiumType[GameRules.BoardSize * GameRules.BoardSize];
            for (int y = 0; y < GameRules.BoardSize; y++)
            {
                for (int x = 0; x < GameRules.BoardSize; x++)
                {
                    premiums[y * GameRules.BoardSize + x] = LayoutRows[y][x] switch
                    {
                        '3' => PremiumType.EquationX3,
                        '2' => PremiumType.EquationX2,
                        '*' => PremiumType.EquationX2,
                        'T' => PremiumType.TileX3,
                        'D' => PremiumType.TileX2,
                        _ => PremiumType.None
                    };
                }
            }

            return premiums;
        }

        #endregion

        #region Fields

        private readonly PlacedTile[] _cells = new PlacedTile[GameRules.BoardSize * GameRules.BoardSize];

        #endregion

        #region Properties

        /// <summary>Number of tiles currently on the board.</summary>
        public int PlacedCount { get; private set; }

        /// <summary>True before the first equation has been played.</summary>
        public bool IsEmpty => PlacedCount == 0;

        #endregion

        #region Queries

        /// <summary>True when the coordinate is on the board.</summary>
        public static bool InBounds(int x, int y) =>
            x >= 0 && x < GameRules.BoardSize && y >= 0 && y < GameRules.BoardSize;

        /// <summary>Premium of a cell (static layout).</summary>
        public static PremiumType PremiumAt(int x, int y) => Premiums[y * GameRules.BoardSize + x];

        /// <summary>Cell contents.</summary>
        public PlacedTile CellAt(int x, int y) => _cells[y * GameRules.BoardSize + x];

        /// <summary>True when the cell holds a tile.</summary>
        public bool IsOccupied(int x, int y) => _cells[y * GameRules.BoardSize + x].Occupied;

        #endregion

        #region Mutation

        /// <summary>Commits a validated placement to the grid.</summary>
        public void Place(in TilePlacement placement)
        {
            int index = placement.Y * GameRules.BoardSize + placement.X;
            _cells[index] = new PlacedTile
            {
                Occupied = true,
                TileId = placement.TileId,
                DeclaredAs = placement.DeclaredAs
            };
            PlacedCount++;
        }

        /// <summary>Clears the whole grid (new match / restore).</summary>
        public void Clear()
        {
            System.Array.Clear(_cells, 0, _cells.Length);
            PlacedCount = 0;
        }

        #endregion

        #region Snapshot support

        /// <summary>Exports only occupied cells (sparse — small saves and resync payloads).</summary>
        public void ExportOccupied(List<TilePlacement> destination)
        {
            for (int y = 0; y < GameRules.BoardSize; y++)
            {
                for (int x = 0; x < GameRules.BoardSize; x++)
                {
                    PlacedTile cell = _cells[y * GameRules.BoardSize + x];
                    if (!cell.Occupied) continue;
                    destination.Add(new TilePlacement
                    {
                        TileId = cell.TileId,
                        X = (byte)x,
                        Y = (byte)y,
                        DeclaredAs = cell.DeclaredAs
                    });
                }
            }
        }

        /// <summary>Restores the grid from exported cells.</summary>
        public void Restore(IReadOnlyList<TilePlacement> cells)
        {
            Clear();
            if (cells == null)
                return;

            for (int i = 0; i < cells.Count; i++)
            {
                TilePlacement cell = cells[i];
                if (!InBounds(cell.X, cell.Y))
                    continue;

                // A duplicated coordinate in a corrupt snapshot would overwrite
                // the cell but count twice, leaving PlacedCount (and therefore
                // IsEmpty, which gates the centre-square rule) permanently wrong.
                if (IsOccupied(cell.X, cell.Y))
                    continue;

                Place(in cell);
            }
        }

        #endregion
    }
}
