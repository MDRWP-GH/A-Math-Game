using System;
using System.Collections.Generic;

namespace AMath.Core
{
    /// <summary>Why a match ended; stored in results, saves and replays.</summary>
    public enum MatchEndReason : byte
    {
        None = 0,
        /// <summary>A player emptied their rack with an empty bag.</summary>
        PlayerFinishedTiles = 1,
        /// <summary>Every player passed for the configured number of rounds.</summary>
        AllPlayersPassed = 2,
        /// <summary>The host (or remaining players after a failed migration) ended the match.</summary>
        EndedManually = 3
    }

    /// <summary>
    /// The authoritative record of one resolved turn.
    /// Produced only by the host after validation. The exact same record is:
    ///   - broadcast to clients (delta synchronization),
    ///   - appended to the replay log,
    ///   - re-executed during replay/host-migration reconstruction.
    /// Note: it intentionally contains no rack/bag contents — those are derived
    /// deterministically from the seed, keeping network payloads tiny.
    /// </summary>
    [Serializable]
    public sealed class TurnRecord
    {
        /// <summary>1-based turn number.</summary>
        public int TurnNumber;

        /// <summary>Player who acted. Injected by the host from the connection, never from the client payload.</summary>
        public int PlayerId;

        /// <summary>Serialized command type (see <see cref="Commands.CommandType"/>).</summary>
        public byte CommandType;

        /// <summary>Serialized command payload (see <see cref="Commands.CommandSerializer"/>).</summary>
        public byte[] CommandPayload;

        /// <summary>Points gained this turn (0 for pass/exchange).</summary>
        public int ScoreDelta;

        /// <summary>UTC ticks when the host resolved the turn.</summary>
        public long TimestampUtcTicks;

        /// <summary>True when this turn ended the match.</summary>
        public bool EndedMatch;

        /// <summary>End reason when <see cref="EndedMatch"/> is true.</summary>
        public MatchEndReason EndReason;
    }

    /// <summary>Final standing of one player.</summary>
    [Serializable]
    public sealed class PlayerResult
    {
        public int PlayerId;
        public string DisplayName;
        public int FinalScore;

        /// <summary>Team index in team matches; -1 in individual matches.</summary>
        public int TeamId = -1;
    }

    /// <summary>Combined score for one team in a team match.</summary>
    [Serializable]
    public sealed class TeamResult
    {
        public int TeamId;
        public int TotalScore;
    }

    /// <summary>Final outcome of a match.</summary>
    [Serializable]
    public sealed class MatchResult
    {
        public MatchEndReason Reason;
        public int WinnerPlayerId = -1;

        /// <summary>Winning team when <see cref="Format"/> is team; otherwise -1.</summary>
        public int WinnerTeamId = -1;

        /// <summary>True when two or more players/teams share the best score.</summary>
        public bool IsDraw;

        public MatchFormat Format = MatchFormat.Individual;

        /// <summary>UTC ticks when the match started (host clock).</summary>
        public long StartedUtcTicks;

        /// <summary>UTC ticks when the match ended.</summary>
        public long EndedUtcTicks;

        /// <summary>Elapsed match time in whole seconds.</summary>
        public int DurationSeconds;

        public List<PlayerResult> Standings = new();
        public List<TeamResult> TeamStandings = new();
    }
}
