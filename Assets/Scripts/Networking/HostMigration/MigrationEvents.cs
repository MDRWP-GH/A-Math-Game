namespace AMath.Networking.HostMigration
{
    /// <summary>Where the connection-recovery pipeline currently is.</summary>
    public enum RecoveryPhase
    {
        /// <summary>No recovery in progress.</summary>
        Idle = 0,

        /// <summary>Grace window: waiting to see if the original host comes back.</summary>
        GraceWait = 1,

        /// <summary>Scanning LAN discovery for the room code (original or migrated host).</summary>
        Searching = 2,

        /// <summary>This machine is promoting itself to host from its autosave.</summary>
        Promoting = 3,

        /// <summary>Connecting to a rediscovered host.</summary>
        Reconnecting = 4,

        /// <summary>Session restored; match can resume.</summary>
        Recovered = 5
    }

    /// <summary>Recovery progress for UI ("waiting for host...", elapsed time, options).</summary>
    public struct RecoveryStateChangedEvent
    {
        public RecoveryPhase Phase;
        public float ElapsedSeconds;
        public int ReconnectAttempts;
    }

    /// <summary>Host migration began (an elected candidate is taking over).</summary>
    public struct HostMigrationStartedEvent
    {
        public bool IAmNewHost;
        public int NewHostPlayerId;
    }

    /// <summary>Host migration (or reconnection) finished.</summary>
    public struct HostMigrationCompletedEvent
    {
        public bool Success;
    }
}
