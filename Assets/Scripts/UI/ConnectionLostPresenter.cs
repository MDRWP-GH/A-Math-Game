using System;
using AMath.Networking;
using AMath.Networking.HostMigration;
using UnityEngine;

namespace AMath.UI
{
    /// <summary>
    /// Presenter for the "connection lost" overlay shown while the recovery
    /// pipeline runs. The player has exactly the two choices required by the
    /// design: keep waiting for the host (recovery keeps searching forever) or
    /// end the match now from the local backup save.
    /// </summary>
    public sealed class ConnectionLostPresenter : MonoBehaviour
    {
        #region View-facing events

        /// <summary>Show/refresh the overlay: (phase, user-readable status, elapsed seconds).</summary>
        public event Action<RecoveryPhase, string, float> StatusChanged;

        /// <summary>Recovery finished (hide the overlay on success).</summary>
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
        }

        private void OnDestroy()
        {
            _eventBus?.Unsubscribe<RecoveryStateChangedEvent>(OnRecoveryStateChanged);
            _eventBus?.Unsubscribe<HostMigrationCompletedEvent>(OnMigrationCompleted);
        }

        #endregion

        #region Event handlers

        private void OnRecoveryStateChanged(RecoveryStateChangedEvent evt)
        {
            StatusChanged?.Invoke(evt.Phase, DescribePhase(evt.Phase), evt.ElapsedSeconds);
        }

        private void OnMigrationCompleted(HostMigrationCompletedEvent evt)
        {
            RecoveryFinished?.Invoke(evt.Success);
        }

        private static string DescribePhase(RecoveryPhase phase) => phase switch
        {
            RecoveryPhase.GraceWait => "Connection lost — waiting for the host...",
            RecoveryPhase.Searching => "Searching for the room on this network...",
            RecoveryPhase.Promoting => "Taking over as the new host...",
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
