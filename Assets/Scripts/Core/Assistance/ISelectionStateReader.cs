namespace AMath.Core.Assistance
{
    /// <summary>
    /// Read-only view over the local player's current UI selection (the tile
    /// they have picked up but not yet placed). Owned and written by the
    /// input/UI layer; Tutorial and AI only ever read it.
    /// </summary>
    public interface ISelectionStateReader
    {
        /// <summary>The tile id currently selected/held by the local player, or null when nothing is selected.</summary>
        byte? SelectedTileId { get; }
    }
}
