using AMath.Core.Events;
using AMath.Core.StateMachines;
using AMath.Gameplay.Players;
using AMath.Managers;
using AMath.Networking.Room;
using AMath.Save;
using Mirror;
using UnityEngine;

namespace AMath.Networking.HostMigration
{
    /// <summary>
    /// Orchestrates the "network died mid-match" experience required by the
    /// design:
    ///  - the moment the connection drops: write a backup save and pause;
    ///  - offer the player two choices: keep waiting for the host to return
    ///    (indefinitely — recovery keeps scanning) or end the match now from
    ///    the local backup;
    ///  - on the (new or original) host: pause while seats are empty and
    ///    resume automatically once every seated player has reconnected.
    /// Delegates the election/rediscovery mechanics to
    /// <see cref="HostMigrationManager"/>.
    /// </summary>
    public sealed class ReconnectionManager : System.IDisposable
    {
        #region Fields

        private readonly IEventBus _eventBus;
        private readonly GameStateMachine _stateMachine;
        private readonly GameManager _gameManager;
        private readonly PlayerManager _playerManager;
        private readonly SaveManager _saveManager;
        private readonly HostMigrationManager _migrationManager;
        private readonly RoomSession _session;

        #endregion

        #region Construction

        public ReconnectionManager(
            IEventBus eventBus,
            GameStateMachine stateMachine,
            GameManager gameManager,
            PlayerManager playerManager,
            SaveManager saveManager,
            HostMigrationManager migrationManager,
            RoomSession session)
        {
            _eventBus = eventBus;
            _stateMachine = stateMachine;
            _gameManager = gameManager;
            _playerManager = playerManager;
            _saveManager = saveManager;
            _migrationManager = migrationManager;
            _session = session;

            _eventBus.Subscribe<ClientDisconnectedEvent>(OnClientDisconnected);
            _eventBus.Subscribe<PlayerConnectionChangedEvent>(OnPlayerConnectionChanged);
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

            if (!evt.MatchWasRunning)
            {
                // Lobby-time disconnect (or rejection): nothing to recover.
                _session.IsActive = false;
                return;
            }

            if (_migrationManager.Phase == RecoveryPhase.Reconnecting)
            {
                // A reconnect attempt failed; resume scanning for the next candidate.
                _migrationManager.OnReconnectFailed();
                return;
            }

            Debug.LogWarning("[Reconnect] Connection to host lost — backing up and pausing.");

            // 1) Backup immediately, before anything else can go wrong.
            _saveManager.SaveNow();

            // 2) Freeze the local game.
            _stateMachine.TransitionTo(MatchPhase.Paused);

            // 3) Start waiting / election. The player may end the match at any time.
            _migrationManager.BeginRecovery();
        }

        /// <summary>
        /// Player chose not to wait: finish the match right now from local
        /// state (scores settled exactly like a normal ending).
        /// </summary>
        public void EndMatchNow()
        {
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

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            _eventBus.Unsubscribe<ClientDisconnectedEvent>(OnClientDisconnected);
            _eventBus.Unsubscribe<PlayerConnectionChangedEvent>(OnPlayerConnectionChanged);
        }

        #endregion
    }
}
