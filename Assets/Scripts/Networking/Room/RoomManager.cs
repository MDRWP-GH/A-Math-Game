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

        #endregion

        #region Construction

        public RoomManager(
            IEventBus eventBus,
            RoomSession session,
            DiscoveryManager discovery,
            GameManager gameManager,
            AMathNetworkManager networkManager)
        {
            _eventBus = eventBus;
            _session = session;
            _discovery = discovery;
            _gameManager = gameManager;
            _networkManager = networkManager;

            // Keep the advertised payload current without polling.
            _eventBus.Subscribe<PlayerRosterChangedEvent>(_ => RefreshAdvertisement());
            _eventBus.Subscribe<MatchPhaseChangedEvent>(_ => RefreshAdvertisement());
        }

        #endregion

        #region Host role

        /// <summary>Creates a room, starts hosting and begins advertising on the LAN.</summary>
        public bool CreateRoom(string roomName, int maxPlayers, ushort port = TransportConfigurator.DefaultPort)
        {
            if (NetworkServer.active || NetworkClient.active)
            {
                Debug.LogWarning("[Room] Already in a session.");
                return false;
            }

            maxPlayers = Mathf.Clamp(maxPlayers, GameRules.MinPlayers, GameRules.MaxPlayers);

            _session.RoomName = string.IsNullOrWhiteSpace(roomName) ? "A-Math Room" : roomName.Trim();
            _session.RoomCode = RoomCodeGenerator.Generate();
            _session.MaxPlayers = maxPlayers;
            _session.Port = port;
            _session.IsHost = true;
            _session.IsActive = true;

            ConfigureTransport(port);
            _networkManager.maxConnections = maxPlayers;
            _networkManager.StartHost();

            _discovery.StopSearching();
            _discovery.StartAdvertising(BuildAdvertisement());

            Debug.Log($"[Room] Hosting '{_session.RoomName}' code {_session.RoomCode} on port {port}.");
            return true;
        }

        /// <summary>
        /// Host-only: finalizes seats and starts the match. Seat order is host
        /// first, then join order, then optional scripted AI seats. If there are
        /// fewer than <see cref="GameRules.MinPlayers"/> humans, AI seats are
        /// added automatically so a solo host can play. Extra AI opponents can
        /// be requested via <paramref name="extraAiPlayers"/>.
        /// </summary>
        public bool StartMatch(MatchFormat format = MatchFormat.Individual, int extraAiPlayers = 0)
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

            int aiCount = Math.Max(0, extraAiPlayers);
            if (members.Count + aiCount < GameRules.MinPlayers)
                aiCount = GameRules.MinPlayers - members.Count;

            if (members.Count + aiCount > GameRules.MaxPlayers)
            {
                Debug.LogWarning($"[Room] Too many seats (max {GameRules.MaxPlayers}).");
                return false;
            }

            int totalSeats = members.Count + aiCount;
            if (format == MatchFormat.Team)
            {
                if (totalSeats < GameRules.MinTeamMatchPlayers || totalSeats % GameRules.TeamCount != 0)
                {
                    Debug.LogWarning("[Room] Team matches need an even number of at least four players.");
                    return false;
                }
            }

            var config = new MatchConfig
            {
                RandomSeed = Guid.NewGuid().GetHashCode(),
                TurnSeconds = GameRules.DefaultTurnSeconds,
                GameVersion = Application.version,
                Format = format
            };

            for (int seat = 0; seat < members.Count; seat++)
            {
                members[seat].ServerAssignSeat(seat);
                config.Players.Add(new PlayerIdentity
                {
                    PlayerId = seat,
                    PersistentGuid = members[seat].PersistentGuid,
                    DisplayName = members[seat].DisplayName,
                    IsAi = false,
                    TeamId = format == MatchFormat.Team ? seat % GameRules.TeamCount : -1
                });
            }

            for (int i = 0; i < aiCount; i++)
            {
                int seat = members.Count + i;
                config.Players.Add(new PlayerIdentity
                {
                    PlayerId = seat,
                    PersistentGuid = $"ai:{config.RandomSeed}:{seat}",
                    DisplayName = aiCount == 1 ? "AI" : $"AI {i + 1}",
                    IsAi = true,
                    TeamId = format == MatchFormat.Team ? seat % GameRules.TeamCount : -1
                });
            }

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
        {
            if (NetworkServer.active || NetworkClient.active)
            {
                Debug.LogWarning("[Room] Already in a session.");
                return false;
            }

            if (room?.Advertisement == null || string.IsNullOrWhiteSpace(room.HostAddress))
            {
                Debug.LogWarning("[Room] Cannot join an invalid room advertisement.");
                return false;
            }

            // A match advertisement is only a reconnect target. Reject a fresh
            // join here so users do not wait for an authentication failure that
            // cannot result in a seat.
            if (room.Advertisement.MatchInProgress && _gameManager.Config == null)
            {
                Debug.LogWarning("[Room] Cannot join a match that is already in progress.");
                return false;
            }

            if (!room.Advertisement.MatchInProgress && !room.IsJoinable)
            {
                Debug.LogWarning("[Room] Cannot join because the room is full.");
                return false;
            }

            _session.RoomName = room.Advertisement.RoomName;
            _session.RoomCode = room.Advertisement.RoomCode;
            _session.MaxPlayers = room.Advertisement.MaxPlayers;
            _session.Port = (ushort)room.Advertisement.Port;
            _session.IsHost = false;
            _session.IsActive = true;

            ConfigureTransport(_session.Port);
            _networkManager.networkAddress = room.HostAddress;
            _networkManager.StartClient();

            Debug.Log($"[Room] Joining '{room.Advertisement.RoomName}' at {room.HostAddress}:{room.Advertisement.Port}.");
            return true;
        }

        /// <summary>
        /// Joins by user-entered code. The code is only a lookup key into the
        /// discovery registry; when no broadcast with that code has been seen,
        /// the join fails with a clear reason.
        /// </summary>
        public bool JoinByCode(string code, out string error)
        {
            error = null;

            if (!RoomCodeGenerator.IsValidFormat(code))
            {
                error = "Invalid room code format.";
                return false;
            }

            if (!_discovery.TryResolveRoomCode(code, out RoomInfo room))
            {
                error = "No room with that code was found on this network.";
                return false;
            }

            return JoinRoom(room);
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
        public void Dispose() => _discovery.StopAdvertising();

        #endregion

        #region Internals

        private void ConfigureTransport(ushort port)
        {
            if (Mirror.Transport.active is KcpTransport kcp)
                TransportConfigurator.Configure(kcp, port);
            else
                Debug.LogError("[Room] Active transport is not KcpTransport.");
        }

        private RoomAdvertisement BuildAdvertisement() => new()
        {
            RoomName = _session.RoomName,
            RoomCode = _session.RoomCode,
            CurrentPlayers = NetworkServer.active ? NetworkServer.connections.Count : 0,
            MaxPlayers = _session.MaxPlayers,
            GameVersion = Application.version,
            Port = _session.Port,
            MatchInProgress = _gameManager.Config != null && _gameManager.Phase != MatchPhase.Lobby
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

        #endregion
    }
}
