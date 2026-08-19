using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Events;
using AMath.Gameplay.Players;
using AMath.Networking.Discovery;
using AMath.Networking.Room;
using AMath.Replay;
using AMath.Save;
using UnityEngine;

namespace AMath.Networking.HostMigration
{
    /// <summary>
    /// Client-side host-succession engine. Uses only local data (the cached
    /// succession table, the local autosave and LAN discovery), because when
    /// it runs, the network session is already gone.
    ///
    /// Timeline after the host vanishes:
    ///  1. Grace window — scan for the original host rebroadcasting the same
    ///     room code (a router hiccup must not trigger a false migration).
    ///  2. Election — my rank in the cached table decides my role:
    ///     rank 0 promotes immediately; rank N waits N * stagger seconds and
    ///     only promotes if no better candidate has appeared (fault-tolerant
    ///     chain: if #1 also died, #2 takes over, and so on).
    ///  3. Promotion loads the latest autosave and re-hosts under the SAME
    ///     room code; everyone else rediscovers that code and reconnects.
    /// There is no negotiation traffic at all — every survivor reaches the
    /// same conclusion from identical replicated data.
    /// </summary>
    public sealed class HostMigrationManager : ITickable
    {
        #region Constants

        /// <summary>Seconds to wait for the original host before electing a new one.</summary>
        public const float GraceSeconds = 8f;

        /// <summary>Extra wait per succession rank before self-promoting.</summary>
        public const float PromotionStaggerSeconds = 10f;

        /// <summary>Maximum automatic reconnect attempts to one recovered host.</summary>
        public const int MaxReconnectAttempts = 4;

        #endregion

        #region Fields

        private readonly IEventBus _eventBus;
        private readonly DiscoveryManager _discovery;
        private readonly RoomManager _roomManager;
        private readonly SaveManager _saveManager;
        private readonly ReplayManager _replayManager;
        private readonly PlayerManager _playerManager;
        private readonly RoomSession _session;

        private readonly List<MigrationCandidate> _cachedTable = new();

        private RecoveryPhase _phase = RecoveryPhase.Idle;
        private float _elapsed;
        private float _promotionDeadline;
        private float _nextReconnectAttempt;
        private float _nextHeartbeat;
        private string _targetRoomCode;
        private int _promotionRank;
        private bool _promotionRankResolved;
        private int _reconnectAttempts;

        #endregion

        #region Properties

        /// <summary>Current recovery phase (UI state).</summary>
        public RecoveryPhase Phase => _phase;

        /// <summary>Number of automatic reconnect attempts in this recovery cycle.</summary>
        public int ReconnectAttempts => _reconnectAttempts;

        #endregion

        #region Construction

        public HostMigrationManager(
            IEventBus eventBus,
            DiscoveryManager discovery,
            RoomManager roomManager,
            SaveManager saveManager,
            ReplayManager replayManager,
            PlayerManager playerManager,
            RoomSession session)
        {
            _eventBus = eventBus;
            _discovery = discovery;
            _roomManager = roomManager;
            _saveManager = saveManager;
            _replayManager = replayManager;
            _playerManager = playerManager;
            _session = session;

            _eventBus.Subscribe<ClientConnectedEvent>(OnClientConnected);
            _eventBus.Subscribe<HostStartedEvent>(OnHostStarted);
        }

        #endregion

        #region Table cache (fed by MigrationTableSync while connected)

        /// <summary>Replaces the cached succession table with the latest replicated copy.</summary>
        public void UpdateCachedTable(List<MigrationCandidate> table)
        {
            _cachedTable.Clear();
            _cachedTable.AddRange(table);
            MigrationRanking.Sort(_cachedTable);
        }

        #endregion

        #region Control

        /// <summary>Starts the recovery pipeline (called by ReconnectionManager on host loss).</summary>
        public void BeginRecovery()
        {
            if (_phase != RecoveryPhase.Idle && _phase != RecoveryPhase.Recovered) return;

            _targetRoomCode = _session.RoomCode;
            _elapsed = 0f;
            _nextHeartbeat = 0f;
            _nextReconnectAttempt = 0f;
            _promotionRank = 0;
            _promotionRankResolved = false;
            _reconnectAttempts = 0;
            SetPhase(RecoveryPhase.GraceWait);
            _discovery.StartSearching();
            Debug.Log($"[Migration] Recovery started for room {_targetRoomCode}.");
        }

        /// <summary>Aborts recovery (player chose to end the match from the local save).</summary>
        public void CancelRecovery()
        {
            if (_phase == RecoveryPhase.Idle) return;
            _discovery.StopSearching();
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

            // Any broadcast with our room code — original host or an earlier
            // candidate that already took over — is a reconnect target.
            if (_discovery.TryResolveRoomCode(_targetRoomCode, out RoomInfo room)
                && _reconnectAttempts < MaxReconnectAttempts
                && _elapsed >= _nextReconnectAttempt)
            {
                Reconnect(room);
                return;
            }

            if (_phase == RecoveryPhase.GraceWait && _elapsed >= GraceSeconds)
            {
                ResolvePromotionRankIfNeeded();
                if (_promotionRank == 0)
                {
                    Promote();
                }
                else
                {
                    _promotionDeadline = CalculatePromotionDeadline(_promotionRank);
                    SetPhase(RecoveryPhase.Searching);
                }
            }
            else if (_phase == RecoveryPhase.Searching && _elapsed >= _promotionDeadline)
            {
                // Better-ranked candidates never appeared; it is my turn.
                Promote();
            }

            // 1 Hz UI heartbeat (elapsed time display).
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
            Debug.Log($"[Migration] Room {_targetRoomCode} rediscovered at {room.HostAddress}; reconnecting.");

            // A synchronous failure produces no disconnect event, so fall back
            // to scanning immediately instead of waiting forever.
            if (!_roomManager.JoinRoom(room))
                OnReconnectFailed();
        }

        private void Promote()
        {
            SetPhase(RecoveryPhase.Promoting);
            _discovery.StopSearching();

            if (!_saveManager.TryLoadForRoom(_targetRoomCode, out SaveFile save, out string error))
            {
                Debug.LogError($"[Migration] Cannot promote — autosave unavailable: {error}");
                // Keep waiting for someone else; the player can still end the match.
                SetPhase(RecoveryPhase.Searching);
                _promotionDeadline = float.MaxValue;
                _discovery.StartSearching();
                return;
            }

            _eventBus.Publish(new HostMigrationStartedEvent
            {
                IAmNewHost = true,
                NewHostPlayerId = _playerManager.LocalPlayerId
            });

            _roomManager.RehostFromSnapshot(save.State, save.RoomCode, save.RoomName, save.MaxPlayers);
            // Restore the replay AFTER the snapshot: RestoreSnapshot publishes
            // MatchStartedEvent, which resets the live replay log.
            _replayManager.Restore(save.Replay);
            // HostStartedEvent completes the flow.
        }

        private void OnHostStarted(HostStartedEvent _)
        {
            if (_phase != RecoveryPhase.Promoting) return;
            SetPhase(RecoveryPhase.Recovered);
            _eventBus.Publish(new HostMigrationCompletedEvent { Success = true });
        }

        private void OnClientConnected(ClientConnectedEvent _)
        {
            if (_phase != RecoveryPhase.Reconnecting) return;
            SetPhase(RecoveryPhase.Recovered);
            _eventBus.Publish(new HostMigrationCompletedEvent { Success = true });
        }

        /// <summary>Reconnect attempt failed; fall back to scanning for the next candidate.</summary>
        public void OnReconnectFailed()
        {
            if (_phase != RecoveryPhase.Reconnecting) return;

            _reconnectAttempts++;
            ResolvePromotionRankIfNeeded();
            if (_reconnectAttempts >= MaxReconnectAttempts)
            {
                Debug.LogWarning(
                    $"[Migration] Reconnect failed {_reconnectAttempts} times; promoting only when this client is elected.");
                _promotionDeadline = Mathf.Max(
                    _promotionDeadline,
                    CalculatePromotionDeadline(_promotionRank));
                SetPhase(RecoveryPhase.Searching);
                return;
            }

            _discovery.StartSearching();
            float backoff = Mathf.Min(8f, 1 << (_reconnectAttempts - 1));
            _promotionDeadline = Mathf.Max(
                _elapsed + backoff,
                CalculatePromotionDeadline(_promotionRank));
            _nextReconnectAttempt = _elapsed + backoff;
            Debug.LogWarning(
                $"[Migration] Reconnect attempt {_reconnectAttempts} failed; retrying discovery in {backoff:0.#}s.");
            SetPhase(RecoveryPhase.Searching);
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

        /// <summary>
        /// When the succession table is empty every survivor used to promote
        /// together; fall back to deterministic seat order instead.
        /// </summary>
        private int ResolvePromotionRank()
        {
            int rank = MigrationRanking.RankOf(_cachedTable, _playerManager.LocalPlayerId);
            if (rank >= 0)
                return rank;

            if (_cachedTable.Count == 0)
            {
                int localId = _playerManager.LocalPlayerId;
                return localId >= 0 ? localId : 0;
            }

            return _cachedTable.Count;
        }

        private void ResolvePromotionRankIfNeeded()
        {
            if (_promotionRankResolved)
            {
                return;
            }

            _promotionRank = ResolvePromotionRank();
            _promotionRankResolved = true;
        }

        private static float CalculatePromotionDeadline(int promotionRank) =>
            GraceSeconds + promotionRank * PromotionStaggerSeconds;

        #endregion
    }
}
