using System;
using System.Collections.Generic;
using AMath.Core.RandomNumbers;

namespace AMath.Gameplay.Board
{
    /// <summary>
    /// The shared tile bag. Exists on every peer but is only *authoritative* on
    /// the host; clients maintain an identical copy purely through the shared
    /// deterministic random stream and the ordered command log.
    /// Contents are never sent over the network — only the count is replicated
    /// for UI, which also means clients cannot trivially read upcoming draws
    /// from packets.
    /// </summary>
    public sealed class TileBag
    {
        #region Fields

        private readonly List<byte> _tiles = new(100);

        #endregion

        #region Properties

        /// <summary>Tiles remaining in the bag.</summary>
        public int Count => _tiles.Count;

        #endregion

        #region Setup / Restore

        /// <summary>Fills and deterministically shuffles the full 100-tile set.</summary>
        public void Reset(DeterministicRandom rng)
        {
            _tiles.Clear();
            _tiles.AddRange(AMathTileSet.CreateFullSet());
            rng.Shuffle(_tiles);
        }

        /// <summary>Restores exact bag contents from a snapshot (host migration / load).</summary>
        public void Restore(IReadOnlyList<byte> tiles)
        {
            _tiles.Clear();
            _tiles.AddRange(tiles);
        }

        /// <summary>Copies current contents into a snapshot buffer.</summary>
        public List<byte> ExportContents() => new(_tiles);

        #endregion

        #region Operations

        /// <summary>
        /// Removes the requested tiles from the bag in the given order.
        /// Returns false and leaves the bag unchanged when any tile is missing.
        /// Used by the tutorial to deal predetermined opening racks.
        /// </summary>
        public bool TryTakeSpecific(IReadOnlyList<byte> tiles, List<byte> destination)
        {
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            var remaining = new List<byte>(_tiles);
            var taken = new List<byte>(tiles.Count);
            for (int i = 0; i < tiles.Count; i++)
            {
                int index = remaining.IndexOf(tiles[i]);
                if (index < 0)
                    return false;

                remaining.RemoveAt(index);
                taken.Add(tiles[i]);
            }

            destination.Clear();
            destination.AddRange(taken);
            _tiles.Clear();
            _tiles.AddRange(remaining);
            return true;
        }

        /// <summary>Draws up to <paramref name="count"/> tiles (less when the bag runs dry).</summary>
        public void Draw(int count, List<byte> destination)
        {
            for (int i = 0; i < count && _tiles.Count > 0; i++)
            {
                // Pop from the end: cheap, and order is already random.
                byte tile = _tiles[^1];
                _tiles.RemoveAt(_tiles.Count - 1);
                destination.Add(tile);
            }
        }

        /// <summary>
        /// Exchange: draws replacements first, then returns the player's tiles
        /// and reshuffles. The fixed sequence matters — every peer must consume
        /// the random stream identically.
        ///
        /// Returns false instead of throwing when the bag is too small, so the
        /// command pipeline can reject the turn through its normal failure path
        /// rather than unwinding out of the middle of a host frame.
        /// </summary>
        public bool Exchange(IReadOnlyList<byte> returned, List<byte> drawn, DeterministicRandom rng)
        {
            if (returned == null || returned.Count > _tiles.Count)
                return false;

            Draw(returned.Count, drawn);
            _tiles.AddRange(returned);
            rng.Shuffle(_tiles);
            return true;
        }

        #endregion
    }
}
