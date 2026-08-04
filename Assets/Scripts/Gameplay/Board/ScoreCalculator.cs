using System.Collections.Generic;
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
        /// <param name="lines">Formed lines from <see cref="PlacementValidator"/>.</param>
        /// <param name="tilesPlaced">Number of tiles the player placed this turn.</param>
        public int Calculate(IReadOnlyList<FormedLine> lines, int tilesPlaced)
        {
            int total = 0;

            for (int l = 0; l < lines.Count; l++)
            {
                FormedLine line = lines[l];
                int lineSum = 0;
                int equationMultiplier = 1;

                for (int c = 0; c < line.Cells.Count; c++)
                {
                    LineCell cell = line.Cells[c];
                    // Physical tile determines points (a blank is always 0,
                    // regardless of what it declares).
                    int points = AMathTileSet.PointsOf(cell.TileId);

                    if (cell.IsNew)
                    {
                        switch (BoardGrid.PremiumAt(cell.X, cell.Y))
                        {
                            case PremiumType.TileX2: points *= 2; break;
                            case PremiumType.TileX3: points *= 3; break;
                            case PremiumType.EquationX2: equationMultiplier *= 2; break;
                            case PremiumType.EquationX3: equationMultiplier *= 3; break;
                        }
                    }

                    lineSum += points;
                }

                total += lineSum * equationMultiplier;
            }

            if (tilesPlaced == GameRules.RackSize)
                total += GameRules.FullRackBonus;

            return total;
        }
    }
}
