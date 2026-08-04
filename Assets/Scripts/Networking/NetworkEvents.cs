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
    /// The local client lost its connection to the host. During a match this
    /// triggers the pause + backup + host-migration flow.
    /// </summary>
    public struct ClientDisconnectedEvent
    {
        /// <summary>True when a match was in progress at the moment of disconnect.</summary>
        public bool MatchWasRunning;
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
