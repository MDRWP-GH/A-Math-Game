using System;
using System.Collections.Generic;

namespace AMath.Core
{
    /// <summary>
    /// Immutable identity of a player inside one match.
    /// <see cref="PersistentGuid"/> survives disconnects so a rejoining player
    /// (or a migrated host) can be matched back to their seat and rack.
    /// </summary>
    [Serializable]
    public sealed class PlayerIdentity
    {
        /// <summary>Match-local id (0..N-1) assigned by the host. Also the turn order.</summary>
        public int PlayerId;

        /// <summary>Stable per-installation GUID used for reconnection identity.</summary>
        public string PersistentGuid;

        /// <summary>Display name shown in UI, replays and saves.</summary>
        public string DisplayName;
    }

    /// <summary>
    /// Everything needed to deterministically start (or re-start) a match.
    /// The host builds this once and broadcasts it; it is also the replay/save
    /// header, so a match can always be reconstructed from it.
    /// </summary>
    [Serializable]
    public sealed class MatchConfig
    {
        /// <summary>Seed for the shared <see cref="RandomNumbers.DeterministicRandom"/> stream.</summary>
        public int RandomSeed;

        /// <summary>Players in turn order (index == PlayerId).</summary>
        public List<PlayerIdentity> Players = new();

        /// <summary>Per-turn time limit in seconds; enforced by the host only.</summary>
        public int TurnSeconds = GameRules.DefaultTurnSeconds;

        /// <summary>Game version that produced this match (rules compatibility check).</summary>
        public string GameVersion;
    }
}
