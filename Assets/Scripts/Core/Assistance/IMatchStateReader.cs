using AMath.Core.StateMachines;

namespace AMath.Core.Assistance
{
    /// <summary>
    /// Read-only view over match flow: phase, turn order and the most recent
    /// command rejection (used to answer "why can't I place this tile?").
    /// </summary>
    public interface IMatchStateReader
    {
        /// <summary>Current match phase.</summary>
        MatchPhase Phase { get; }

        /// <summary>1-based number of the turn currently being played.</summary>
        int TurnNumber { get; }

        /// <summary>PlayerId whose turn it is.</summary>
        int CurrentPlayerId { get; }

        /// <summary>Tiles remaining in the shared bag.</summary>
        int TilesRemainingInBag { get; }

        /// <summary>
        /// Reason the local player's last submitted command was rejected, or
        /// null when the last command succeeded (or none was submitted yet).
        /// Cleared once a new command is accepted.
        /// </summary>
        string LastCommandRejectionReason { get; }
    }
}
