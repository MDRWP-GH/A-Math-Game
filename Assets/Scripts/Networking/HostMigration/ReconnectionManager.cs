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
    ///  - client loses host: pause/backup (mid-match), retry for a grace window,
    ///    then leave and try a fresh join if the room is still advertised;
    ///  - host loses network: dissolve the waiting room and play room;
    ///  - host pauses while seats are empty, and resumes either when all
    ///    return or once the wait window expires.
    /// </summary>
    public sealed class ReconnectionManager : ITickable, System.IDisposable
    {
        #region Constants

        /// <summary>Seconds to search for the room after leave before giving up.</summary>
        private const float FreshJoinSeconds = 5f;

        /// <summary>Extra fresh-join windows granted when an attempt drops mid-connect.</summary>
        private const int MaxFreshJoinRetries = 2;

        /// <summary>
        /// Hard ceiling on a whole recovery cycle. The blocking "connection
        /// lost" overlay is visible for its entire duration, so no combination
        /// of grace, retry and fresh-join windows may outlive this.
        /// </summary>
        private const float MaxRecoverySeconds = 30f;

        /// <summary>
        /// Consecutive discovery broadcast failures (~1 Hz) before the host
        /// dissolves the room as network-lost.
        /// </summary>
        private const int HostBroadcastFailureLimit = 3;

        /// <summary>
        /// Seconds the host holds a match paused for missing seats before
        /// playing on without them. A player who never returns must not be
        /// able to freeze the table for everyone still at it.
        /// </summary>
        private const float HostPauseSeconds = 45f;

        #endregion

        #region Fields

        private readonly IEventBus _eventBus;
        private readonly GameStateMachine _stateMachine;
        private readonly GameManager _gameManager;
        private readonly PlayerManager _playerManager;
        private readonly SaveManager _saveManager;
        private readonly HostReconnectManager _migrationManager;
        private readonly RoomManager _roomManager;
        private readonly DiscoveryManager _discovery;
        private readonly RoomSession _session;

        private bool _freshJoining;
        private float _freshJoinElapsed;
        private int _freshJoinRetries;
        private string _freshJoinRoomCode;
        private string _savedReconnectToken;
        private bool _matchWasRunningAtDisconnect;
        private string _rejectionReason;
        private bool _connectedSinceJoin;
        private float _recoveryElapsed;
        private float _hostPauseElapsed;

        #endregion

        #region Properties

        /// <summary>
        /// True when a match (not a lobby) was running at the moment the
        /// connection dropped. The UI needs this to decide whether ending the
        /// session should show match results at all.
        /// </summary>
        public bool MatchWasRunning => _matchWasRunningAtDisconnect;

        /// <summary>
        /// Seconds left before the host plays on without the missing players,
        /// or zero when no match is waiting. The HUD shows this so a pause
        /// never looks like a hang.
        /// </summary>
        public float HostPauseSecondsRemaining =>
            IsHostWaitingForPlayers ? Mathf.Max(0f, HostPauseSeconds - _hostPauseElapsed) : 0f;

        /// <summary>True while the host holds a real match paused for absent seats.</summary>
        public bool IsHostWaitingForPlayers =>
            NetworkServer.active
            && _stateMachine.CurrentPhase == MatchPhase.Paused
            && _gameManager.Config != null;

        #endregion

        #region Construction

        public ReconnectionManager(
            IEventBus eventBus,
            GameStateMachine stateMachine,
            GameManager gameManager,
            PlayerManager playerManager,
            SaveManager saveManager,
            HostReconnectManager migrationManager,
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
            _eventBus.Subscribe<ConnectionRejectedEvent>(OnConnectionRejected);
        }

        #endregion

        #region Connection loss (client side)

        private void OnConnectionRejected(ConnectionRejectedEvent evt)
        {
            // Consumed by the disconnect that always follows a rejection.
            _rejectionReason = string.IsNullOrEmpty(evt.Reason) ? "Rejected by host." : evt.Reason;
        }

        private void OnClientDisconnected(ClientDisconnectedEvent evt)
        {
            // Both flags describe the attempt that just ended, so they are
            // consumed here unconditionally — leaving either set would let a
            // stale verdict decide the next disconnect.
            string rejection = _rejectionReason;
            bool wasConnected = _connectedSinceJoin;
            _rejectionReason = null;
            _connectedSinceJoin = false;

            // The host's own local client also raises this when hosting stops;
            // recovery is only for clients that lost their server.
            if (NetworkServer.active) return;

            // Intentional leave (RoomManager.LeaveRoom clears the flag before
            // stopping) — nothing to recover.
            if (!_session.IsActive) return;

            // A refusal at the door is deterministic: retrying produces the same
            // verdict, so end the session immediately instead of parking the
            // player behind the blocking recovery overlay.
            if (rejection != null)
            {
                Debug.LogWarning($"[Reconnect] Host refused the connection ({rejection}); not recovering.");
                _migrationManager.CancelRecovery();
                FinishDissolved();
                return;
            }

            // Fresh-join attempt dropped mid-connect; grant another window
            // instead of silently waiting out the current one.
            if (_freshJoining)
            {
                if (_freshJoinRetries < MaxFreshJoinRetries)
                {
                    _freshJoinRetries++;
                    _freshJoinElapsed = 0f;
                    Debug.LogWarning($"[Reconnect] Fresh join dropped; retry {_freshJoinRetries}.");
                }

                return;
            }

            if (_migrationManager.Phase == RecoveryPhase.Reconnecting)
            {
                _migrationManager.OnReconnectFailed();
                return;
            }

            // Already in a reconnect cycle (grace/search) — ignore duplicate events.
            if (_migrationManager.Phase != RecoveryPhase.Idle
                && _migrationManager.Phase != RecoveryPhase.Recovered)
                return;

            // The connection never completed, so there is no session to restore
            // — this is a failed join (unreachable host, timeout), not a loss.
            if (!wasConnected)
            {
                Debug.LogWarning("[Reconnect] Join never completed; returning to the room browser.");
                FinishDissolved();
                return;
            }

            // A lobby holds no state to restore, and migration re-hosts from a
            // match snapshot that does not exist yet, so the room code will
            // never come back. Waiting would only stall the player.
            if (!evt.MatchWasRunning)
            {
                _matchWasRunningAtDisconnect = false;
                Debug.LogWarning("[Reconnect] Lost the host while in the lobby — dissolving the room.");
                FinishDissolved();
                return;
            }

            _matchWasRunningAtDisconnect = true;
            _recoveryElapsed = 0f;
            Debug.LogWarning("[Reconnect] Connection to host lost — starting reconnect grace.");

            _saveManager.SaveNow();
            _stateMachine.TransitionTo(MatchPhase.Paused);
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
            _connectedSinceJoin = true;
            _rejectionReason = null;

            if (!_freshJoining) return;

            Debug.Log("[Reconnect] Fresh join succeeded.");
            ClearFreshJoinState();
            _eventBus.Publish(new HostMigrationCompletedEvent { Success = true });
        }

        /// <summary>
        /// Player chose not to wait: finish the match right now from local
        /// state (scores settled exactly like a normal ending). In the lobby
        /// there is nothing to settle, so this only tears the session down.
        /// </summary>
        public void EndMatchNow()
        {
            AbortRecovery();
            _gameManager.EndMatchManually();
        }

        /// <summary>
        /// Stops every recovery activity and guarantees a terminal recovery
        /// state reaches the UI. <see cref="HostReconnectManager.CancelRecovery"/>
        /// stays silent when it is already idle (which it is during fresh-join),
        /// so the blocking overlay would otherwise never be told to close.
        /// </summary>
        public void AbortRecovery()
        {
            ClearFreshJoinState();
            _discovery.StopSearching();
            _migrationManager.CancelRecovery();
            _recoveryElapsed = 0f;
            _eventBus.Publish(new RecoveryStateChangedEvent
            {
                Phase = RecoveryPhase.Idle,
                ElapsedSeconds = 0f
            });
        }

        /// <summary>Player gave up waiting and wants out of the room entirely.</summary>
        public void LeaveRoomNow()
        {
            AbortRecovery();

            if (_session.IsActive)
                _roomManager.LeaveRoom();

            if (_gameManager.Config != null && _gameManager.Phase != MatchPhase.Lobby)
                _gameManager.AbandonSession();

            _matchWasRunningAtDisconnect = false;
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
            TickHostPauseDeadline(deltaTime);
            TickRecoveryDeadline(deltaTime);
            TickFreshJoin(deltaTime);
        }

        /// <summary>
        /// Backstop for the host-side pause. The turn timer only runs while
        /// Playing, so a seat that never comes back would otherwise hold the
        /// match forever. After the window the match plays on and the absent
        /// players' turns time out into passes, which the match can end on.
        /// </summary>
        private void TickHostPauseDeadline(float deltaTime)
        {
            if (!IsHostWaitingForPlayers)
            {
                _hostPauseElapsed = 0f;
                return;
            }

            _hostPauseElapsed += deltaTime;
            if (_hostPauseElapsed < HostPauseSeconds)
                return;

            _hostPauseElapsed = 0f;
            Debug.LogWarning(
                $"[Reconnect] Missing players did not return within {HostPauseSeconds:0}s — resuming without them.");
            ForceResume();
        }

        /// <summary>
        /// Backstop for the blocking recovery overlay: however the grace,
        /// retry and fresh-join windows chain together, recovery ends here.
        /// </summary>
        private void TickRecoveryDeadline(float deltaTime)
        {
            bool recovering = _freshJoining
                || (_migrationManager.Phase != RecoveryPhase.Idle
                    && _migrationManager.Phase != RecoveryPhase.Recovered);

            if (!recovering)
            {
                _recoveryElapsed = 0f;
                return;
            }

            _recoveryElapsed += deltaTime;
            if (_recoveryElapsed < MaxRecoverySeconds)
                return;

            Debug.LogWarning(
                $"[Reconnect] Recovery exceeded {MaxRecoverySeconds:0}s — dissolving the room.");
            _migrationManager.CancelRecovery();
            FinishDissolved();
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
            _recoveryElapsed = 0f;
            if (_session.IsActive)
                _roomManager.LeaveRoom();

            if (_matchWasRunningAtDisconnect
                || (_gameManager.Config != null && _gameManager.Phase != MatchPhase.Lobby))
                _gameManager.AbandonSession();

            bool matchWasRunning = _matchWasRunningAtDisconnect;
            _matchWasRunningAtDisconnect = false;

            _eventBus.Publish(new RoomDissolvedEvent
            {
                Reason = matchWasRunning
                    ? RoomDissolveReason.ReconnectFailed
                    : RoomDissolveReason.LobbyDisconnect
            });
            _eventBus.Publish(new HostMigrationCompletedEvent { Success = false });
        }

        private void ClearFreshJoinState()
        {
            _freshJoining = false;
            _freshJoinElapsed = 0f;
            _freshJoinRetries = 0;
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
            _eventBus.Unsubscribe<ConnectionRejectedEvent>(OnConnectionRejected);
        }

        #endregion
    }
}
