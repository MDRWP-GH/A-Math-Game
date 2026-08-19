using System.Collections.Generic;
using System.Text;
using AMath.Core;

namespace AMath.Gameplay.Board
{
    /// <summary>
    /// Official A-Math scoring. Executed exclusively by the host — clients only
    /// re-run it locally to verify the broadcast result (desync detection),
    /// never to produce an official score.
    ///
    /// Rules:
    ///  - every formed equation scores the sum of its tile points;
    ///  - tile premiums (x2/x3) apply only to tiles placed this turn;
    ///  - equation premiums (x2/x3) multiply the whole line, only when a new
    ///    tile sits on the premium cell; multiple stack multiplicatively;
    ///  - a tile shared by two equations scores in both;
    ///  - using the entire rack awards <see cref="GameRules.FullRackBonus"/>.
    /// </summary>
    public sealed class ScoreCalculator
    {
        /// <summary>Computes the total score for a validated placement.</summary>
        public int Calculate(IReadOnlyList<FormedLine> lines, int tilesPlaced) =>
            CalculateDetailed(lines, tilesPlaced).Total;

        /// <summary>
        /// Same rules as <see cref="Calculate"/>, but returns a per-tile /
        /// per-equation breakdown for UI preview and teaching tools.
        /// </summary>
        public PlacementScoreBreakdown CalculateDetailed(IReadOnlyList<FormedLine> lines, int tilesPlaced)
        {
            var breakdown = new PlacementScoreBreakdown();
            if (lines == null) return breakdown;

            for (int l = 0; l < lines.Count; l++)
            {
                FormedLine line = lines[l];
                var eq = new EquationScoreLine
                {
                    EquationText = FormatEquation(line)
                };

                int lineSum = 0;
                int equationMultiplier = 1;

                for (int c = 0; c < line.Cells.Count; c++)
                {
                    LineCell cell = line.Cells[c];
                    int face = AMathTileSet.PointsOf(cell.TileId);
                    int tilePremium = 1;
                    PremiumType premium = PremiumType.None;

                    if (cell.IsNew)
                    {
                        premium = BoardGrid.PremiumAt(cell.X, cell.Y);
                        switch (premium)
                        {
                            case PremiumType.TileX2: tilePremium = 2; break;
                            case PremiumType.TileX3: tilePremium = 3; break;
                            case PremiumType.EquationX2: equationMultiplier *= 2; break;
                            case PremiumType.EquationX3: equationMultiplier *= 3; break;
                        }
                    }

                    int beforeEq = face * tilePremium;
                    lineSum += beforeEq;

                    eq.Tiles.Add(new TileScoreContribution
                    {
                        X = cell.X,
                        Y = cell.Y,
                        TileId = cell.TileId,
                        EffectiveTileId = cell.EffectiveTileId,
                        IsNew = cell.IsNew,
                        FacePoints = face,
                        TilePremium = tilePremium,
                        PointsBeforeEquationMultiplier = beforeEq,
                        CellPremium = premium
                    });
                }

                eq.EquationMultiplier = equationMultiplier;
                eq.LineSubtotal = lineSum * equationMultiplier;
                breakdown.Equations.Add(eq);
                breakdown.Total += eq.LineSubtotal;
            }

            if (tilesPlaced == GameRules.RackSize)
            {
                breakdown.FullRackBonus = GameRules.FullRackBonus;
                breakdown.Total += breakdown.FullRackBonus;
            }

            AccumulateNewTileTotals(breakdown);
            return breakdown;
        }

        private static void AccumulateNewTileTotals(PlacementScoreBreakdown breakdown)
        {
            // Aggregate new-tile face×premium across equations (shared tiles add in each line).
            var map = new Dictionary<int, TileScoreContribution>();
            for (int e = 0; e < breakdown.Equations.Count; e++)
            {
                EquationScoreLine eq = breakdown.Equations[e];
                for (int t = 0; t < eq.Tiles.Count; t++)
                {
                    TileScoreContribution tile = eq.Tiles[t];
                    if (!tile.IsNew) continue;

                    int key = (tile.X << 8) | tile.Y;
                    int weighted = tile.PointsBeforeEquationMultiplier * eq.EquationMultiplier;
                    if (map.TryGetValue(key, out TileScoreContribution existing))
                    {
                        existing.PointsBeforeEquationMultiplier += weighted;
                    }
                    else
                    {
                        map[key] = new TileScoreContribution
                        {
                            X = tile.X,
                            Y = tile.Y,
                            TileId = tile.TileId,
                            EffectiveTileId = tile.EffectiveTileId,
                            IsNew = true,
                            FacePoints = tile.FacePoints,
                            TilePremium = tile.TilePremium,
                            PointsBeforeEquationMultiplier = weighted,
                            CellPremium = tile.CellPremium
                        };
                    }
                }
            }

            foreach (TileScoreContribution value in map.Values)
                breakdown.NewTileTotals.Add(value);
        }

        /// <summary>Human-readable equation using effective symbols, e.g. "1+2=3".</summary>
        public static string FormatEquation(FormedLine line)
        {
            if (line == null || line.Cells.Count == 0) return string.Empty;
            var text = new StringBuilder(line.Cells.Count * 2);
            for (int i = 0; i < line.Cells.Count; i++)
                text.Append(AMathTileSet.SymbolOf(line.Cells[i].EffectiveTileId));
            return text.ToString();
        }
    }
}
