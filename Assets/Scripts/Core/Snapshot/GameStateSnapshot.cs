using System;
using System.Collections.Generic;
using AMath.Gameplay.Board;

namespace AMath.Core.Snapshot
{
    /// <summary>Persisted state of one player inside a snapshot.</summary>
    [Serializable]
    public sealed class PlayerSnapshot
    {
        public int PlayerId;
        public string PersistentGuid;
        public string DisplayName;
        public int Score;
        public bool IsAi;
        public List<byte> Rack = new();
    }

    /// <summary>
    /// A complete, serializable picture of a match at one instant.
    ///
    /// One format, three consumers:
    ///  - <c>SaveManager</c> writes it to disk as JSON after every turn;
    ///  - host migration restores it on the newly elected host;
    ///  - late joiners / reconnecting clients receive it as the resync payload.
    /// It is sparse (only occupied cells, only remaining bag tiles), so a full
    /// snapshot stays around 1-2 KB — cheap to save and to send.
    /// </summary>
    [Serializable]
    public sealed class GameStateSnapshot
    {
        /// <summary>The match header (seed, roster, rules).</summary>
        public MatchConfig Config;

        /// <summary>Phase at capture time (byte form of <see cref="StateMachines.MatchPhase"/>).</summary>
        public byte Phase;

        /// <summary>Raw deterministic RNG state at capture time.</summary>
        public long RandomState;

        /// <summary>1-based turn number about to be played.</summary>
        public int TurnNumber;

        /// <summary>Player whose turn it is.</summary>
        public int CurrentPlayerId;

        /// <summary>Consecutive pass/exchange turns (match-end tracking).</summary>
        public int ConsecutivePasses;

        /// <summary>Occupied board cells only.</summary>
        public List<TilePlacement> BoardCells = new();

        /// <summary>Exact remaining bag contents, in draw order.</summary>
        public List<byte> BagTiles = new();

        /// <summary>All players including scores and racks.</summary>
        public List<PlayerSnapshot> Players = new();

        /// <summary>Final result when the snapshot was taken after match end; null otherwise.</summary>
        public MatchResult Result;

        /// <summary>UTC ticks when the match began (for duration tracking across restore).</summary>
        public long MatchStartedUtcTicks;
    }
}
