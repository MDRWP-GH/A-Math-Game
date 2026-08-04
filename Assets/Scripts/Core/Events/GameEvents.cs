using AMath.Core.Commands;
using AMath.Core.StateMachines;

namespace AMath.Core.Events
{
    // ------------------------------------------------------------------
    // Match lifecycle events (published by core; consumed by net/UI/save)
    // ------------------------------------------------------------------

    /// <summary>The local game phase changed (see <see cref="GameStateMachine"/>).</summary>
    public struct MatchPhaseChangedEvent
    {
        public MatchPhase Previous;
        public MatchPhase Current;
    }

    /// <summary>A match started (or restarted after restore) on this peer.</summary>
    public struct MatchStartedEvent
    {
        public MatchConfig Config;
    }

    /// <summary>A new turn began.</summary>
    public struct TurnStartedEvent
    {
        public int TurnNumber;
        public int PlayerId;
        /// <summary>Seconds the player has for this turn.</summary>
        public int TurnSeconds;
    }

    /// <summary>
    /// The host validated and executed a turn.
    /// On the host this triggers broadcast + autosave + replay append;
    /// on clients it is published after the record is applied locally.
    /// </summary>
    public struct TurnResolvedEvent
    {
        public TurnRecord Record;
        /// <summary>True on the machine that has authority (the host).</summary>
        public bool IsAuthority;
    }

    /// <summary>The match ended; results are final.</summary>
    public struct MatchFinishedEvent
    {
        public MatchResult Result;
    }

    /// <summary>Local simulation no longer matches the host (checksum/score mismatch).</summary>
    public struct DesyncDetectedEvent
    {
        public string Reason;
    }

    // ------------------------------------------------------------------
    // Player intent (UI -> network layer)
    // ------------------------------------------------------------------

    /// <summary>The local player wants to perform an action; the network layer forwards it to the host.</summary>
    public struct LocalCommandRequestedEvent
    {
        public IGameCommand Command;
    }

    /// <summary>The host rejected the local player's last command.</summary>
    public struct CommandRejectedEvent
    {
        public string Reason;
    }

    // ------------------------------------------------------------------
    // Roster events
    // ------------------------------------------------------------------

    /// <summary>Roster composition changed (join/leave in lobby).</summary>
    public struct PlayerRosterChangedEvent { }

    /// <summary>A known player's connection state changed during a match.</summary>
    public struct PlayerConnectionChangedEvent
    {
        public int PlayerId;
        public bool IsConnected;
    }

    /// <summary>The local player's rack changed (draw/exchange). UI refreshes from PlayerManager.</summary>
    public struct LocalRackChangedEvent
    {
        public int PlayerId;
    }

    // ------------------------------------------------------------------
    // Persistence events
    // ------------------------------------------------------------------

    /// <summary>An autosave/manual save completed successfully.</summary>
    public struct SaveCompletedEvent
    {
        public string FilePath;
        public int TurnNumber;
    }
}
