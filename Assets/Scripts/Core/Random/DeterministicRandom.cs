using System;

namespace AMath.Core.RandomNumbers
{
    /// <summary>
    /// Deterministic pseudo-random generator (xorshift64*) with fully
    /// serializable state.
    ///
    /// This is the backbone of the whole architecture: the host publishes one
    /// seed at match start, every tile draw and shuffle consumes this stream in
    /// command order, therefore:
    ///   - every client can reconstruct hidden state (racks, bag) locally,
    ///   - a replay is just "seed + ordered command list",
    ///   - after host migration the new host resumes from a snapshot that
    ///     contains the exact RNG state.
    /// Never use UnityEngine.Random or System.Random for game logic.
    /// </summary>
    [Serializable]
    public sealed class DeterministicRandom
    {
        #region State

        // Serialized as long because JsonUtility cannot handle ulong.
        private long _state;

        /// <summary>Raw generator state; persisted in snapshots and saves.</summary>
        public long State
        {
            get => _state;
            set => _state = value == 0 ? 0x9E3779B97F4A7C15L : value; // state must never be zero
        }

        #endregion

        #region Construction

        /// <summary>Creates a generator from a 32-bit match seed.</summary>
        public DeterministicRandom(int seed)
        {
            // SplitMix64-style seed expansion so nearby seeds diverge immediately.
            ulong z = (ulong)(uint)seed + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            _state = unchecked((long)(z ^ (z >> 31)));
            if (_state == 0) _state = unchecked((long)0x9E3779B97F4A7C15UL);
        }

        /// <summary>Restores a generator from previously captured raw state.</summary>
        public static DeterministicRandom FromState(long state) => new(0) { State = state };

        #endregion

        #region Generation

        /// <summary>Returns the next raw 64-bit value.</summary>
        public ulong NextULong()
        {
            ulong x = unchecked((ulong)_state);
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = unchecked((long)x);
            return x * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>Returns a value in [0, maxExclusive).</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextULong() % (ulong)maxExclusive);
        }

        /// <summary>Returns a value in [minInclusive, maxExclusive).</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return minInclusive + NextInt(maxExclusive - minInclusive);
        }

        /// <summary>In-place Fisher–Yates shuffle. Consumes N-1 random values.</summary>
        public void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        #endregion
    }
}
