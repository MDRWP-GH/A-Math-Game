using System;
using System.Collections.Generic;
using AMath.Core.Events;
using AMath.Networking;
using AMath.Networking.Discovery;
using AMath.Networking.Room;
using UnityEngine;

namespace AMath.UI
{
    /// <summary>
    /// Presenter for the "find / create room" screen. Contains no layout code:
    /// it exposes C# events and simple methods so any view (uGUI, UI Toolkit,
    /// the project's UiFactory) can bind to it. All game knowledge stays in
    /// the managers; this class only translates.
    /// </summary>
    public sealed class RoomBrowserPresenter : MonoBehaviour
    {
        #region View-facing events

        /// <summary>Room list changed. Empty list + searching means show "No rooms found".</summary>
        public event Action<IReadOnlyList<RoomInfo>> RoomsChanged;

        /// <summary>A join/create attempt failed with a user-readable reason.</summary>
        public event Action<string> ErrorRaised;

        #endregion

        #region Fields

        private IEventBus _eventBus;
        private DiscoveryManager _discovery;
        private RoomManager _roomManager;
        private bool _initialized;

        #endregion

        #region Properties

        /// <summary>True when searching is active and no rooms are visible.</summary>
        public bool NoRoomsFound => _discovery is { NoRoomsFound: true };

        #endregion

        #region Lifecycle

        private void Start() => EnsureInitialized();

        private void OnDestroy()
        {
            if (!_initialized) return;
            _eventBus.Unsubscribe<RoomListUpdatedEvent>(OnRoomListUpdated);
        }

        private void OnRoomListUpdated(RoomListUpdatedEvent evt) => RoomsChanged?.Invoke(evt.Rooms);

        private void EnsureInitialized()
        {
            if (_initialized) return;
            if (NetworkContext.Services == null) return;

            _eventBus = NetworkContext.Services.Resolve<IEventBus>();
            _discovery = NetworkContext.Services.Resolve<DiscoveryManager>();
            _roomManager = NetworkContext.Services.Resolve<RoomManager>();
            _eventBus.Subscribe<RoomListUpdatedEvent>(OnRoomListUpdated);
            _initialized = true;
        }

        #endregion

        #region View commands

        /// <summary>Begins LAN discovery (call when the browser screen opens).</summary>
        public void StartSearching()
        {
            EnsureInitialized();
            _discovery.StartSearching();
        }

        /// <summary>Stops LAN discovery (call when the screen closes).</summary>
        public void StopSearching()
        {
            EnsureInitialized();
            _discovery.StopSearching();
        }

        // Error events carry localization keys; the view translates them so the
        // message always matches the language selected in the settings screen.

        /// <summary>Creates and hosts a new room.</summary>
        public void CreateRoom(string roomName, int maxPlayers)
        {
            EnsureInitialized();
            if (!_roomManager.CreateRoom(roomName, maxPlayers))
                ErrorRaised?.Invoke("ui.play.err_create");
        }

        /// <summary>Joins a room selected from the discovered list.</summary>
        public void JoinRoom(RoomInfo room)
        {
            EnsureInitialized();
            if (!room.IsJoinable)
            {
                ErrorRaised?.Invoke("ui.play.err_not_joinable");
                return;
            }

            if (!_roomManager.JoinRoom(room))
                ErrorRaised?.Invoke("ui.play.err_join");
        }

        /// <summary>Joins by a user-typed room code (resolved through discovery).</summary>
        public void JoinByCode(string code)
        {
            EnsureInitialized();
            if (!_roomManager.JoinByCode(code, out string error))
                ErrorRaised?.Invoke(error);
        }

        #endregion
    }
}
