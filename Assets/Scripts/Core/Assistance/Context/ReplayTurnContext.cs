using AMath.Core.Commands;

namespace AMath.Core.Assistance.Context
{
    /// <summary>
    /// Safe, compact replay fact exposed to Replay Coach. It contains only
    /// public turn metadata and score impact—not command payloads, historical
    /// rack contents, random state or other hidden information.
    /// </summary>
    public sealed class ReplayTurnContext
    {
        /// <summary>1-based turn number.</summary>
        public int TurnNumber;

        /// <summary>Player who took the turn.</summary>
        public int PlayerId;

        /// <summary>Public command category.</summary>
        public CommandType CommandType;

        /// <summary>Official score gained by the turn.</summary>
        public int ScoreDelta;
    }
}
