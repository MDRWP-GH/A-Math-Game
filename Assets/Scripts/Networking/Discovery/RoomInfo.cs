using System;

namespace AMath.Networking.Discovery
{
    /// <summary>
    /// JSON payload broadcast by a host once per second.
    /// Kept flat and small (&lt; 300 bytes) — JsonUtility-compatible.
    /// </summary>
    [Serializable]
    public sealed class RoomAdvertisement
    {
        /// <summary>Protocol marker so foreign UDP traffic is ignored cheaply.</summary>
        public string Protocol = ProtocolId;

        public const string ProtocolId = "AMATH1";

        public string RoomName;
        public string RoomCode;
        public int CurrentPlayers;
        public int MaxPlayers;
        public string GameVersion;
        /// <summary>Game (KCP) port; the discovery port is separate.</summary>
        public int Port;
        /// <summary>True when a match is running (reconnect target rather than open lobby).</summary>
        public bool MatchInProgress;
    }

    /// <summary>
    /// A discovered room as tracked by the client-side registry.
    /// The address comes from the UDP packet's *actual* sender, never from the
    /// payload, so a spoofed advertisement cannot redirect players elsewhere.
    /// </summary>
    public sealed class RoomInfo
    {
        /// <summary>Host IPv4 address (socket-derived).</summary>
        public string HostAddress;

        /// <summary>Latest advertisement received from this host.</summary>
        public RoomAdvertisement Advertisement;

        /// <summary>Realtime timestamp of the last received broadcast (expiry tracking).</summary>
        public float LastSeenTime;

        /// <summary>True when this room can accept a fresh (non-reconnect) join.</summary>
        public bool IsJoinable =>
            !Advertisement.MatchInProgress
            && Advertisement.CurrentPlayers < Advertisement.MaxPlayers;
    }
}
