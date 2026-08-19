using System.Collections.Generic;
using AMath.Core;

namespace AMath.Gameplay.Board
{
    /// <summary>How one physical tile contributed inside one scored equation line.</summary>
    public sealed class TileScoreContribution
    {
        public byte X;
        public byte Y;
        public byte TileId;
        public byte EffectiveTileId;
        public bool IsNew;
        /// <summary>Face value from the official A-Math tile set (blank = 0).</summary>
        public int FacePoints;
        /// <summary>Tile premium applied to this cell this turn (1, 2, or 3).</summary>
        public int TilePremium;
        /// <summary>FacePoints × TilePremium before the equation multiplier.</summary>
        public int PointsBeforeEquationMultiplier;
        public PremiumType CellPremium;
    }

    /// <summary>One formed equation and its subtotal.</summary>
    public sealed class EquationScoreLine
    {
        public string EquationText;
        public int EquationMultiplier = 1;
        public int LineSubtotal;
        public readonly List<TileScoreContribution> Tiles = new(GameRules.BoardSize);
    }

    /// <summary>Full A-Math score breakdown for a validated placement.</summary>
    public sealed class PlacementScoreBreakdown
    {
        public int Total;
        public int FullRackBonus;
        public readonly List<EquationScoreLine> Equations = new(4);
        /// <summary>Per new tile: sum of contributions across all equations (shared tiles count twice).</summary>
        public readonly List<TileScoreContribution> NewTileTotals = new(GameRules.RackSize);
    }
}
