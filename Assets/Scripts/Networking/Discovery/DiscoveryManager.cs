using System;
using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Events;
using UnityEngine;

namespace AMath.Networking.Discovery
{
    /// <summary>
    /// Facade over LAN discovery. Owns the broadcaster (host role), the
    /// listener (client role) and the registry of currently visible rooms.
    ///
    /// The registry is keyed by host IP, entries expire after
    /// <see cref="RoomStaleSeconds"/> without a broadcast, and every change is
    /// published as <see cref="RoomListUpdatedEvent"/>. It also resolves
    /// room codes to host IPs — codes are pure user convenience; the
    /// connection always targets the discovered address.
    /// </summary>
    public sealed class DiscoveryManager : ITickable, IDisposable
    {
        #region Constants

        /// <summary>UDP port used for discovery (distinct from the game port).</summary>
        public const int DiscoveryPort = 47777;

        /// <summary>Seconds without a broadcast before a room disappears from the list.</summary>
        public const float RoomStaleSeconds = 3f;

        #endregion

        #region Fields

        private readonly IEventBus _eventBus;
        private readonly LanBroadcastService _broadcaster;
        private readonly LanListenerService _listener;

        private readonly Dictionary<string, RoomInfo> _roomsByAddress = new();
        private readonly List<RoomInfo> _roomList = new();
        private readonly Action<string, RoomAdvertisement> _onAdvertisement; // cached: no per-frame closure
        private float _nextExpiryCheck;
        private float _now;
        private bool _registryChanged;

        #endregion

        #region Properties

        /// <summary>Rooms currently visible on the LAN (updated in place).</summary>
        public IReadOnlyList<RoomInfo> Rooms => _roomList;

        /// <summary>True when searching is active but nothing has been found ("No rooms found").</summary>
        public bool NoRoomsFound => IsSearching && _roomList.Count == 0;

        /// <summary>True while the LAN listener is active.</summary>
        public bool IsSearching => _listener.IsRunning;

        /// <summary>True when the listener could not bind the discovery port.</summary>
        public bool SearchFailed { get; private set; }

        /// <summary>True while this machine is advertising a room.</summary>
        public bool IsAdvertising => _broadcaster.IsRunning;

        /// <summary>
        /// Consecutive failed advertisement sends while hosting. Hosts use this
        /// to dissolve the room when the local network interface dies.
        /// </summary>
        public int ConsecutiveBroadcastFailures => _broadcaster.ConsecutiveSendFailures;

        #endregion

        #region Construction

        public DiscoveryManager(IEventBus eventBus)
        {
            _eventBus = eventBus;
            _broadcaster = new LanBroadcastService(DiscoveryPort);
            _listener = new LanListenerService(DiscoveryPort);
            _onAdvertisement = (address, advertisement) =>
                _registryChanged |= UpsertRoom(address, advertisement, _now);
        }

        #endregion

        #region Host role

        /// <summary>Starts advertising this machine's room (host only).</summary>
        public void StartAdvertising(RoomAdvertisement advertisement) => _broadcaster.Start(advertisement);

        /// <summary>Updates the advertised payload (player count, match state...).</summary>
        public void UpdateAdvertisement(RoomAdvertisement advertisement)
        {
            if (_broadcaster.IsRunning)
                _broadcaster.UpdateAdvertisement(advertisement);
        }

        /// <summary>Stops advertising (room closed or match hidden).</summary>
        public void StopAdvertising() => _broadcaster.Stop();

        #endregion

        #region Client role

        /// <summary>Starts listening for rooms on the LAN.</summary>
        public void StartSearching()
        {
            _roomsByAddress.Clear();
            _roomList.Clear();
            SearchFailed = !_listener.Start();
            PublishRoomList();
        }

        /// <summary>Stops listening and clears results.</summary>
        public void StopSearching()
        {
            _listener.Stop();
            SearchFailed = false;
            _roomsByAddress.Clear();
            _roomList.Clear();
            PublishRoomList();
        }

        /// <summary>
        /// Resolves a user-entered room code to a discovered room.
        /// Returns false when no room with that code is currently visible.
        /// </summary>
        public bool TryResolveRoomCode(string roomCode, out RoomInfo room)
        {
            room = null;
            if (string.IsNullOrWhiteSpace(roomCode)) return false;

            foreach (RoomInfo candidate in _roomList)
            {
                if (string.Equals(candidate.Advertisement.RoomCode, roomCode.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    room = candidate;
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region ITickable

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            _now = Time.unscaledTime;

            _broadcaster.Tick(_now);

            if (!_listener.IsRunning) return;

            _registryChanged = false;
            _listener.Tick(_onAdvertisement);

            // Expire silent rooms twice per stale window.
            if (_now >= _nextExpiryCheck)
            {
                _nextExpiryCheck = _now + RoomStaleSeconds * 0.5f;
                _registryChanged |= ExpireStaleRooms(_now);
            }

            if (_registryChanged)
                PublishRoomList();
        }

        #endregion

        #region Registry internals

        private bool UpsertRoom(string address, RoomAdvertisement advertisement, float now)
        {
            if (_roomsByAddress.TryGetValue(address, out RoomInfo existing))
            {
                existing.Advertisement = advertisement;
                existing.LastSeenTime = now;
                return true; // payload may have changed (player count etc.)
            }

            var room = new RoomInfo
            {
                HostAddress = address,
                Advertisement = advertisement,
                LastSeenTime = now
            };
            _roomsByAddress.Add(address, room);
            _roomList.Add(room);
            return true;
        }

        private bool ExpireStaleRooms(float now)
        {
            bool changed = false;
            for (int i = _roomList.Count - 1; i >= 0; i--)
            {
                if (now - _roomList[i].LastSeenTime > RoomStaleSeconds)
                {
                    _roomsByAddress.Remove(_roomList[i].HostAddress);
                    _roomList.RemoveAt(i);
                    changed = true;
                }
            }

            return changed;
        }

        private void PublishRoomList() =>
            _eventBus.Publish(new RoomListUpdatedEvent { Rooms = _roomList });

        #endregion

        #region Test support

        /// <summary>Test hook: inserts or updates a room without UDP I/O.</summary>
        internal void TestReceiveAdvertisement(string address, RoomAdvertisement advertisement, float now)
        {
            if (UpsertRoom(address, advertisement, now))
                PublishRoomList();
        }

        /// <summary>Test hook: expires stale rooms at the given timestamp.</summary>
        internal void TestExpireStaleRooms(float now)
        {
            if (ExpireStaleRooms(now))
                PublishRoomList();
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            _broadcaster.Dispose();
            _listener.Dispose();
        }

        #endregion
    }
}
