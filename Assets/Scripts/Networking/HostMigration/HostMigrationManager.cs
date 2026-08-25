using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Events;
using AMath.Networking.Discovery;
using AMath.Networking.Room;
using UnityEngine;

namespace AMath.Networking.HostMigration
{
    /// <summary>
    /// Client-side reconnect pipeline (Minecraft-style): after the host link
    /// drops, scan LAN for the same room code for a short grace window and try
    /// to reconnect. There is no host promotion — if reconnect fails, recovery
    /// ends so the caller can leave and attempt a fresh join.
    /// </summary>
    public sealed class HostMigrationManager : ITickable, System.IDisposable
    {
        #region Constants

        /// <summary>Seconds to wait / retry reconnect before giving up.</summary>
        public const float GraceSeconds = 5f;

        /// <summary>Maximum automatic seat-token reconnect attempts in one recovery cycle.</summary>
        public const int MaxReconnectAttempts = 1;

        #endregion

        #region Fields

        private readonly IEventBus _eventBus;
        private readonly DiscoveryManager _discovery;
        private readonly RoomManager _roomManager;
        private readonly RoomSession _session;

        private RecoveryPhase _phase = RecoveryPhase.Idle;
        private float _elapsed;
        private float _nextReconnectAttempt;
        private float _nextHeartbeat;
        private string _targetRoomCode;
        private int _reconnectAttempts;

        #endregion

        #region Properties

        /// <summary>Current recovery phase (UI state).</summary>
        public RecoveryPhase Phase => _phase;

        /// <summary>Number of automatic reconnect attempts in this recovery cycle.</summary>
        public int ReconnectAttempts => _reconnectAttempts;

        /// <summary>Room code being recovered, if any.</summary>
        public string TargetRoomCode => _targetRoomCode;

        #endregion

        #region Construction

        public HostMigrationManager(
            IEventBus eventBus,
            DiscoveryManager discovery,
            RoomManager roomManager,
            RoomSession session)
        {
            _eventBus = eventBus;
            _discovery = discovery;
            _roomManager = roomManager;
            _session = session;

            _eventBus.Subscribe<ClientConnectedEvent>(OnClientConnected);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _eventBus.Unsubscribe<ClientConnectedEvent>(OnClientConnected);
        }

        #endregion

        #region Table cache (legacy; promotion disabled)

        /// <summary>
        /// Accepts the replicated succession table for compatibility with
        /// <see cref="MigrationTableSync"/>. Host promotion is disabled, so
        /// the table is not used during reconnect.
        /// </summary>
        public void UpdateCachedTable(List<MigrationCandidate> table)
        {
            // Intentionally unused — Minecraft-style reconnect does not promote.
        }

        #endregion

        #region Control

        /// <summary>Starts the reconnect pipeline (called by ReconnectionManager on disconnect).</summary>
        public void BeginRecovery()
        {
            if (_phase != RecoveryPhase.Idle && _phase != RecoveryPhase.Recovered) return;

            _targetRoomCode = _session.RoomCode;
            _elapsed = 0f;
            _nextHeartbeat = 0f;
            _nextReconnectAttempt = 0f;
            _reconnectAttempts = 0;
            SetPhase(RecoveryPhase.GraceWait);
            _discovery.StartSearching();
            Debug.Log($"[Reconnect] Recovery started for room {_targetRoomCode} (grace {GraceSeconds:0.#}s).");
        }

        /// <summary>Aborts recovery (player chose to end the match from the local save).</summary>
        public void CancelRecovery()
        {
            if (_phase == RecoveryPhase.Idle) return;
            _discovery.StopSearching();
            _targetRoomCode = null;
            SetPhase(RecoveryPhase.Idle);
        }

        #endregion

        #region ITickable

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (_phase != RecoveryPhase.GraceWait && _phase != RecoveryPhase.Searching)
                return;

            _elapsed += deltaTime;

            if (_discovery.TryResolveRoomCode(_targetRoomCode, out RoomInfo room)
                && _reconnectAttempts < MaxReconnectAttempts
                && _elapsed >= _nextReconnectAttempt)
            {
                Reconnect(room);
                return;
            }

            if (_elapsed >= GraceSeconds)
            {
                FailRecovery("Grace window elapsed without a successful reconnect.");
                return;
            }

            if (_elapsed >= _nextHeartbeat)
            {
                _nextHeartbeat = _elapsed + 1f;
                _eventBus.Publish(new RecoveryStateChangedEvent { Phase = _phase, ElapsedSeconds = _elapsed });
            }
        }

        #endregion

        #region Transitions

        private void Reconnect(RoomInfo room)
        {
            SetPhase(RecoveryPhase.Reconnecting);
            _discovery.StopSearching();
            Debug.Log($"[Reconnect] Room {_targetRoomCode} rediscovered at {room.HostAddress}; reconnecting.");

            // A synchronous failure produces no disconnect event, so fail the
            // attempt immediately instead of waiting forever.
            if (!_roomManager.JoinRoom(room))
                OnReconnectFailed();
        }

        private void OnClientConnected(ClientConnectedEvent _)
        {
            if (_phase != RecoveryPhase.Reconnecting) return;
            SetPhase(RecoveryPhase.Recovered);
            _discovery.StopSearching();
            _eventBus.Publish(new HostMigrationCompletedEvent { Success = true });
        }

        /// <summary>Reconnect attempt failed; retry within grace or end recovery.</summary>
        public void OnReconnectFailed()
        {
            if (_phase != RecoveryPhase.Reconnecting) return;

            _reconnectAttempts++;
            if (_reconnectAttempts >= MaxReconnectAttempts || _elapsed >= GraceSeconds)
            {
                FailRecovery($"Reconnect failed after {_reconnectAttempts} attempt(s).");
                return;
            }

            _discovery.StartSearching();
            _nextReconnectAttempt = _elapsed + 1f;
            Debug.LogWarning(
                $"[Reconnect] Attempt {_reconnectAttempts} failed; retrying discovery in 1s.");
            SetPhase(RecoveryPhase.Searching);
        }

        private void FailRecovery(string reason)
        {
            Debug.LogWarning($"[Reconnect] Recovery failed: {reason}");
            _discovery.StopSearching();
            SetPhase(RecoveryPhase.Idle);
            _eventBus.Publish(new HostMigrationCompletedEvent { Success = false });
        }

        private void SetPhase(RecoveryPhase phase)
        {
            if (_phase == phase) return;
            _phase = phase;
            _eventBus.Publish(new RecoveryStateChangedEvent
            {
                Phase = phase,
                ElapsedSeconds = _elapsed,
                ReconnectAttempts = _reconnectAttempts
            });
        }

        #endregion
    }
}
