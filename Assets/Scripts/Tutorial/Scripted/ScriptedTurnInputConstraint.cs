using System.Collections.Generic;
using AMath.Core.Assistance;
using AMath.Gameplay.Board;
using AMath.Gameplay.Interaction;

namespace AMath.Tutorial.Scripted
{
    /// <summary>
    /// Lets the player pick and place their own tiles, but only onto the
    /// cells (and with the tiles) named by the current scripted turn.
    /// Pass and exchange are disabled unless the active tutorial step enables them.
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

        /// <summary>When false, the rack can be used but board placement is blocked.</summary>
        public bool PlacementEnabled { get; set; } = true;

        /// <summary>When true, the player may pass the turn.</summary>
        public bool PassEnabled { get; set; }

        /// <summary>When true, the player may exchange rack tiles.</summary>
        public bool ExchangeEnabled { get; set; }

        /// <summary>When set, exchange must use exactly these rack indices.</summary>
        public IReadOnlyList<int> ExpectedExchangeIndices { get; set; } = System.Array.Empty<int>();

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

            if (!PlacementEnabled)
            {
                error = Text("tutorial.error.select_tile_first");
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
            if (!PassEnabled)
            {
                error = Text("tutorial.error.pass_disabled");
                return false;
            }

            error = null;
            return true;
        }

        /// <inheritdoc />
        public bool AllowsExchange(IReadOnlyList<int> rackIndices, out string error)
        {
            if (!ExchangeEnabled)
            {
                error = Text("tutorial.error.exchange_disabled");
                return false;
            }

            if (ExpectedExchangeIndices != null && ExpectedExchangeIndices.Count > 0)
            {
                if (rackIndices == null || rackIndices.Count != ExpectedExchangeIndices.Count)
                {
                    error = Text("tutorial.error.exchange_selection");
                    return false;
                }

                var remaining = new List<int>(ExpectedExchangeIndices);
                for (int i = 0; i < rackIndices.Count; i++)
                {
                    if (!remaining.Remove(rackIndices[i]))
                    {
                        error = Text("tutorial.error.exchange_selection");
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        private string Text(string key) => _text != null ? _text.GetText(key) : key;
    }
}
