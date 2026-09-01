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

        /// <summary>
        /// True when this seat is driven by the scripted medium AI on the host.
        /// Human peers never send commands for these seats.
        /// </summary>
        public bool IsAi;

        /// <summary>
        /// Team index when <see cref="MatchConfig.Format"/> is <see cref="MatchFormat.Team"/>.
        /// Unused (-1) in individual matches.
        /// </summary>
        public int TeamId = -1;

        /// <summary>
        /// Index into <see cref="PlayerColorPalette"/> for this player's chosen
        /// colour. Chosen in the lobby and frozen here at match start, so a
        /// replay shows the colours the players actually had.
        /// </summary>
        public byte ColorId;
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

        /// <summary>Individual free-for-all or two-team scoring.</summary>
        public MatchFormat Format = MatchFormat.Individual;
    }
}
