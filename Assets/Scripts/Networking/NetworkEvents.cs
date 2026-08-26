using System.Collections.Generic;
using AMath.Networking.Discovery;

namespace AMath.Networking
{
    // Events owned by the networking layer. Core never references these;
    // UI and networking systems communicate through them via the event bus.

    /// <summary>This machine started hosting (server + local client active).</summary>
    public struct HostStartedEvent { }

    /// <summary>This machine stopped hosting.</summary>
    public struct HostStoppedEvent { }

    /// <summary>The local client connected to a host.</summary>
    public struct ClientConnectedEvent { }

    /// <summary>
    /// The host refused this client at the door (version, room code, capacity,
    /// identity clash, match already running). The reason is host-authored
    /// English text meant to be shown verbatim — it explains failures the UI
    /// cannot deduce on its own.
    /// </summary>
    public struct ConnectionRejectedEvent
    {
        public string Reason;
    }

    /// <summary>
    /// The local client lost its connection to the host. Triggers the
    /// reconnect grace window (then leave + fresh join on failure).
    /// </summary>
    public struct ClientDisconnectedEvent
    {
        /// <summary>True when a match was in progress at the moment of disconnect.</summary>
        public bool MatchWasRunning;
    }

    /// <summary>Why a room was dissolved for this machine.</summary>
    public enum RoomDissolveReason
    {
        HostNetworkLost = 0,
        ReconnectFailed = 1,
        LobbyDisconnect = 2
    }

    /// <summary>
    /// The local room session was torn down (host network loss, or client
    /// gave up after reconnect / fresh-join failed).
    /// </summary>
    public struct RoomDissolvedEvent
    {
        public RoomDissolveReason Reason;
    }

    /// <summary>The discovered-room list changed (rooms appeared, expired or updated).</summary>
    public struct RoomListUpdatedEvent
    {
        public IReadOnlyList<RoomInfo> Rooms;
    }

    /// <summary>A client reported its connection quality to the host (host-side event).</summary>
    public struct QualityReportReceivedEvent
    {
        public int PlayerId;
        /// <summary>Round-trip time in seconds as measured by the client.</summary>
        public double RttSeconds;
    }
}
