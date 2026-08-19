using System.Collections.Generic;
using AMath.Core.Commands;
using AMath.Gameplay.Board;

namespace AMath.Gameplay.AI
{
    /// <summary>
    /// Picks the next command for a scripted AI seat. Host-only: the chosen
    /// command still goes through <see cref="Managers.GameManager.SubmitCommand"/>.
    /// </summary>
    public interface IAiMoveChooser
    {
        /// <summary>
        /// Returns a place, exchange, or pass command for the given public board
        /// and the AI seat's own rack. Uses <paramref name="choiceSeed"/> only for
        /// medium-difficulty selection — never the match tile RNG.
        /// </summary>
        IGameCommand Choose(
            BoardManager board,
            IReadOnlyList<byte> rack,
            int bagCount,
            int choiceSeed);
    }
}
