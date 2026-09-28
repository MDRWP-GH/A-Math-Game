using System.Collections.Generic;
using AMath.Gameplay.Board;

namespace AMath.Gameplay.Interaction
{
    /// <summary>
    /// Optional gate on local draft input. Play mode passes null (no extra
    /// rules). Tutorial injects a scripted constraint so the player still
    /// selects and places their own tiles, but only the authored cells/tiles
    /// are accepted.
    /// </summary>
    public interface ITurnInputConstraint
    {
        /// <summary>Whether a draft tile may be dropped on this cell.</summary>
        bool AllowsPlaceOnCell(
            byte tileId,
            byte declaredAs,
            int x,
            int y,
            IReadOnlyList<TilePlacement> pending,
            out string error);

        /// <summary>Whether the current draft may be submitted.</summary>
        bool AllowsConfirmPlace(IReadOnlyList<TilePlacement> pending, out string error);

        /// <summary>Whether passing the turn is allowed.</summary>
        bool AllowsPass(out string error);

        /// <summary>Whether exchanging the given rack indices is allowed.</summary>
        bool AllowsExchange(IReadOnlyList<int> rackIndices, out string error);
    }
}
