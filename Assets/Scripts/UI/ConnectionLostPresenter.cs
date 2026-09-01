using System;
using AMath.Networking;
using AMath.Networking.HostMigration;
using UnityEngine;

namespace AMath.UI
{
    /// <summary>
    /// Presenter for the "connection lost" overlay shown while reconnect /
    /// fresh-join runs. Reconnecting is automatic; the player may leave the
    /// room, or — mid-match only — end it now from the local backup save.
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

        #region Properties

        /// <summary>
        /// True when a real match was running when the link dropped. Without a
        /// match there are no results to show, so the view must offer "leave"
        /// rather than "end the match".
        /// </summary>
        public bool MatchWasRunning => _reconnection is { MatchWasRunning: true };

        #endregion

        #region Lifecycle

        private void Start()
        {
            // This component can be created before the composition root has
            // registered anything, so resolving is attempted rather than assumed.
            if (NetworkContext.Services == null)
            {
                Debug.LogWarning("[UI] Connection-lost presenter started before services existed.");
                return;
            }

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
        public void EndMatchNow() => _reconnection?.EndMatchNow();

        /// <summary>Player gave up on the room and wants to go back to the browser.</summary>
        public void LeaveRoom() => _reconnection?.LeaveRoomNow();

        #endregion
    }
}
