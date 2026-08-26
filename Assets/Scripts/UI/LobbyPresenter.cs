using System;
using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Events;
using AMath.Networking;
using AMath.Networking.Room;
using AMath.Networking.RPC;
using Mirror;
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

        /// <summary>Current lobby roster (host first, then join order).</summary>
        public IReadOnlyList<NetworkPlayer> Members => _members;

        /// <summary>
        /// Only the host may start. One human is enough — empty seats up to the
        /// minimum are filled with the scripted medium AI.
        /// </summary>
        public bool CanStartMatch => _session is { IsHost: true } && _members.Count >= 1;

        /// <summary>Match format selected by the host in the lobby.</summary>
        public MatchFormat SelectedFormat
        {
            get => _session?.SelectedFormat ?? MatchFormat.Individual;
            set
            {
                if (_session != null)
                    _session.SelectedFormat = value;
            }
        }

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

        /// <summary>Rebuilds the roster from live Mirror connections.</summary>
        public void RefreshMembers()
        {
            _members.Clear();

            if (NetworkServer.active)
                CollectFromServerConnections(_members);
            else if (NetworkClient.active)
                CollectFromClientSpawned(_members);

            _members.RemoveAll(static player => player == null);
            MembersChanged?.Invoke(_members);
        }

        private static void CollectFromServerConnections(List<NetworkPlayer> members)
        {
            // Same ordering as RoomManager.StartMatch: host first, then join order.
            var ordered = new List<NetworkConnectionToClient>(NetworkServer.connections.Values);
            ordered.Sort(static (a, b) => a.connectionId.CompareTo(b.connectionId));

            if (NetworkServer.localConnection?.identity != null
                && NetworkServer.localConnection.identity.TryGetComponent(out NetworkPlayer hostPlayer))
            {
                members.Add(hostPlayer);
            }

            foreach (NetworkConnectionToClient conn in ordered)
            {
                if (conn.identity != null
                    && conn.identity.TryGetComponent(out NetworkPlayer player)
                    && !members.Contains(player))
                {
                    members.Add(player);
                }
            }
        }

        private static void CollectFromClientSpawned(List<NetworkPlayer> members)
        {
            foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
            {
                if (identity != null && identity.TryGetComponent(out NetworkPlayer player))
                    members.Add(player);
            }
        }

        #endregion

        #region View commands

        /// <summary>
        /// Host action: assign seats and start the match.
        /// <paramref name="extraAiPlayers"/> adds AI opponents beyond the
        /// automatic fill-to-minimum.
        /// </summary>
        public void StartMatch(int extraAiPlayers = 0) =>
            _roomManager.StartMatch(SelectedFormat, extraAiPlayers);

        /// <summary>Leaves the room (both roles).</summary>
        public void LeaveRoom() => _roomManager.LeaveRoom();

        #endregion
    }
}
