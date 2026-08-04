using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace AMath.Networking.Discovery
{
    /// <summary>
    /// Host-side discovery emitter: serializes the current
    /// <see cref="RoomAdvertisement"/> and broadcasts it via UDP once per
    /// interval. Runs entirely on the main thread (a 1 Hz sendto is
    /// negligible) and reuses one buffer to stay allocation-free per tick.
    /// </summary>
    public sealed class LanBroadcastService : IDisposable
    {
        #region Fields

        private readonly int _discoveryPort;
        private readonly float _interval;

        private UdpClient _udp;
        private IPEndPoint _broadcastEndPoint;
        private float _nextSendTime;
        private byte[] _payload = Array.Empty<byte>();

        #endregion

        #region Properties

        /// <summary>True while advertising.</summary>
        public bool IsRunning { get; private set; }

        #endregion

        #region Construction

        public LanBroadcastService(int discoveryPort, float intervalSeconds = 1f)
        {
            _discoveryPort = discoveryPort;
            _interval = intervalSeconds;
        }

        #endregion

        #region Control

        /// <summary>Starts broadcasting the given advertisement.</summary>
        public void Start(RoomAdvertisement advertisement)
        {
            Stop();

            try
            {
                _udp = new UdpClient { EnableBroadcast = true };
                _broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, _discoveryPort);
                UpdateAdvertisement(advertisement);
                _nextSendTime = 0f;
                IsRunning = true;
            }
            catch (SocketException ex)
            {
                Debug.LogError($"[Discovery] Cannot start broadcasting: {ex.Message}");
                Stop();
            }
        }

        /// <summary>Re-serializes the payload (player count changed, match started, ...).</summary>
        public void UpdateAdvertisement(RoomAdvertisement advertisement)
        {
            _payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(advertisement));
        }

        /// <summary>Stops broadcasting and releases the socket.</summary>
        public void Stop()
        {
            IsRunning = false;
            _udp?.Close();
            _udp = null;
        }

        /// <inheritdoc />
        public void Dispose() => Stop();

        #endregion

        #region Tick

        /// <summary>Called from the composition root's update loop.</summary>
        public void Tick(float unscaledTime)
        {
            if (!IsRunning || unscaledTime < _nextSendTime) return;
            _nextSendTime = unscaledTime + _interval;

            try
            {
                _udp.Send(_payload, _payload.Length, _broadcastEndPoint);
            }
            catch (SocketException ex)
            {
                // Non-fatal (e.g. cable unplugged); keep trying each interval.
                Debug.LogWarning($"[Discovery] Broadcast failed: {ex.Message}");
            }
        }

        #endregion
    }
}
