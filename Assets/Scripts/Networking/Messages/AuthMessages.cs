using Mirror;

namespace AMath.Networking.Messages
{
    /// <summary>
    /// First message a connecting client sends, before any spawning happens.
    /// The host uses it to accept or reject the connection at the door.
    /// </summary>
    public struct AuthRequestMessage : NetworkMessage
    {
        /// <summary>Client's game version; must match the host exactly.</summary>
        public string GameVersion;

        /// <summary>Room code the client believes it is joining (second factor next to the IP).</summary>
        public string RoomCode;

        /// <summary>Stable per-installation GUID (reconnection identity).</summary>
        public string PersistentGuid;

        /// <summary>Player display name.</summary>
        public string DisplayName;

        /// <summary>
        /// Token this client received from the host on its previous accepted
        /// connection. Empty on a first join. The host requires it to match
        /// before handing a mid-match seat back, so knowing a victim's GUID is
        /// no longer enough to take their place while they are disconnected.
        /// </summary>
        public string ReconnectToken;
    }

    /// <summary>Host's verdict on an <see cref="AuthRequestMessage"/>.</summary>
    public struct AuthResponseMessage : NetworkMessage
    {
        public bool Approved;
        public string Reason;

        /// <summary>Freshly issued token to present when reconnecting to this host.</summary>
        public string ReconnectToken;
    }

    /// <summary>
    /// Identity attached to an authenticated connection
    /// (stored in <c>NetworkConnection.authenticationData</c>).
    /// </summary>
    public sealed class AuthenticatedIdentity
    {
        public string PersistentGuid;
        public string DisplayName;

        /// <summary>True when this connection resumes an existing seat in a running match.</summary>
        public bool IsReconnection;

        /// <summary>Seat claimed by a reconnection; -1 for fresh lobby joins.</summary>
        public int ExistingPlayerId = -1;
    }
}
