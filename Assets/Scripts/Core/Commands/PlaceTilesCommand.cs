using System.Collections.Generic;
using AMath.Gameplay.Board;

namespace AMath.Core.Commands
{
    /// <summary>
    /// Request to place tiles from the acting player's rack onto the board,
    /// forming one or more valid equations. The host performs full validation
    /// (rack ownership, geometry, equation math, scoring); clients only *request*.
    /// </summary>
    public sealed class PlaceTilesCommand : IGameCommand
    {
        /// <inheritdoc />
        public CommandType Type => CommandType.PlaceTiles;

        /// <summary>Tiles to place this turn (1..RackSize entries).</summary>
        public List<TilePlacement> Placements { get; } = new(GameRules.RackSize);
    }
}
