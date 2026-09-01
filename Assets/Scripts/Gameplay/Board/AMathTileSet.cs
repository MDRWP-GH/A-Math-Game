using System.Collections.Generic;

namespace AMath.Gameplay.Board
{
    /// <summary>
    /// Static definition of the official 100-tile A-Math set.
    ///
    /// Tiles are identified by a single byte:
    ///   0..20  = number tiles with that face value
    ///   21..27 = operators (+, -, +/-, x, ÷, x/÷, =)
    ///   28     = blank
    /// Data-driven tables keep validation, scoring, bag and UI consistent, and
    /// a byte id keeps every network payload and save minimal.
    /// </summary>
    public static class AMathTileSet
    {
        #region Tile ids

        public const byte Plus = 21;
        public const byte Minus = 22;
        public const byte PlusOrMinus = 23;
        public const byte Times = 24;
        public const byte Divide = 25;
        public const byte TimesOrDivide = 26;
        // Named EqualsSign (not "Equals") to avoid colliding with
        // object.Equals, which would shadow the constant in "using static"
        // contexts and produce baffling compile errors.
        public const byte EqualsSign = 27;
        public const byte Blank = 28;

        /// <summary>Total distinct tile ids.</summary>
        public const int TileTypeCount = 29;

        #endregion

        #region Data tables

        // Index = tile id. Official A-Math point values.
        private static readonly byte[] Points =
        {
            1, 1, 1, 1, 2, 2, 2, 2, 2, 2,          // 0..9
            3, 4, 3, 6, 4, 4, 4, 6, 4, 7, 5,       // 10..20
            2, 2, 1, 2, 2, 1, 1,                   // + - +/- x ÷ x/÷ =
            0                                       // blank
        };

        // Index = tile id. Official A-Math distribution (sums to 100).
        private static readonly byte[] Distribution =
        {
            5, 6, 6, 5, 5, 4, 4, 4, 4, 4,           // 0..9
            2, 1, 2, 1, 1, 1, 1, 1, 1, 1, 1,        // 10..20
            4, 4, 5, 4, 4, 4, 11,                   // + - +/- x ÷ x/÷ =
            4                                        // blank
        };

        private static readonly string[] Symbols =
        {
            "0","1","2","3","4","5","6","7","8","9",
            "10","11","12","13","14","15","16","17","18","19","20",
            "+","-","±","×","÷","×/÷","=","?"
        };

        #endregion

        #region Queries

        /// <summary>True for a byte that names a real tile.</summary>
        public static bool IsValidTileId(byte tileId) => tileId < TileTypeCount;

        /// <summary>
        /// Point value of a tile. Blank is always worth 0, and so is anything
        /// outside the set: a corrupt id reaching end-of-match scoring or the
        /// HUD must not take the match down with an index error. Validation is
        /// the place that rejects such ids, not the lookup tables.
        /// </summary>
        public static int PointsOf(byte tileId) => IsValidTileId(tileId) ? Points[tileId] : 0;

        /// <summary>Human-readable symbol (UI / replay text).</summary>
        public static string SymbolOf(byte tileId) => IsValidTileId(tileId) ? Symbols[tileId] : "?";

        /// <summary>Short label for rack/HUD: symbol plus official face points.</summary>
        public static string SymbolWithPoints(byte tileId) =>
            $"{SymbolOf(tileId)} ({PointsOf(tileId)})";

        /// <summary>Thai/English-neutral description of face value for teaching UI.</summary>
        public static string DescribeFaceValue(byte tileId)
        {
            int points = PointsOf(tileId);
            if (tileId == Blank)
                return $"{SymbolOf(tileId)} = 0 pts (blank)";
            return $"{SymbolOf(tileId)} = {points} pts";
        }

        /// <summary>True for single-digit number tiles (0..9), which may combine into multi-digit numbers.</summary>
        public static bool IsSingleDigit(byte tileId) => tileId <= 9;

        /// <summary>True for double-digit number tiles (10..20), which must always stand alone.</summary>
        public static bool IsDoubleDigit(byte tileId) => tileId >= 10 && tileId <= 20;

        /// <summary>True for any number tile (0..20).</summary>
        public static bool IsNumber(byte tileId) => tileId <= 20;

        /// <summary>True for resolved binary operators (+, -, x, ÷).</summary>
        public static bool IsResolvedOperator(byte tileId) =>
            tileId == Plus || tileId == Minus || tileId == Times || tileId == Divide;

        /// <summary>True for tiles that must declare an identity when placed (blank, +/-, x/÷).</summary>
        public static bool RequiresDeclaration(byte tileId) =>
            tileId == Blank || tileId == PlusOrMinus || tileId == TimesOrDivide;

        /// <summary>Validates that a declaration is legal for the given physical tile.</summary>
        public static bool IsLegalDeclaration(byte tileId, byte declaredAs)
        {
            switch (tileId)
            {
                case Blank:
                    // A blank may represent any concrete tile — but not another
                    // blank, and not the flexible operator tiles (declaring a
                    // blank as "±" would still leave the operator unresolved).
                    return declaredAs < Blank && declaredAs != PlusOrMinus && declaredAs != TimesOrDivide;
                case PlusOrMinus:
                    return declaredAs == Plus || declaredAs == Minus;
                case TimesOrDivide:
                    return declaredAs == Times || declaredAs == Divide;
                default:
                    return declaredAs == TilePlacement.NoDeclaration;
            }
        }

        /// <summary>Builds the full 100-tile multiset in canonical (unshuffled) order.</summary>
        public static List<byte> CreateFullSet()
        {
            var tiles = new List<byte>(100);
            for (byte id = 0; id < TileTypeCount; id++)
                for (int n = 0; n < Distribution[id]; n++)
                    tiles.Add(id);
            return tiles;
        }

        #endregion
    }
}
