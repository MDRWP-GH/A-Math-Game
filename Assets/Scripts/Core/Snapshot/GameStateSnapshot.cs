using System;
using System.Collections.Generic;
using AMath.Gameplay.Board;
using AMath.Core.StateMachines;

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
        /// <summary>Team index in team matches; otherwise -1.</summary>
        public int TeamId = -1;
        /// <summary>Index into <see cref="PlayerColorPalette"/> for this player's colour.</summary>
        public byte ColorId;
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

        /// <summary>
        /// Elapsed seconds captured from the authority's monotonic clock. Older
        /// saves omit this field and restore through the UTC compatibility path.
        /// </summary>
        public double MatchElapsedSeconds;

        /// <summary>Rejects incomplete snapshots before a restore mutates live match state.</summary>
        public bool TryValidate(out string error)
        {
            error = null;
            if (Config?.Players == null || Players == null ||
                Config.Players.Count < 1 || Config.Players.Count > GameRules.MaxPlayers ||
                Players.Count != Config.Players.Count || BoardCells == null || BagTiles == null)
            {
                error = "Snapshot is missing match, roster, board, or bag data.";
                return false;
            }

            if (TurnNumber < 1 || CurrentPlayerId < 0 || CurrentPlayerId >= Players.Count)
            {
                error = "Snapshot has an invalid turn or current player.";
                return false;
            }

            if (!System.Enum.IsDefined(typeof(MatchPhase), Phase) || Config.TurnSeconds <= 0)
            {
                error = "Snapshot has an invalid match phase or turn time.";
                return false;
            }

            for (int i = 0; i < Players.Count; i++)
            {
                if (Config.Players[i] == null || Config.Players[i].PlayerId != i ||
                    Players[i] == null || Players[i].PlayerId != i || Players[i].Rack == null)
                {
                    error = $"Snapshot has an incomplete player at seat {i}.";
                    return false;
                }

                if (Players[i].Rack.Count > GameRules.RackSize)
                {
                    error = $"Snapshot has an oversized rack at seat {i}.";
                    return false;
                }
                foreach (byte tileId in Players[i].Rack)
                {
                    if (AMathTileSet.IsValidTileId(tileId)) continue;
                    error = $"Snapshot has an unknown rack tile at seat {i}.";
                    return false;
                }
            }

            foreach (byte tileId in BagTiles)
            {
                if (AMathTileSet.IsValidTileId(tileId)) continue;
                error = "Snapshot has an unknown bag tile.";
                return false;
            }

            var occupied = new HashSet<int>();
            foreach (TilePlacement cell in BoardCells)
            {
                if (!BoardGrid.InBounds(cell.X, cell.Y) ||
                    !occupied.Add(cell.Y * GameRules.BoardSize + cell.X) ||
                    !AMathTileSet.IsValidTileId(cell.TileId) ||
                    !AMathTileSet.IsLegalDeclaration(cell.TileId, cell.DeclaredAs))
                {
                    error = "Snapshot has an invalid or duplicate board cell.";
                    return false;
                }
            }

            return true;
        }
    }
}
