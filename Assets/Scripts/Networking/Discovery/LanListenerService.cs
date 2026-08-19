using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace AMath.Networking.Discovery
{
    /// <summary>
    /// Client-side discovery receiver. A background thread blocks on the UDP
    /// socket and pushes raw packets into a lock-free queue; the main thread
    /// drains and parses them during Tick, because Unity APIs (and the event
    /// bus subscribers) are not thread-safe.
    /// </summary>
    public sealed class LanListenerService : IDisposable
    {
        #region Types

        private readonly struct RawPacket
        {
            public readonly string SenderAddress;
            public readonly string Json;

            public RawPacket(string senderAddress, string json)
            {
                SenderAddress = senderAddress;
                Json = json;
            }
        }

        #endregion

        #region Fields

        /// <summary>
        /// Cap on how long Stop() waits for the reader. Closing the socket
        /// unblocks it immediately in practice; the bound only exists so a
        /// wedged socket cannot freeze the main thread.
        /// </summary>
        private const int ThreadJoinTimeoutMs = 500;

        private readonly int _discoveryPort;
        private readonly ConcurrentQueue<RawPacket> _pending = new();

        private UdpClient _udp;
        private Thread _receiveThread;
        private volatile bool _running;

        #endregion

        #region Properties

        /// <summary>True while listening.</summary>
        public bool IsRunning => _running;

        #endregion

        #region Construction

        public LanListenerService(int discoveryPort)
        {
            _discoveryPort = discoveryPort;
        }

        #endregion

        #region Control

        /// <summary>Starts listening for room broadcasts.</summary>
        public void Start()
        {
            Stop();

            try
            {
                _udp = new UdpClient();
                // Multiple clients (or client + host) may share one machine.
                _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udp.Client.Bind(new IPEndPoint(IPAddress.Any, _discoveryPort));

                _running = true;
                _receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "AMath.LanDiscovery"
                };
                _receiveThread.Start();
            }
            catch (SocketException ex)
            {
                Debug.LogError($"[Discovery] Cannot start listening: {ex.Message}");
                Stop();
            }
        }

        /// <summary>Stops listening and joins the receive thread.</summary>
        public void Stop()
        {
            _running = false;
            _udp?.Close(); // unblocks the Receive call
            _udp = null;

            // Wait for the thread to actually exit before returning: Start()
            // calls Stop() first, and rebinding the port while the previous
            // reader is still alive fails intermittently.
            Thread thread = _receiveThread;
            _receiveThread = null;
            if (thread != null && thread.IsAlive)
                thread.Join(ThreadJoinTimeoutMs);

            while (_pending.TryDequeue(out _)) { }
        }

        /// <inheritdoc />
        public void Dispose() => Stop();

        #endregion

        #region Receive thread

        private void ReceiveLoop()
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            UdpClient udp = _udp;

            while (_running)
            {
                try
                {
                    byte[] data = udp.Receive(ref remote);
                    // Cheap sanity bound before any parsing.
                    if (data.Length == 0 || data.Length > 1024) continue;

                    _pending.Enqueue(new RawPacket(
                        remote.Address.ToString(),
                        Encoding.UTF8.GetString(data)));
                }
                catch (SocketException)
                {
                    // Socket closed by Stop(); exit quietly.
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        #endregion

        #region Main-thread drain

        /// <summary>
        /// Drains received packets on the main thread. Invokes
        /// <paramref name="onAdvertisement"/> for every valid advertisement.
        /// </summary>
        public void Tick(Action<string, RoomAdvertisement> onAdvertisement)
        {
            while (_pending.TryDequeue(out RawPacket packet))
            {
                RoomAdvertisement advertisement;
                try
                {
                    advertisement = JsonUtility.FromJson<RoomAdvertisement>(packet.Json);
                }
                catch (Exception)
                {
                    continue; // foreign or corrupt traffic
                }

                if (advertisement == null || advertisement.Protocol != RoomAdvertisement.ProtocolId)
                    continue;

                onAdvertisement(packet.SenderAddress, advertisement);
            }
        }

        #endregion
    }
}
