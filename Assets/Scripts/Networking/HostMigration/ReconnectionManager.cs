using AMath.Core;
using AMath.Core.Events;
using AMath.Core.StateMachines;
using AMath.Gameplay.Players;
using AMath.Managers;
using AMath.Networking.Discovery;
using AMath.Networking.Room;
using AMath.Save;
using Mirror;
using UnityEngine;

namespace AMath.Networking.HostMigration
{
    /// <summary>
    /// Orchestrates Minecraft-style disconnect handling:
    ///  - client loses host: pause/backup (mid-match), wait ~5s to reconnect,
    ///    then leave and try a fresh join if the room is still advertised;
    ///  - host loses network: dissolve the waiting room and play room;
    ///  - host pauses while seats are empty and auto-resumes when all return.
    /// </summary>
    public sealed class ReconnectionManager : ITickable, System.IDisposable
    {
        #region Constants

        /// <summary>Seconds to search for the room after leave before giving up.</summary>
        private const float FreshJoinSeconds = 5f;

        /// <summary>
        /// Consecutive discovery broadcast failures (~1 Hz) before the host
        /// dissolves the room as network-lost.
        /// </summary>
        private const int HostBroadcastFailureLimit = 3;

        #endregion

        #region Fields

        private readonly IEventBus _eventBus;
        private readonly GameStateMachine _stateMachine;
        private readonly GameManager _gameManager;
        private readonly PlayerManager _playerManager;
        private readonly SaveManager _saveManager;
        private readonly HostMigrationManager _migrationManager;
        private readonly RoomManager _roomManager;
        private readonly DiscoveryManager _discovery;
        private readonly RoomSession _session;

        private bool _freshJoining;
        private float _freshJoinElapsed;
        private string _freshJoinRoomCode;
        private string _savedReconnectToken;
        private bool _matchWasRunningAtDisconnect;

        #endregion

        #region Construction

        public ReconnectionManager(
            IEventBus eventBus,
            GameStateMachine stateMachine,
            GameManager gameManager,
            PlayerManager playerManager,
            SaveManager saveManager,
            HostMigrationManager migrationManager,
            RoomManager roomManager,
            DiscoveryManager discovery,
            RoomSession session)
        {
            _eventBus = eventBus;
            _stateMachine = stateMachine;
            _gameManager = gameManager;
            _playerManager = playerManager;
            _saveManager = saveManager;
            _migrationManager = migrationManager;
            _roomManager = roomManager;
            _discovery = discovery;
            _session = session;

            _eventBus.Subscribe<ClientDisconnectedEvent>(OnClientDisconnected);
            _eventBus.Subscribe<PlayerConnectionChangedEvent>(OnPlayerConnectionChanged);
            _eventBus.Subscribe<HostMigrationCompletedEvent>(OnMigrationCompleted);
            _eventBus.Subscribe<ClientConnectedEvent>(OnClientConnected);
        }

        #endregion

        #region Connection loss (client side)

        private void OnClientDisconnected(ClientDisconnectedEvent evt)
        {
            // The host's own local client also raises this when hosting stops;
            // recovery is only for clients that lost their server.
            if (NetworkServer.active) return;

            // Intentional leave (RoomManager.LeaveRoom clears the flag before
            // stopping) — nothing to recover.
            if (!_session.IsActive) return;

            // Fresh-join attempt failed to stay connected; keep searching.
            if (_freshJoining)
                return;

            if (_migrationManager.Phase == RecoveryPhase.Reconnecting)
            {
                _migrationManager.OnReconnectFailed();
                return;
            }

            // Already in a reconnect cycle (grace/search) — ignore duplicate events.
            if (_migrationManager.Phase != RecoveryPhase.Idle
                && _migrationManager.Phase != RecoveryPhase.Recovered)
                return;

            _matchWasRunningAtDisconnect = evt.MatchWasRunning;
            Debug.LogWarning("[Reconnect] Connection to host lost — starting reconnect grace.");

            if (evt.MatchWasRunning)
            {
                _saveManager.SaveNow();
                _stateMachine.TransitionTo(MatchPhase.Paused);
            }

            _migrationManager.BeginRecovery();
        }

        private void OnMigrationCompleted(HostMigrationCompletedEvent evt)
        {
            if (evt.Success)
            {
                ClearFreshJoinState();
                return;
            }

            // Seat-token reconnect failed while still in the room → leave + fresh join.
            // Fresh-join timeout also publishes failure; do not re-enter.
            if (_session.IsActive && !_freshJoining)
                BeginFreshJoinAfterLeave();
        }

        private void BeginFreshJoinAfterLeave()
        {
            _savedReconnectToken = _session.ReconnectToken;
            _freshJoinRoomCode = !string.IsNullOrEmpty(_migrationManager.TargetRoomCode)
                ? _migrationManager.TargetRoomCode
                : _session.RoomCode;

            Debug.LogWarning(
                $"[Reconnect] Seat reconnect failed — leaving room {_freshJoinRoomCode} then fresh-joining.");

            _migrationManager.CancelRecovery();
            _roomManager.LeaveRoom();

            if (string.IsNullOrEmpty(_freshJoinRoomCode))
            {
                FinishDissolved();
                return;
            }

            // Preserve token so mid-match auth can reclaim the seat on fresh join.
            _session.ReconnectToken = _savedReconnectToken;

            _freshJoining = true;
            _freshJoinElapsed = 0f;
            _discovery.StartSearching();
            _eventBus.Publish(new RecoveryStateChangedEvent
            {
                Phase = RecoveryPhase.Searching,
                ElapsedSeconds = 0f
            });
        }

        private void OnClientConnected(ClientConnectedEvent _)
        {
            if (!_freshJoining) return;

            Debug.Log("[Reconnect] Fresh join succeeded.");
            ClearFreshJoinState();
            _eventBus.Publish(new HostMigrationCompletedEvent { Success = true });
        }

        /// <summary>
        /// Player chose not to wait: finish the match right now from local
        /// state (scores settled exactly like a normal ending).
        /// </summary>
        public void EndMatchNow()
        {
            ClearFreshJoinState();
            _migrationManager.CancelRecovery();
            _gameManager.EndMatchManually();
        }

        #endregion

        #region Resume (host side)

        private void OnPlayerConnectionChanged(PlayerConnectionChangedEvent evt)
        {
            if (!NetworkServer.active) return;

            if (!evt.IsConnected)
            {
                // A seated player dropped mid-match: back up and pause until
                // they return (the host can ForceResume to play on without them).
                if (_stateMachine.CurrentPhase == MatchPhase.Playing)
                {
                    Debug.LogWarning($"[Reconnect] Player {evt.PlayerId} disconnected — pausing the match.");
                    _saveManager.SaveNow();
                    _stateMachine.TransitionTo(MatchPhase.Paused);
                }

                return;
            }

            TryAutoResume();
        }

        private void TryAutoResume()
        {
            if (!NetworkServer.active || _stateMachine.CurrentPhase != MatchPhase.Paused)
                return;

            foreach (PlayerState player in _playerManager.Players)
            {
                if (!player.IsConnected)
                    return; // still waiting for someone
            }

            Debug.Log("[Reconnect] All players are back — resuming.");
            _stateMachine.TransitionTo(MatchPhase.Playing);
        }

        /// <summary>
        /// Host override: resume even though some players are still missing
        /// (their turns will time out into passes until they return).
        /// </summary>
        public void ForceResume()
        {
            if (NetworkServer.active && _stateMachine.CurrentPhase == MatchPhase.Paused)
                _stateMachine.TransitionTo(MatchPhase.Playing);
        }

        #endregion

        #region ITickable

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            TickHostNetworkWatch();
            TickFreshJoin(deltaTime);
        }

        private void TickHostNetworkWatch()
        {
            if (!NetworkServer.active || !_session.IsActive || !_session.IsHost)
                return;

            if (!_discovery.IsAdvertising)
                return;

            if (_discovery.ConsecutiveBroadcastFailures < HostBroadcastFailureLimit)
                return;

            Debug.LogError(
                $"[Reconnect] Host network lost ({_discovery.ConsecutiveBroadcastFailures} broadcast failures) — dissolving room.");
            // LeaveRoom stops advertising + StopHost → HostStoppedEvent returns the
            // host UI to the menu. Clients see a normal disconnect and run recovery.
            _roomManager.LeaveRoom();
            if (_gameManager.Config != null && _gameManager.Phase != MatchPhase.Lobby)
                _gameManager.AbandonSession();
        }

        private void TickFreshJoin(float deltaTime)
        {
            if (!_freshJoining) return;

            _freshJoinElapsed += deltaTime;

            if (_discovery.TryResolveRoomCode(_freshJoinRoomCode, out RoomInfo room)
                && !NetworkClient.active
                && !NetworkServer.active)
            {
                _session.ReconnectToken = _savedReconnectToken;
                if (_roomManager.JoinRoom(room))
                {
                    // Stay in fresh-joining until ClientConnected (or disconnect).
                    _eventBus.Publish(new RecoveryStateChangedEvent
                    {
                        Phase = RecoveryPhase.Reconnecting,
                        ElapsedSeconds = _freshJoinElapsed
                    });
                    return;
                }
            }

            if (_freshJoinElapsed >= FreshJoinSeconds)
            {
                Debug.LogWarning("[Reconnect] Fresh join timed out — room dissolved from this client.");
                FinishDissolved();
            }
            else if (Mathf.FloorToInt(_freshJoinElapsed) != Mathf.FloorToInt(_freshJoinElapsed - deltaTime))
            {
                _eventBus.Publish(new RecoveryStateChangedEvent
                {
                    Phase = RecoveryPhase.Searching,
                    ElapsedSeconds = _freshJoinElapsed
                });
            }
        }

        private void FinishDissolved()
        {
            ClearFreshJoinState();
            _discovery.StopSearching();
            if (_session.IsActive)
                _roomManager.LeaveRoom();

            if (_matchWasRunningAtDisconnect
                || (_gameManager.Config != null && _gameManager.Phase != MatchPhase.Lobby))
                _gameManager.AbandonSession();

            _eventBus.Publish(new RoomDissolvedEvent
            {
                Reason = _matchWasRunningAtDisconnect
                    ? RoomDissolveReason.ReconnectFailed
                    : RoomDissolveReason.LobbyDisconnect
            });
            _eventBus.Publish(new HostMigrationCompletedEvent { Success = false });
        }

        private void ClearFreshJoinState()
        {
            _freshJoining = false;
            _freshJoinElapsed = 0f;
            _freshJoinRoomCode = null;
            _savedReconnectToken = null;
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            _eventBus.Unsubscribe<ClientDisconnectedEvent>(OnClientDisconnected);
            _eventBus.Unsubscribe<PlayerConnectionChangedEvent>(OnPlayerConnectionChanged);
            _eventBus.Unsubscribe<HostMigrationCompletedEvent>(OnMigrationCompleted);
            _eventBus.Unsubscribe<ClientConnectedEvent>(OnClientConnected);
        }

        #endregion
    }
}
