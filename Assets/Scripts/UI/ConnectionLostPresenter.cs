using System;
using AMath.Networking;
using AMath.Networking.HostMigration;
using UnityEngine;

namespace AMath.UI
{
    /// <summary>
    /// Presenter for the "connection lost" overlay shown while reconnect /
    /// fresh-join runs. The player may keep waiting (automatic) or end the
    /// match now from the local backup save.
    /// </summary>
    public sealed class ConnectionLostPresenter : MonoBehaviour
    {
        #region View-facing events

        /// <summary>Show/refresh the overlay: (phase, user-readable status, elapsed seconds).</summary>
        public event Action<RecoveryPhase, string, float> StatusChanged;

        /// <summary>Recovery finished (hide the overlay on success; leave room on failure).</summary>
        public event Action<bool> RecoveryFinished;

        #endregion

        #region Fields

        private Core.Events.IEventBus _eventBus;
        private ReconnectionManager _reconnection;

        #endregion

        #region Lifecycle

        private void Start()
        {
            _eventBus = NetworkContext.Services.Resolve<Core.Events.IEventBus>();
            _reconnection = NetworkContext.Services.Resolve<ReconnectionManager>();

            _eventBus.Subscribe<RecoveryStateChangedEvent>(OnRecoveryStateChanged);
            _eventBus.Subscribe<HostMigrationCompletedEvent>(OnMigrationCompleted);
            _eventBus.Subscribe<RoomDissolvedEvent>(OnRoomDissolved);
        }

        private void OnDestroy()
        {
            _eventBus?.Unsubscribe<RecoveryStateChangedEvent>(OnRecoveryStateChanged);
            _eventBus?.Unsubscribe<HostMigrationCompletedEvent>(OnMigrationCompleted);
            _eventBus?.Unsubscribe<RoomDissolvedEvent>(OnRoomDissolved);
        }

        #endregion

        #region Event handlers

        private void OnRecoveryStateChanged(RecoveryStateChangedEvent evt)
        {
            StatusChanged?.Invoke(evt.Phase, DescribePhase(evt.Phase), evt.ElapsedSeconds);
        }

        private void OnMigrationCompleted(HostMigrationCompletedEvent evt)
        {
            // Success = reconnected. Failure here only means seat-reconnect
            // failed and a fresh join may still be in progress — wait for
            // RoomDissolvedEvent before treating the session as gone.
            if (evt.Success)
                RecoveryFinished?.Invoke(true);
        }

        private void OnRoomDissolved(RoomDissolvedEvent _)
        {
            RecoveryFinished?.Invoke(false);
        }

        private static string DescribePhase(RecoveryPhase phase) => phase switch
        {
            RecoveryPhase.GraceWait => "Connection lost — reconnecting...",
            RecoveryPhase.Searching => "Searching for the room on this network...",
            RecoveryPhase.Reconnecting => "Room found — reconnecting...",
            RecoveryPhase.Recovered => "Reconnected!",
            _ => string.Empty
        };

        #endregion

        #region View commands

        /// <summary>Player chose to stop waiting and finish the match from the local backup.</summary>
        public void EndMatchNow() => _reconnection.EndMatchNow();

        #endregion
    }
}
