using System;
using System.Collections.Generic;
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
        private readonly float _targetRefreshSeconds;

        private UdpClient _udp;
        private IReadOnlyList<IPEndPoint> _targets = Array.Empty<IPEndPoint>();
        private float _nextSendTime;
        private float _nextTargetRefreshTime;
        private byte[] _payload = Array.Empty<byte>();

        #endregion

        #region Properties

        /// <summary>True while advertising.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Consecutive failed UDP sends. Resets to 0 on success. Used by the
        /// host to detect network loss and dissolve the room.
        /// </summary>
        public int ConsecutiveSendFailures { get; private set; }

        #endregion

        #region Construction

        public LanBroadcastService(int discoveryPort, float intervalSeconds = 1f, float targetRefreshSeconds = 30f)
        {
            _discoveryPort = discoveryPort;
            _interval = intervalSeconds;
            _targetRefreshSeconds = targetRefreshSeconds;
        }

        #endregion

        #region Control

        /// <summary>Starts broadcasting the given advertisement.</summary>
        public bool Start(RoomAdvertisement advertisement)
        {
            Stop();

            try
            {
                _udp = new UdpClient { EnableBroadcast = true };
                RefreshTargets(0f);
                UpdateAdvertisement(advertisement);
                _nextSendTime = 0f;
                ConsecutiveSendFailures = 0;
                IsRunning = true;
                return true;
            }
            catch (SocketException ex)
            {
                Debug.LogError($"[Discovery] Cannot start broadcasting: {ex.Message}");
                Stop();
                return false;
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
            ConsecutiveSendFailures = 0;
            _targets = Array.Empty<IPEndPoint>();
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

            if (unscaledTime >= _nextTargetRefreshTime)
                RefreshTargets(unscaledTime);

            _nextSendTime = unscaledTime + _interval;

            bool anySent = false;
            foreach (IPEndPoint target in _targets)
            {
                try
                {
                    _udp.Send(_payload, _payload.Length, target);
                    anySent = true;
                }
                catch (SocketException ex)
                {
                    Debug.LogWarning($"[Discovery] Broadcast to {target} failed: {ex.Message}");
                }
            }

            if (anySent)
                ConsecutiveSendFailures = 0;
            else
            {
                ConsecutiveSendFailures++;
                Debug.LogWarning(
                    $"[Discovery] Broadcast failed ({ConsecutiveSendFailures}): no targets delivered.");
            }
        }

        private void RefreshTargets(float unscaledTime)
        {
            _nextTargetRefreshTime = unscaledTime + _targetRefreshSeconds;
            _targets = LanBroadcastTargets.GetEndpoints(_discoveryPort);
        }

        #endregion
    }
}
