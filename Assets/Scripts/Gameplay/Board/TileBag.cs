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
        /// </summary>
        public void Exchange(IReadOnlyList<byte> returned, List<byte> drawn, DeterministicRandom rng)
        {
            if (returned.Count > _tiles.Count)
                throw new InvalidOperationException("Not enough tiles in the bag to exchange.");

            Draw(returned.Count, drawn);
            _tiles.AddRange(returned);
            rng.Shuffle(_tiles);
        }

        #endregion
    }
}
