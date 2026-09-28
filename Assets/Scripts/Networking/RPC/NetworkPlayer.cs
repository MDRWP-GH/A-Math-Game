using System;
using AMath.Core;
using AMath.Core.Commands;
using AMath.Core.Events;
using AMath.Core.StateMachines;
using AMath.Gameplay.Players;
using AMath.Managers;
using AMath.Networking.Messages;
using Mirror;
using UnityEngine;

namespace AMath.Networking.RPC
{
    /// <summary>
    /// Per-connection player object (assigned as Mirror's player prefab).
    ///
    /// This is the **only** client-to-host channel for gameplay:
    /// <see cref="CmdSubmitCommand"/> ships serialized command bytes, and the
    /// host resolves the acting player from the connection itself — never from
    /// the payload — so tampered payloads cannot impersonate other players.
    /// Rejection reasons come back privately via a TargetRpc.
    /// </summary>
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        #region Constants

        /// <summary>Interval between client connection-quality reports (host migration ranking).</summary>
        private const float QualityReportInterval = 5f;

        /// <summary>
        /// Ceiling for a serialized command. The largest legal command places a
        /// full rack, so anything beyond this is malformed or hostile and is
        /// dropped before it reaches the deserializer. Shared with the record
        /// reader so the inbound and outbound paths cannot drift apart.
        /// </summary>
        private const int MaxCommandPayloadBytes = NetworkDtoSerialization.MaxCommandPayloadBytes;

        #endregion

        #region SyncVars

        /// <summary>Seat id (-1 while unseated in the lobby). Host-assigned only.</summary>
        [SyncVar(hook = nameof(OnPlayerIdChanged))]
        private int _playerId = -1;

        /// <summary>Display name shown in lobby and match UI.</summary>
        [SyncVar(hook = nameof(OnDisplayNameChanged))]
        private string _displayName;

        /// <summary>Persistent identity GUID (used to rebuild seating after migration).</summary>
        [SyncVar]
        private string _persistentGuid;

        /// <summary>True for the connection that owns the room (replicated to all clients).</summary>
        [SyncVar]
        private bool _isHost;

        /// <summary>
        /// Index into <see cref="PlayerColorPalette"/>. Assigned randomly by the
        /// host on join and changeable by the owner while in the lobby.
        /// </summary>
        [SyncVar(hook = nameof(OnColorIdChanged))]
        private byte _colorId;

        /// <summary>
        /// Team picked in the lobby (0 or 1). Frozen into MatchConfig at start.
        /// </summary>
        [SyncVar(hook = nameof(OnLobbyTeamIdChanged))]
        private byte _lobbyTeamId;

        /// <summary>
        /// Room-wide match format chosen by the host. Only the host player's
        /// value is authoritative; other copies stay at the default.
        /// </summary>
        [SyncVar(hook = nameof(OnLobbyMatchFormatChanged))]
        private byte _lobbyMatchFormat;

        /// <summary>
        /// Per-turn limit preset chosen by the host. Only the host player's
        /// value is authoritative; other copies stay at the default.
        /// </summary>
        [SyncVar(hook = nameof(OnLobbyTurnTimePresetChanged))]
        private byte _lobbyTurnTimePreset;

        #endregion

        #region Fields

        private IEventBus _eventBus;
        private GameManager _gameManager;
        private PlayerManager _playerManager;
        private float _nextQualityReport;

        #endregion

        #region Properties

        /// <summary>Seat id (-1 while unseated).</summary>
        public int PlayerId => _playerId;

        /// <summary>Display name.</summary>
        public string DisplayName => _displayName;

        /// <summary>Persistent identity GUID.</summary>
        public string PersistentGuid => _persistentGuid;

        /// <summary>True when this connection is the room host.</summary>
        public bool IsHost => _isHost;

        /// <summary>Index into <see cref="PlayerColorPalette"/> for this player's colour.</summary>
        public byte ColorId => _colorId;

        /// <summary>Lobby team selection (0 or 1). Meaningful in team mode only.</summary>
        public byte LobbyTeamId => _lobbyTeamId;

        /// <summary>Host-selected lobby format. Meaningful on the host player only.</summary>
        public MatchFormat LobbyMatchFormat => (MatchFormat)_lobbyMatchFormat;

        /// <summary>Host-selected turn-time preset. Meaningful on the host player only.</summary>
        public TurnTimePreset LobbyTurnTimePreset =>
            TurnTimePresetExtensions.IsDefined(_lobbyTurnTimePreset)
                ? (TurnTimePreset)_lobbyTurnTimePreset
                : GameRules.DefaultTurnTimePreset;

        #endregion

        #region Server initialization

        /// <summary>Server-only: sets identity before the object is spawned.</summary>
        [Server]
        public void ServerInitialize(
            int playerId,
            string displayName,
            string persistentGuid,
            bool isHost = false,
            byte colorId = PlayerColorPalette.FallbackId)
        {
            _playerId = playerId;
            _displayName = displayName;
            _persistentGuid = persistentGuid;
            _isHost = isHost;
            _colorId = colorId;
        }

        /// <summary>
        /// Server-only: true when a player other than <paramref name="except"/>
        /// already holds <paramref name="colorId"/>. Reads the live connection
        /// list rather than a cached roster, so a colour freed by someone
        /// leaving becomes available again immediately.
        /// </summary>
        [Server]
        public static bool ServerIsColorTaken(byte colorId, NetworkPlayer except)
        {
            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            {
                if (connection?.identity == null) continue;
                if (!connection.identity.TryGetComponent(out NetworkPlayer other)) continue;
                if (other == except) continue;
                if (other._colorId == colorId) return true;
            }

            return false;
        }

        /// <summary>
        /// Server-only: a free colour, starting the search at a random point so
        /// joining players get an arbitrary colour rather than always the first
        /// unused one.
        /// </summary>
        [Server]
        public static byte ServerPickFreeColor() =>
            PlayerColorPalette.FirstUnused(
                candidate => ServerIsColorTaken(candidate, null),
                UnityEngine.Random.Range(0, PlayerColorPalette.Count));

        /// <summary>Server-only: updates the replicated host badge after migration.</summary>
        [Server]
        public void ServerSetIsHost(bool isHost)
        {
            _isHost = isHost;
        }

        /// <summary>Server-only: assigns the final seat when the host starts the match.</summary>
        [Server]
        public void ServerAssignSeat(int playerId)
        {
            _playerId = playerId;

            // Mirror does not invoke SyncVar hooks on the machine that sets the
            // value, so the host must update its own local-player mapping here.
            if (isLocalPlayer)
                _playerManager.LocalPlayerId = playerId;
        }

        #endregion

        #region Lifecycle

        public override void OnStartServer()
        {
            ResolveServices();
        }

        public override void OnStartClient()
        {
            ResolveServices();
            NotifyRosterChanged();
        }

        public override void OnStartLocalPlayer()
        {
            if (_playerId >= 0)
                _playerManager.LocalPlayerId = _playerId;

            _eventBus.Subscribe<LocalCommandRequestedEvent>(OnLocalCommandRequested);
            NotifyRosterChanged();
        }

        public override void OnStopLocalPlayer()
        {
            _eventBus?.Unsubscribe<LocalCommandRequestedEvent>(OnLocalCommandRequested);
        }

        private void OnDestroy()
        {
            // Unspawn removes this identity from NetworkClient.spawned after
            // OnStopClient, so refresh here once the object is actually gone.
            _eventBus?.Publish(new PlayerRosterChangedEvent());
        }

        private void ResolveServices()
        {
            if (_eventBus != null || NetworkContext.Services == null) return;
            _eventBus = NetworkContext.Services.Resolve<IEventBus>();
            _gameManager = NetworkContext.Services.Resolve<GameManager>();
            _playerManager = NetworkContext.Services.Resolve<PlayerManager>();
        }

        private void OnPlayerIdChanged(int _, int newId)
        {
            if (isLocalPlayer && newId >= 0)
                _playerManager.LocalPlayerId = newId;
        }

        private void OnDisplayNameChanged(string _, string __) => NotifyRosterChanged();

        private void OnColorIdChanged(byte _, byte __) => NotifyRosterChanged();

        private void OnLobbyTeamIdChanged(byte _, byte __) => NotifyRosterChanged();

        private void OnLobbyMatchFormatChanged(byte _, byte __) => NotifyRosterChanged();

        private void OnLobbyTurnTimePresetChanged(byte _, byte __) => NotifyRosterChanged();

        private void NotifyRosterChanged()
        {
            ResolveServices();
            _eventBus?.Publish(new PlayerRosterChangedEvent());
        }

        #endregion

        #region Colour channel (client -> host)

        /// <summary>
        /// Asks the host for a colour. Only meaningful for the local player;
        /// the host decides whether the request is allowed.
        /// </summary>
        public void RequestColor(byte colorId)
        {
            if (!isLocalPlayer) return;
            CmdRequestColor(colorId);
        }

        /// <summary>
        /// Host-side colour change. Silently ignored rather than reported when
        /// refused: the lobby already hides taken colours, so a rejection here
        /// only happens when two players tapped the same swatch at once, and an
        /// error popup for that would be noise.
        /// </summary>
        [Command]
        private void CmdRequestColor(byte colorId)
        {
            if (!PlayerColorPalette.IsValid(colorId)) return;

            // Colours are frozen into MatchConfig at match start, so allowing a
            // change afterwards would leave the HUD disagreeing with the replay.
            if (_gameManager != null && _gameManager.Phase != MatchPhase.Lobby) return;

            if (ServerIsColorTaken(colorId, this)) return;

            _colorId = colorId;

            // Mirror skips SyncVar hooks on the machine that assigns the value,
            // so the host refreshes its own lobby list explicitly.
            NotifyRosterChanged();
        }

        #endregion

        #region Lobby format channel (host -> everyone)

        /// <summary>
        /// Host-only: publishes the chosen match format so every machine can
        /// show the team picker and pick a side.
        /// </summary>
        public void RequestLobbyMatchFormat(MatchFormat format)
        {
            if (!isLocalPlayer || !_isHost) return;
            if (format != MatchFormat.Individual && format != MatchFormat.Team) return;

            if (isServer)
                ServerSetLobbyMatchFormat(format);
            else
                CmdSetLobbyMatchFormat((byte)format);
        }

        [Command]
        private void CmdSetLobbyMatchFormat(byte format)
        {
            if (!_isHost) return;
            ServerSetLobbyMatchFormat((MatchFormat)format);
        }

        [Server]
        public void ServerSetLobbyMatchFormat(MatchFormat format)
        {
            TrySetLobbyMatchFormat(format);
        }

        /// <summary>
        /// Applies a host-selected lobby format once. Kept separate from the
        /// Mirror entry point so repeated-update behaviour is directly testable.
        /// </summary>
        internal bool TrySetLobbyMatchFormat(MatchFormat format)
        {
            if (_gameManager != null && _gameManager.Phase != MatchPhase.Lobby) return false;
            if (format != MatchFormat.Individual && format != MatchFormat.Team) return false;

            byte encoded = (byte)format;
            if (_lobbyMatchFormat == encoded)
                return false;

            _lobbyMatchFormat = encoded;
            NotifyRosterChanged();
            return true;
        }

        #endregion

        #region Lobby turn-time channel (host -> everyone)

        /// <summary>
        /// Host-only: publishes the chosen per-turn limit so every machine can
        /// show the same Rush → Long selection.
        /// </summary>
        public void RequestLobbyTurnTimePreset(TurnTimePreset preset)
        {
            if (!isLocalPlayer || !_isHost) return;
            if (!TurnTimePresetExtensions.IsDefined((byte)preset)) return;

            if (isServer)
                ServerSetLobbyTurnTimePreset(preset);
            else
                CmdSetLobbyTurnTimePreset((byte)preset);
        }

        [Command]
        private void CmdSetLobbyTurnTimePreset(byte preset)
        {
            if (!_isHost) return;
            ServerSetLobbyTurnTimePreset((TurnTimePreset)preset);
        }

        [Server]
        public void ServerSetLobbyTurnTimePreset(TurnTimePreset preset)
        {
            TrySetLobbyTurnTimePreset(preset);
        }

        /// <summary>
        /// Applies a host-selected turn-time preset once. Kept separate from the
        /// Mirror entry point so repeated-update behaviour is directly testable.
        /// </summary>
        internal bool TrySetLobbyTurnTimePreset(TurnTimePreset preset)
        {
            if (_gameManager != null && _gameManager.Phase != MatchPhase.Lobby) return false;
            if (!TurnTimePresetExtensions.IsDefined((byte)preset)) return false;

            byte encoded = (byte)preset;
            if (_lobbyTurnTimePreset == encoded)
                return false;

            _lobbyTurnTimePreset = encoded;
            NotifyRosterChanged();
            return true;
        }

        #endregion

        #region Team channel (client -> host)

        /// <summary>
        /// Asks the host to join a team. Only meaningful for the local player
        /// while the room is still in the lobby.
        /// </summary>
        public void RequestTeam(byte teamId)
        {
            if (!isLocalPlayer) return;
            CmdRequestTeam(teamId);
        }

        [Command]
        private void CmdRequestTeam(byte teamId)
        {
            if (teamId >= GameRules.TeamCount) return;

            if (_gameManager != null && _gameManager.Phase != MatchPhase.Lobby) return;

            _lobbyTeamId = teamId;
            NotifyRosterChanged();
        }

        #endregion

        #region Command channel (client -> host)

        private void OnLocalCommandRequested(LocalCommandRequestedEvent evt)
        {
            if (evt.Command == null) return;
            CmdSubmitCommand((byte)evt.Command.Type, CommandSerializer.Serialize(evt.Command));
        }

        /// <summary>
        /// Host-side entry point for every client action. Fully validated;
        /// malformed or illegal requests are rejected without side effects.
        /// </summary>
        [Command]
        private void CmdSubmitCommand(byte commandType, byte[] payload)
        {
            PlayerState seat = _playerManager.GetById(_playerId);
            if (seat == null || seat.IsAi)
            {
                TargetCommandRejected(connectionToClient, "This seat cannot submit commands.");
                return;
            }

            if (payload != null && payload.Length > MaxCommandPayloadBytes)
            {
                TargetCommandRejected(connectionToClient, "Command payload is too large.");
                return;
            }

            IGameCommand command;
            try
            {
                command = CommandSerializer.Deserialize((CommandType)commandType, payload);
            }
            catch (Exception)
            {
                TargetCommandRejected(connectionToClient, "Malformed command.");
                return;
            }

            // Clients may not fabricate host-only timeout passes.
            if (command is PassTurnCommand pass)
                pass.WasTimeout = false;

            var outcome = _gameManager.SubmitCommand(_playerId, command, out _);
            if (!outcome.Success)
                TargetCommandRejected(connectionToClient, outcome.Error);
            // Success needs no reply here: NetworkGameState broadcasts the
            // authoritative TurnRecord to everyone, including the sender.
        }

        [TargetRpc]
        private void TargetCommandRejected(NetworkConnectionToClient _, string reason)
        {
            _eventBus?.Publish(new CommandRejectedEvent { Reason = reason });
        }

        #endregion

        #region Connection quality reporting (host migration input)

        private void Update()
        {
            if (!isLocalPlayer || isServer || _playerId < 0) return;

            if (Time.unscaledTime >= _nextQualityReport)
            {
                _nextQualityReport = Time.unscaledTime + QualityReportInterval;
                CmdReportQuality(NetworkTime.rtt);
            }
        }

        [Command]
        private void CmdReportQuality(double rttSeconds)
        {
            _eventBus?.Publish(new QualityReportReceivedEvent
            {
                PlayerId = _playerId,
                RttSeconds = rttSeconds
            });
        }

        #endregion
    }
}
