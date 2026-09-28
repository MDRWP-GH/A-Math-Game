using System;
using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Events;
using AMath.Core.Snapshot;
using AMath.Core.StateMachines;
using AMath.Managers;
using AMath.Networking.Discovery;
using AMath.Networking.RPC;
using AMath.Networking.Transport;
using kcp2k;
using Mirror;
using UnityEngine;

namespace AMath.Networking.Room
{
    /// <summary>
    /// Owns the room lifecycle on both roles:
    ///  - Host: create room (code, transport, advertising), start the match
    ///    with final seat assignment, keep the broadcast payload fresh,
    ///    re-host a match from a snapshot after host migration.
    ///  - Client: join a discovered room, or join by room code (resolved to a
    ///    host IP through discovery — never from the code alone).
    /// </summary>
    public sealed class RoomManager : IDisposable
    {
        #region Fields

        private readonly IEventBus _eventBus;
        private readonly RoomSession _session;
        private readonly DiscoveryManager _discovery;
        private readonly GameManager _gameManager;
        private readonly AMathNetworkManager _networkManager;
        private readonly Func<bool> _networkAvailable;

        // Kept so Dispose can unsubscribe the exact delegates that were registered.
        private readonly Action<PlayerRosterChangedEvent> _onRosterChanged;
        private readonly Action<MatchPhaseChangedEvent> _onPhaseChanged;

        #endregion

        #region Construction

        public RoomManager(
            IEventBus eventBus,
            RoomSession session,
            DiscoveryManager discovery,
            GameManager gameManager,
            AMathNetworkManager networkManager,
            Func<bool> networkAvailable = null)
        {
            _eventBus = eventBus;
            _session = session;
            _discovery = discovery;
            _gameManager = gameManager;
            _networkManager = networkManager;
            _networkAvailable = networkAvailable ?? LanBroadcastTargets.HasUsableLanInterface;

            // Keep the advertised payload current without polling. The handlers
            // are stored so Dispose can unsubscribe the exact same delegates.
            _onRosterChanged = _ => RefreshAdvertisement();
            _onPhaseChanged = _ => RefreshAdvertisement();
            _eventBus.Subscribe(_onRosterChanged);
            _eventBus.Subscribe(_onPhaseChanged);
        }

        #endregion

        #region Host role

        /// <summary>Creates a room, starts hosting and begins advertising on the LAN.</summary>
        public bool CreateRoom(string roomName, int maxPlayers, ushort port = TransportConfigurator.DefaultPort)
            => TryCreateRoom(roomName, maxPlayers, out _, port);

        /// <summary>Creates a room and returns a stable reason when startup cannot complete.</summary>
        public bool TryCreateRoom(
            string roomName,
            int maxPlayers,
            out RoomOperationError error,
            ushort port = TransportConfigurator.DefaultPort)
        {
            error = EvaluateCreateAvailability(
                _session.IsActive, NetworkServer.active, NetworkClient.active, _networkAvailable());
            if (error != RoomOperationError.None)
            {
                return false;
            }

            maxPlayers = Mathf.Clamp(maxPlayers, GameRules.MinPlayers, GameRules.MaxPlayers);

            _session.RoomName = string.IsNullOrWhiteSpace(roomName) ? RoomSession.DefaultRoomName : roomName.Trim();
            _session.RoomCode = RoomCodeGenerator.Generate();
            _session.MaxPlayers = maxPlayers;
            _session.Port = port;
            _session.IsHost = true;
            _session.IsActive = true;

            try
            {
                if (!ConfigureTransport(port))
                    throw new InvalidOperationException("The active transport is not KCP.");

                _networkManager.maxConnections = maxPlayers;
                _discovery.StopSearching();
                if (!_discovery.StartAdvertising(BuildAdvertisement()))
                    throw new InvalidOperationException("LAN room advertising could not start.");

                _networkManager.StartHost();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Room] Could not start host: {ex.Message}");
                RollbackFailedStart();
                error = _networkAvailable()
                    ? RoomOperationError.TransportFailed
                    : RoomOperationError.NetworkUnavailable;
                return false;
            }

            Debug.Log($"[Room] Hosting '{_session.RoomName}' code {_session.RoomCode} on port {port}.");
            return true;
        }

        /// <summary>Host-only: assigns human seats and starts the match.</summary>
        public bool StartMatch(MatchFormat format = MatchFormat.Individual)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("[Room] Only the host can start the match.");
                return false;
            }

            List<NetworkPlayer> members = CollectSeatedMembers();
            if (members.Count < 1)
            {
                Debug.LogWarning("[Room] Need at least the host to start.");
                return false;
            }

            if (!GameRules.IsValidHumanRoster(members.Count))
            {
                Debug.LogWarning("[Room] Need 2–4 human players to start.");
                return false;
            }

            if (format == MatchFormat.Team)
            {
                CountTeams(members, out int team0, out int team1);
                if (!GameRules.IsValidTeamSplit(team0, team1))
                {
                    Debug.LogWarning("[Room] Both teams need at least one player.");
                    return false;
                }
            }

            var humans = new List<PlayerIdentity>(members.Count);

            for (int seat = 0; seat < members.Count; seat++)
            {
                members[seat].ServerAssignSeat(seat);
                byte colorId = members[seat].ColorId;

                humans.Add(new PlayerIdentity
                {
                    PlayerId = seat,
                    PersistentGuid = members[seat].PersistentGuid,
                    DisplayName = members[seat].DisplayName,
                    IsAi = false,
                    TeamId = format == MatchFormat.Team ? members[seat].LobbyTeamId : -1,
                    ColorId = colorId
                });
            }

            MatchConfig config = BuildMatchConfig(
                humans,
                format,
                Guid.NewGuid().GetHashCode(),
                GameRules.TurnSecondsFor(_session.SelectedTurnTimePreset),
                Application.version);

            // NetworkGameState (server side) hears MatchStartedEvent and
            // broadcasts the config so every client starts identically.
            _gameManager.StartMatch(config);
            return true;
        }

        /// <summary>
        /// Host-migration entry point: restore the match from the latest
        /// snapshot, become the host and re-advertise the room under the SAME
        /// room code so waiting clients can rediscover and rejoin it.
        /// </summary>
        public bool RehostFromSnapshot(
            GameStateSnapshot snapshot,
            string roomCode,
            string roomName,
            int maxPlayers,
            ushort port = TransportConfigurator.DefaultPort)
        {
            if (NetworkServer.active || NetworkClient.active)
            {
                Debug.LogWarning("[Room] Cannot re-host while a session is active.");
                return false;
            }

            _session.RoomName = roomName;
            _session.RoomCode = roomCode;
            _session.MaxPlayers = maxPlayers;
            _session.Port = port;
            _session.IsHost = true;
            _session.IsActive = true;

            // Restore state BEFORE hosting so the authenticator immediately
            // treats returning players (and our own local client) as reconnections.
            // Paused rather than the snapshot's phase: the room has no players
            // in it yet.
            _gameManager.RestoreSnapshot(snapshot, MatchPhase.Paused);

            ConfigureTransport(_session.Port);
            _networkManager.maxConnections = maxPlayers;
            _networkManager.StartHost();

            _discovery.StopSearching();
            _discovery.StartAdvertising(BuildAdvertisement());

            Debug.Log($"[Room] Re-hosting room {roomCode} after migration; waiting for players.");
            return true;
        }

        #endregion

        #region Client role

        /// <summary>Joins a discovered room by connecting to its socket-verified address.</summary>
        public bool JoinRoom(RoomInfo room)
            => TryJoinRoom(room, out _);

        /// <summary>Connects to a discovered room and reports why it was rejected locally.</summary>
        public bool TryJoinRoom(RoomInfo room, out RoomOperationError error)
        {
            error = EvaluateJoinAvailability(
                room,
                _session.IsActive,
                NetworkServer.active,
                NetworkClient.active,
                _networkAvailable(),
                _gameManager?.Config != null);
            if (error != RoomOperationError.None)
                return false;

            _session.RoomName = room.Advertisement.RoomName;
            _session.RoomCode = room.Advertisement.RoomCode;
            _session.MaxPlayers = room.Advertisement.MaxPlayers;
            _session.Port = (ushort)room.Advertisement.Port;
            _session.IsHost = false;
            _session.IsActive = true;

            try
            {
                if (!ConfigureTransport(_session.Port))
                    throw new InvalidOperationException("The active transport is not KCP.");

                _networkManager.networkAddress = room.HostAddress;
                _networkManager.StartClient();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Room] Could not start client: {ex.Message}");
                RollbackFailedStart();
                error = _networkAvailable()
                    ? RoomOperationError.TransportFailed
                    : RoomOperationError.NetworkUnavailable;
                return false;
            }

            Debug.Log($"[Room] Joining '{room.Advertisement.RoomName}' at {room.HostAddress}:{room.Advertisement.Port}.");
            return true;
        }

        /// <summary>
        /// Joins by user-entered code. The code is only a lookup key into the
        /// discovery registry; when no broadcast with that code has been seen,
        /// the join fails with a clear reason.
        ///
        /// <paramref name="error"/> is a localization key, not prose: the UI
        /// runs it through the text provider like every other failure reason.
        /// </summary>
        public bool JoinByCode(string code, out string error)
        {
            bool joined = TryJoinByCode(code, out RoomOperationError operationError);
            error = joined ? null : RoomOperationErrorText.LocalizationKey(operationError);
            return joined;
        }

        public bool TryJoinByCode(string code, out RoomOperationError error)
        {
            if (!RoomCodeGenerator.IsValidFormat(code))
            {
                error = RoomOperationError.CodeInvalid;
                return false;
            }

            if (!_discovery.TryResolveRoomCode(code, out RoomInfo room))
            {
                error = RoomOperationError.CodeNotFound;
                return false;
            }

            return TryJoinRoom(room, out error);
        }

        #endregion

        #region Shared

        /// <summary>Leaves the current room (both roles) and stops advertising.</summary>
        public void LeaveRoom()
        {
            _discovery.StopAdvertising();

            // Mark the leave as intentional BEFORE stopping: the disconnect
            // callbacks fired by StopHost/StopClient must not be mistaken for
            // a connection loss by the reconnection pipeline.
            _session.IsActive = false;

            if (NetworkServer.active)
                _networkManager.StopHost();
            else if (NetworkClient.active)
                _networkManager.StopClient();

            _session.Reset();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _eventBus.Unsubscribe(_onRosterChanged);
            _eventBus.Unsubscribe(_onPhaseChanged);
            _discovery.StopAdvertising();

            // Searching owns a bound UDP socket and a receive thread; leaving it
            // running past teardown keeps the discovery port claimed and pushes
            // room-list updates at a presenter that is already gone.
            _discovery.StopSearching();
        }

        #endregion

        #region Internals

        private bool ConfigureTransport(ushort port)
        {
            if (Mirror.Transport.active is KcpTransport kcp)
            {
                TransportConfigurator.Configure(kcp, port);
                return true;
            }

            Debug.LogError("[Room] Active transport is not KcpTransport.");
            return false;
        }

        internal static RoomOperationError EvaluateCreateAvailability(
            bool sessionActive,
            bool serverActive,
            bool clientActive,
            bool networkAvailable)
        {
            if (sessionActive || serverActive || clientActive)
                return RoomOperationError.AlreadyInSession;
            return networkAvailable ? RoomOperationError.None : RoomOperationError.NetworkUnavailable;
        }

        internal static RoomOperationError EvaluateJoinAvailability(
            RoomInfo room,
            bool sessionActive,
            bool serverActive,
            bool clientActive,
            bool networkAvailable,
            bool hasExistingMatch)
        {
            if (sessionActive || serverActive || clientActive)
                return RoomOperationError.AlreadyInSession;
            if (!networkAvailable)
                return RoomOperationError.NetworkUnavailable;
            if (room?.Advertisement == null
                || string.IsNullOrWhiteSpace(room.HostAddress)
                || room.Advertisement.Port <= 0
                || room.Advertisement.Port > ushort.MaxValue
                || !RoomCodeGenerator.IsValidFormat(room.Advertisement.RoomCode)
                || room.Advertisement.MaxPlayers < GameRules.MinPlayers
                || room.Advertisement.MaxPlayers > GameRules.MaxPlayers
                || room.Advertisement.CurrentPlayers < 0)
                return RoomOperationError.InvalidRoom;
            if (room.Advertisement.MatchInProgress && !hasExistingMatch)
                return RoomOperationError.MatchStarted;
            if (!room.Advertisement.MatchInProgress && !room.IsJoinable)
                return RoomOperationError.RoomFull;
            return RoomOperationError.None;
        }

        internal static MatchConfig BuildMatchConfig(
            IReadOnlyList<PlayerIdentity> humans,
            MatchFormat format,
            int randomSeed,
            int turnSeconds,
            string gameVersion)
        {
            if (humans == null)
                throw new ArgumentNullException(nameof(humans));
            if (!GameRules.IsValidHumanRoster(humans.Count))
                throw new ArgumentOutOfRangeException(nameof(humans));

            var config = new MatchConfig
            {
                RandomSeed = randomSeed,
                TurnSeconds = turnSeconds,
                GameVersion = gameVersion,
                Format = format
            };

            for (int seat = 0; seat < humans.Count; seat++)
            {
                PlayerIdentity human = humans[seat]
                    ?? throw new ArgumentException("Human identities cannot contain null entries.", nameof(humans));
                config.Players.Add(new PlayerIdentity
                {
                    PlayerId = seat,
                    PersistentGuid = human.PersistentGuid,
                    DisplayName = human.DisplayName,
                    IsAi = false,
                    TeamId = format == MatchFormat.Team ? human.TeamId : -1,
                    ColorId = human.ColorId
                });
            }

            return config;
        }

        private void RollbackFailedStart()
        {
            _discovery.StopAdvertising();
            _session.IsActive = false;

            try
            {
                if (NetworkServer.active)
                    _networkManager.StopHost();
                else if (NetworkClient.active)
                    _networkManager.StopClient();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Room] Transport rollback reported: {ex.Message}");
            }

            _session.Reset();
        }

        private RoomAdvertisement BuildAdvertisement() => new()
        {
            RoomName = _session.RoomName,
            RoomCode = _session.RoomCode,
            CurrentPlayers = NetworkServer.active ? NetworkServer.connections.Count : 0,
            MaxPlayers = _session.MaxPlayers,
            GameVersion = Application.version,
            Port = _session.Port,
            // Only a live match blocks fresh joins. A Finished match still has a
            // Config, so testing "not Lobby" left the post-match room advertised
            // as in-progress and unjoinable until the host recreated it.
            MatchInProgress = _gameManager.Phase is MatchPhase.Playing or MatchPhase.Paused
        };

        private void RefreshAdvertisement()
        {
            if (_session.IsHost && _discovery.IsAdvertising)
                _discovery.UpdateAdvertisement(BuildAdvertisement());
        }

        private static List<NetworkPlayer> CollectSeatedMembers()
        {
            var members = new List<NetworkPlayer>(GameRules.MaxPlayers);

            // Host's own player first (seat 0), then clients by connection id
            // (join order) for a stable, explainable seating.
            var ordered = new List<NetworkConnectionToClient>(NetworkServer.connections.Values);
            ordered.Sort((a, b) => a.connectionId.CompareTo(b.connectionId));

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

            return members;
        }

        private static void CountTeams(List<NetworkPlayer> members, out int team0Count, out int team1Count)
        {
            team0Count = 0;
            team1Count = 0;

            foreach (NetworkPlayer member in members)
            {
                if (member.LobbyTeamId == 0)
                    team0Count++;
                else
                    team1Count++;
            }
        }

        #endregion
    }
}
