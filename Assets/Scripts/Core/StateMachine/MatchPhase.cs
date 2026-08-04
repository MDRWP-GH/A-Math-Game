namespace AMath.Core.StateMachines
{
    /// <summary>
    /// High-level phase of a networked match.
    /// The value is host-authoritative and replicated to clients; every peer
    /// drives its local <see cref="GameStateMachine"/> from the replicated value.
    /// </summary>
    public enum MatchPhase : byte
    {
        /// <summary>Players gathering in the room, before the match starts.</summary>
        Lobby = 0,

        /// <summary>Match data is being distributed / restored (also used during resync).</summary>
        Loading = 1,

        /// <summary>Match in progress, turns being played.</summary>
        Playing = 2,

        /// <summary>Match halted (connection loss, host migration, manual pause).</summary>
        Paused = 3,

        /// <summary>Match over; results available.</summary>
        Finished = 4
    }
}
