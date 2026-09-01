using AMath.Core;

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
        /// <summary>
        /// Name given to a room when the host does not type one, so pressing
        /// "play" straight away still produces a room others can recognise.
        /// </summary>
        public const string DefaultRoomName = "A-Math Server";

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

        /// <summary>
        /// Token the host issued on our last accepted connection, replayed when
        /// reconnecting to prove we are the same client. Memory-only and
        /// discarded together with the room.
        /// </summary>
        public string ReconnectToken { get; set; }

        /// <summary>Match format the host selected in the lobby before start.</summary>
        public MatchFormat SelectedFormat { get; set; } = MatchFormat.Individual;

        /// <summary>Resets to the "not in a room" state.</summary>
        public void Reset()
        {
            RoomName = null;
            RoomCode = null;
            MaxPlayers = 0;
            IsHost = false;
            Port = 0;
            IsActive = false;
            ReconnectToken = null;
            SelectedFormat = MatchFormat.Individual;
        }
    }
}
