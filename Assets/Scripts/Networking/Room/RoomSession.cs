namespace AMath.Networking.Room
{
    /// <summary>
    /// Shared, mutable description of the room this machine currently belongs
    /// to. One instance is created by the composition root and read by the
    /// authenticator, discovery broadcaster and UI — so "what room am I in?"
    /// has exactly one source of truth.
    /// </summary>
    public sealed class RoomSession
    {
        /// <summary>Display name of the room.</summary>
        public string RoomName { get; set; }

        /// <summary>6-character human-friendly room code (e.g. "AB29KF").</summary>
        public string RoomCode { get; set; }

        /// <summary>Maximum seats in the room (2..8).</summary>
        public int MaxPlayers { get; set; }

        /// <summary>True while this machine is the host.</summary>
        public bool IsHost { get; set; }

        /// <summary>Game port the host listens on.</summary>
        public ushort Port { get; set; }

        /// <summary>True while any room membership is active (host or client).</summary>
        public bool IsActive { get; set; }

        /// <summary>Resets to the "not in a room" state.</summary>
        public void Reset()
        {
            RoomName = null;
            RoomCode = null;
            MaxPlayers = 0;
            IsHost = false;
            Port = 0;
            IsActive = false;
        }
    }
}
