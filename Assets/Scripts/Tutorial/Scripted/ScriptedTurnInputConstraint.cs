using System.Collections.Generic;
using AMath.Core.Assistance;
using AMath.Gameplay.Board;
using AMath.Gameplay.Interaction;

namespace AMath.Tutorial.Scripted
{
    /// <summary>
    /// Lets the player pick and place their own tiles, but only onto the
    /// cells (and with the tiles) named by the current scripted turn.
    /// Pass and exchange stay disabled for the whole tutorial match.
    /// </summary>
    public sealed class ScriptedTurnInputConstraint : ITurnInputConstraint
    {
        private readonly ILocalizedTextProvider _text;

        public ScriptedTurnInputConstraint(ILocalizedTextProvider text)
        {
            _text = text;
        }

        /// <summary>False until the tutorial reaches the player's placement step.</summary>
        public bool InputEnabled { get; set; }

        /// <summary>Required placements for the active player turn, or empty when locked.</summary>
        public IReadOnlyList<TilePlacement> Expected { get; set; } = System.Array.Empty<TilePlacement>();

        /// <inheritdoc />
        public bool AllowsPlaceOnCell(
            byte tileId,
            byte declaredAs,
            int x,
            int y,
            IReadOnlyList<TilePlacement> pending,
            out string error)
        {
            if (!InputEnabled)
            {
                error = Text("tutorial.error.not_your_step");
                return false;
            }

            if (!ScriptedPlacementRules.TryGetExpectedAt(Expected, x, y, out TilePlacement required))
            {
                error = Text("tutorial.error.off_script_cell");
                return false;
            }

            if (ScriptedPlacementRules.PendingOccupies(pending, x, y))
            {
                error = Text("tutorial.error.off_script_cell");
                return false;
            }

            if (required.TileId != tileId || required.DeclaredAs != declaredAs)
            {
                error = Text("tutorial.error.off_script_tile");
                return false;
            }

            error = null;
            return true;
        }

        /// <inheritdoc />
        public bool AllowsConfirmPlace(IReadOnlyList<TilePlacement> pending, out string error)
        {
            if (!InputEnabled)
            {
                error = Text("tutorial.error.not_your_step");
                return false;
            }

            if (!ScriptedPlacementRules.IsComplete(Expected, pending))
            {
                error = Text("tutorial.error.off_script_confirm");
                return false;
            }

            error = null;
            return true;
        }

        /// <inheritdoc />
        public bool AllowsPass(out string error)
        {
            error = Text("tutorial.error.pass_disabled");
            return false;
        }

        /// <inheritdoc />
        public bool AllowsExchange(out string error)
        {
            error = Text("tutorial.error.exchange_disabled");
            return false;
        }

        private string Text(string key) => _text != null ? _text.GetText(key) : key;
    }
}
