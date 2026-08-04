using System.Collections.Generic;
using AMath.Gameplay.Board;

namespace AMath.Core.Assistance
{
    /// <summary>
    /// Read-only view over the board domain. Implemented by a thin adapter
    /// over <see cref="Gameplay.Board.BoardManager"/> and consumed only by
    /// systems that must never mutate gameplay (Tutorial, AI).
    /// </summary>
    public interface IBoardStateReader
    {
        /// <summary>Board width/height in cells (see <see cref="GameRules.BoardSize"/>).</summary>
        int BoardSize { get; }

        /// <summary>Every currently occupied cell. Empty cells are not listed.</summary>
        IReadOnlyList<TilePlacement> GetOccupiedCells();
    }
}
