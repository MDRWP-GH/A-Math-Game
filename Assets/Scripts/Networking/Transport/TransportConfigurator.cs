using kcp2k;
using Mirror;
using UnityEngine;

namespace AMath.Networking.Transport
{
    /// <summary>
    /// Central KCP transport tuning for LAN play with 2-8 players.
    /// Kept in one place so gameplay code never touches transport internals
    /// and the transport can be swapped without touching other systems.
    /// </summary>
    public static class TransportConfigurator
    {
        /// <summary>Default game port (KCP/UDP).</summary>
        public const ushort DefaultPort = 7778;

        /// <summary>
        /// Milliseconds without traffic before a connection is considered dead.
        /// Deliberately short: fast host-loss detection drives host migration.
        /// </summary>
        public const int TimeoutMilliseconds = 10_000;

        /// <summary>Applies LAN-optimized settings to a KCP transport.</summary>
        public static void Configure(KcpTransport transport, ushort port = DefaultPort)
        {
            transport.Port = port;

            // LAN latency is ~1ms; favor responsiveness over throughput.
            transport.NoDelay = true;      // disable Nagle-style batching in kcp
            transport.Interval = 10;       // 10ms internal update tick
            transport.FastResend = 2;      // resend after 2 duplicate ACKs instead of waiting for RTO
            transport.Timeout = TimeoutMilliseconds;

            // Plain IPv4 avoids dual-mode socket quirks on mixed Windows LANs.
            transport.DualMode = false;

            Debug.Log($"[Transport] KCP configured for LAN on port {port}.");
        }
    }
}
