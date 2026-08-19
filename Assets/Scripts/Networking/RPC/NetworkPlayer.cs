using System;
using AMath.Core.Commands;
using AMath.Core.Events;
using AMath.Gameplay.Players;
using AMath.Managers;
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
        /// dropped before it reaches the deserializer.
        /// </summary>
        private const int MaxCommandPayloadBytes = 256;

        #endregion

        #region SyncVars

        /// <summary>Seat id (-1 while unseated in the lobby). Host-assigned only.</summary>
        [SyncVar(hook = nameof(OnPlayerIdChanged))]
        private int _playerId = -1;

        /// <summary>Display name shown in lobby and match UI.</summary>
        [SyncVar]
        private string _displayName;

        /// <summary>Persistent identity GUID (used to rebuild seating after migration).</summary>
        [SyncVar]
        private string _persistentGuid;

        /// <summary>True for the connection that owns the room (replicated to all clients).</summary>
        [SyncVar]
        private bool _isHost;

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

        #endregion

        #region Server initialization

        /// <summary>Server-only: sets identity before the object is spawned.</summary>
        [Server]
        public void ServerInitialize(int playerId, string displayName, string persistentGuid, bool isHost = false)
        {
            _playerId = playerId;
            _displayName = displayName;
            _persistentGuid = persistentGuid;
            _isHost = isHost;
        }

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
        }

        public override void OnStartLocalPlayer()
        {
            if (_playerId >= 0)
                _playerManager.LocalPlayerId = _playerId;

            _eventBus.Subscribe<LocalCommandRequestedEvent>(OnLocalCommandRequested);
        }

        public override void OnStopLocalPlayer()
        {
            _eventBus?.Unsubscribe<LocalCommandRequestedEvent>(OnLocalCommandRequested);
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
