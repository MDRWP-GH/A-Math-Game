using System;
using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Events;
using AMath.Networking;
using AMath.Networking.Room;
using AMath.Networking.RPC;
using UnityEngine;

namespace AMath.UI
{
    /// <summary>
    /// Presenter for the room lobby: member list, room code display and the
    /// host's "start match" action. Members are read from the replicated
    /// <see cref="NetworkPlayer"/> objects, so no extra roster sync exists.
    /// </summary>
    public sealed class LobbyPresenter : MonoBehaviour
    {
        #region View-facing events

        /// <summary>Membership changed; rebuild the member list.</summary>
        public event Action<IReadOnlyList<NetworkPlayer>> MembersChanged;

        #endregion

        #region Fields

        private readonly List<NetworkPlayer> _members = new(GameRules.MaxPlayers);
        private IEventBus _eventBus;
        private RoomManager _roomManager;
        private RoomSession _session;

        #endregion

        #region Properties

        /// <summary>Room code to display prominently ("share this with friends").</summary>
        public string RoomCode => _session?.RoomCode ?? string.Empty;

        /// <summary>Room display name.</summary>
        public string RoomName => _session?.RoomName ?? string.Empty;

        /// <summary>Only the host may start the match, and only with enough players.</summary>
        public bool CanStartMatch => _session is { IsHost: true } && _members.Count >= GameRules.MinPlayers;

        #endregion

        #region Lifecycle

        private void Start()
        {
            _eventBus = NetworkContext.Services.Resolve<IEventBus>();
            _roomManager = NetworkContext.Services.Resolve<RoomManager>();
            _session = NetworkContext.Services.Resolve<RoomSession>();

            _eventBus.Subscribe<PlayerRosterChangedEvent>(OnRosterChanged);
            RefreshMembers();
        }

        private void OnDestroy()
        {
            _eventBus?.Unsubscribe<PlayerRosterChangedEvent>(OnRosterChanged);
        }

        private void OnRosterChanged(PlayerRosterChangedEvent evt) => RefreshMembers();

        private void RefreshMembers()
        {
            _members.Clear();
            _members.AddRange(FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None));
            MembersChanged?.Invoke(_members);
        }

        #endregion

        #region View commands

        /// <summary>Host action: assign seats and start the match.</summary>
        public void StartMatch() => _roomManager.StartMatch();

        /// <summary>Leaves the room (both roles).</summary>
        public void LeaveRoom() => _roomManager.LeaveRoom();

        #endregion
    }
}
