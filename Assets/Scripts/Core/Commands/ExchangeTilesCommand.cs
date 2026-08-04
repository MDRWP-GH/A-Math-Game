using System.Collections.Generic;

namespace AMath.Core.Commands
{
    /// <summary>
    /// Request to swap rack tiles with the bag and forfeit the turn.
    /// Replacements are drawn from the deterministic stream *before* the
    /// returned tiles re-enter the bag, then the bag is reshuffled — the exact
    /// order matters so every peer reproduces identical bag state.
    /// </summary>
    public sealed class ExchangeTilesCommand : IGameCommand
    {
        /// <inheritdoc />
        public CommandType Type => CommandType.ExchangeTiles;

        /// <summary>Tile ids (from the acting player's rack) to return to the bag.</summary>
        public List<byte> TileIds { get; } = new(GameRules.RackSize);
    }
}
