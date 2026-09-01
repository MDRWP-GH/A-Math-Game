using System.Collections.Generic;
using AMath.Core.Snapshot;

namespace AMath.Gameplay.Board
{
    /// <summary>
    /// Facade over the board domain: grid state, placement validation and
    /// scoring. Keeps <see cref="BoardGrid"/>, <see cref="PlacementValidator"/>
    /// and <see cref="ScoreCalculator"/> as small single-purpose collaborators
    /// instead of one god class.
    /// </summary>
    public sealed class BoardManager
    {
        #region Fields

        private readonly PlacementValidator _validator = new();
        private readonly ScoreCalculator _scoreCalculator = new();

        #endregion

        #region Properties

        /// <summary>The underlying grid (read access for UI/rendering).</summary>
        public BoardGrid Grid { get; } = new();

        #endregion

        #region Gameplay

        /// <summary>Validates a placement request against the current board and a rack.</summary>
        public PlacementValidation Validate(IReadOnlyList<byte> rack, IReadOnlyList<TilePlacement> placements) =>
            _validator.Validate(Grid, rack, placements);

        /// <summary>
        /// Commits a *previously validated* placement and returns the score.
        /// Must only be called with the validation produced for this exact
        /// request; anything else is rejected rather than written, because a
        /// board that takes an unvalidated write corrupts the tile count and
        /// the occupancy invariant every peer's state is compared against.
        /// </summary>
        public int Commit(PlacementValidation validation, IReadOnlyList<TilePlacement> placements)
        {
            if (validation == null || !validation.IsValid || placements == null)
                throw new System.InvalidOperationException("Commit requires a successful validation.");

            for (int i = 0; i < placements.Count; i++)
            {
                if (Grid.IsOccupied(placements[i].X, placements[i].Y))
                    throw new System.InvalidOperationException("Commit would overwrite an occupied cell.");
            }

            int score = _scoreCalculator.Calculate(validation.Lines, placements.Count);
            for (int i = 0; i < placements.Count; i++)
            {
                TilePlacement placement = placements[i];
                Grid.Place(in placement);
            }

            return score;
        }

        /// <summary>Clears the board for a new match.</summary>
        public void Reset() => Grid.Clear();

        #endregion

        #region Snapshot support

        /// <summary>Writes occupied cells into a snapshot.</summary>
        public void ExportTo(GameStateSnapshot snapshot)
        {
            snapshot.BoardCells.Clear();
            Grid.ExportOccupied(snapshot.BoardCells);
        }

        /// <summary>Restores the board from a snapshot.</summary>
        public void RestoreFrom(GameStateSnapshot snapshot) => Grid.Restore(snapshot.BoardCells);

        #endregion
    }
}
