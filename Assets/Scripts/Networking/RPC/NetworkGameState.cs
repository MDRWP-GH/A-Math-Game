using System.Collections.Generic;
using System.Text;
using AMath.Core;
using AMath.Core.Events;
using AMath.Core.Snapshot;
using AMath.Core.StateMachines;
using AMath.Gameplay.Players;
using AMath.Managers;
using AMath.Replay;
using Mirror;
using UnityEngine;

namespace AMath.Networking.RPC
{
    /// <summary>
    /// The single replication hub for match state (scene object, host-owned).
    ///
    /// Synchronization strategy — only changed data, never the whole board:
    ///  - cheap scalars (phase, turn, deadline, bag count, seed) are SyncVars;
    ///  - scores are a SyncList updated once per resolved turn;
    ///  - the board is never streamed: each turn is broadcast once as a
    ///    ~50-byte <see cref="TurnRecord"/> RPC and every client re-executes it
    ///    through the same deterministic engine as the host;
    ///  - a full <see cref="GameStateSnapshot"/> is sent only to a single
    ///    connection on late-join/reconnect/desync (TargetRpc), never broadcast.
    ///
    /// The turn timer is replicated as an absolute NetworkTime deadline, so
    /// clients render a countdown without any per-frame timer traffic.
    /// </summary>
    public sealed class NetworkGameState : NetworkBehaviour
    {
        #region Types

        /// <summary>Replicated score entry (UI-facing).</summary>
        public struct ScoreEntry
        {
            public int PlayerId;
            public int Score;
        }

        /// <summary>Which half of a full-state transfer a chunk belongs to.</summary>
        private enum FullStatePart : byte
        {
            Snapshot = 0,
            Replay = 1
        }

        #endregion

        #region Constants

        /// <summary>
        /// Characters per full-state chunk. A mid-game snapshot plus its replay
        /// log runs to tens of kilobytes, which a single RPC cannot carry
        /// reliably across transports, so the transfer is split into pieces that
        /// comfortably fit one message.
        /// </summary>
        private const int FullStateChunkSize = 8_000;

        /// <summary>Minimum seconds between resync requests honoured per connection.</summary>
        private const double ResyncCooldownSeconds = 2d;

        #endregion

        #region SyncVars

        [SyncVar(hook = nameof(OnPhaseSynced))]
        private byte _phase = (byte)MatchPhase.Lobby;

        [SyncVar] private int _turnNumber;
        [SyncVar] private int _currentPlayerId = -1;
        [SyncVar] private double _turnDeadline;
        [SyncVar] private int _bagCount;
        [SyncVar] private int _randomSeed;

        private readonly SyncList<ScoreEntry> _scores = new();

        #endregion

        #region Fields

        private IEventBus _eventBus;
        private GameManager _gameManager;
        private GameStateMachine _stateMachine;
        private PlayerManager _playerManager;
        private ReplayManager _replayManager;
        private TurnManager _turnManager;

        private readonly Dictionary<int, double> _lastResyncPerConnection = new();
        private readonly StringBuilder _incomingSnapshot = new();
        private readonly StringBuilder _incomingReplay = new();
        private double _nextResyncRequestAt;

        #endregion

        #region Public read access (UI)

        /// <summary>Replicated phase.</summary>
        public MatchPhase Phase => (MatchPhase)_phase;

        /// <summary>Player whose turn it is.</summary>
        public int CurrentPlayerId => _currentPlayerId;

        /// <summary>Current turn number.</summary>
        public int TurnNumber => _turnNumber;

        /// <summary>Tiles left in the bag.</summary>
        public int BagCount => _bagCount;

        /// <summary>Seconds remaining in the current turn (derived from the replicated deadline).</summary>
        public float RemainingTurnSeconds => Mathf.Max(0f, (float)(_turnDeadline - NetworkTime.time));

        /// <summary>Replicated scores.</summary>
        public SyncList<ScoreEntry> Scores => _scores;

        #endregion

        #region Lifecycle

        public override void OnStartServer()
        {
            ResolveServices();
            if (NetworkContext.Services != null)
                NetworkContext.Services.RegisterOrReplace(this);

            _eventBus.Subscribe<MatchStartedEvent>(OnServerMatchStarted);
            _eventBus.Subscribe<MatchRestoredEvent>(OnServerMatchRestored);
            _eventBus.Subscribe<TurnStartedEvent>(OnServerTurnStarted);
            _eventBus.Subscribe<TurnResolvedEvent>(OnServerTurnResolved);
            _eventBus.Subscribe<MatchFinishedEvent>(OnServerMatchFinished);
            _eventBus.Subscribe<MatchPhaseChangedEvent>(OnServerPhaseChanged);
        }

        public override void OnStartClient()
        {
            ResolveServices();
            if (NetworkContext.Services != null)
                NetworkContext.Services.RegisterOrReplace(this);

            if (!isServer)
                _eventBus.Subscribe<DesyncDetectedEvent>(OnClientDesync);
        }

        public override void OnStopServer()
        {
            _lastResyncPerConnection.Clear();
            if (_eventBus == null) return;

            _eventBus.Unsubscribe<MatchStartedEvent>(OnServerMatchStarted);
            _eventBus.Unsubscribe<MatchRestoredEvent>(OnServerMatchRestored);
            _eventBus.Unsubscribe<TurnStartedEvent>(OnServerTurnStarted);
            _eventBus.Unsubscribe<TurnResolvedEvent>(OnServerTurnResolved);
            _eventBus.Unsubscribe<MatchFinishedEvent>(OnServerMatchFinished);
            _eventBus.Unsubscribe<MatchPhaseChangedEvent>(OnServerPhaseChanged);
        }

        public override void OnStopClient()
        {
            if (!isServer)
                _eventBus?.Unsubscribe<DesyncDetectedEvent>(OnClientDesync);
        }

        private void ResolveServices()
        {
            if (_eventBus != null || NetworkContext.Services == null) return;
            _eventBus = NetworkContext.Services.Resolve<IEventBus>();
            _gameManager = NetworkContext.Services.Resolve<GameManager>();
            _stateMachine = NetworkContext.Services.Resolve<GameStateMachine>();
            _playerManager = NetworkContext.Services.Resolve<PlayerManager>();
            _replayManager = NetworkContext.Services.Resolve<ReplayManager>();
            _turnManager = NetworkContext.Services.Resolve<TurnManager>();
        }

        #endregion

        #region Server: event bus -> replication

        private void OnServerMatchStarted(MatchStartedEvent evt)
        {
            _randomSeed = evt.Config.RandomSeed;
            _bagCount = _gameManager.BagCount;
            RebuildScores();
            RpcStartMatch(evt.Config);
        }

        /// <summary>
        /// A host that adopted a match from a snapshot still owes clients the
        /// replicated headers, but not <see cref="RpcStartMatch"/>: clients get
        /// restored state through <see cref="ServerSendFullStateTo"/>, and
        /// telling them to start a fresh match would discard it.
        /// </summary>
        private void OnServerMatchRestored(MatchRestoredEvent _)
        {
            ServerSeedFromGameState(_gameManager.CaptureSnapshot());
        }

        private void OnServerTurnStarted(TurnStartedEvent evt)
        {
            _turnNumber = evt.TurnNumber;
            _currentPlayerId = evt.PlayerId;
            _turnDeadline = NetworkTime.time + evt.TurnSeconds;
        }

        private void OnServerTurnResolved(TurnResolvedEvent evt)
        {
            if (!evt.IsAuthority) return;

            _bagCount = _gameManager.BagCount;

            // Match end adjusts every player's score (leftover tiles),
            // so replicate the whole table instead of a single delta.
            if (evt.Record.EndedMatch)
                RebuildScores();
            else
                UpdateScore(evt.Record.PlayerId);

            RpcApplyTurn(evt.Record);
        }

        private void OnServerMatchFinished(MatchFinishedEvent evt)
        {
            if (evt.Result == null)
                return;

            RebuildScores();
            // This RPC follows the terminal-turn RPC on the same reliable
            // channel. Manual endings have no turn RPC and arrive here directly.
            RpcFinalizeMatch(evt.Result);
        }

        private void OnServerPhaseChanged(MatchPhaseChangedEvent evt)
        {
            _phase = (byte)evt.Current;

            // Resuming after a pause (reconnection / host migration): refresh
            // the replicated turn data and give the current player a full clock.
            if (evt.Current == MatchPhase.Playing && evt.Previous == MatchPhase.Paused)
            {
                // Keep the enforced timeout in step with the replicated deadline.
                _turnManager.ResetClock();
                _turnNumber = _turnManager.TurnNumber;
                _currentPlayerId = _turnManager.CurrentPlayerId;
                _turnDeadline = NetworkTime.time + _turnManager.TurnSeconds;
                _bagCount = _gameManager.BagCount;
                RebuildScores();
            }
        }

        private void RebuildScores()
        {
            _scores.Clear();
            foreach (PlayerState player in _playerManager.Players)
                _scores.Add(new ScoreEntry { PlayerId = player.PlayerId, Score = player.Score });
        }

        private void UpdateScore(int playerId)
        {
            PlayerState player = _playerManager.GetById(playerId);
            for (int i = 0; i < _scores.Count; i++)
            {
                if (_scores[i].PlayerId == playerId)
                {
                    _scores[i] = new ScoreEntry { PlayerId = playerId, Score = player.Score };
                    return;
                }
            }
        }

        #endregion

        #region Client RPCs

        /// <summary>Starts the identical deterministic match on every client.</summary>
        [ClientRpc]
        private void RpcStartMatch(MatchConfig config)
        {
            if (isServer) return; // host already started locally
            _gameManager.StartMatch(config);
        }

        /// <summary>Delta update: one accepted turn, re-executed locally by each client.</summary>
        [ClientRpc]
        private void RpcApplyTurn(TurnRecord record)
        {
            if (isServer) return;
            _gameManager.ApplyReplicatedRecord(record);
        }

        /// <summary>Publishes the host-computed result on every non-host peer.</summary>
        [ClientRpc]
        private void RpcFinalizeMatch(MatchResult result)
        {
            if (isServer) return;
            _gameManager.ApplyAuthoritativeResult(result);
        }

        #endregion

        #region Resync (late join / reconnect / desync recovery)

        /// <summary>Server: pushes the complete state to one connection only.</summary>
        [Server]
        public void ServerSendFullStateTo(NetworkConnectionToClient conn)
        {
            GameStateSnapshot snapshot = _gameManager.CaptureSnapshot();
            ServerSeedFromGameState(snapshot);
            SendFullStatePart(conn, FullStatePart.Snapshot, JsonUtility.ToJson(snapshot));
            SendFullStatePart(conn, FullStatePart.Replay, _replayManager.ExportJson());
            TargetFullStateComplete(conn);
        }

        [Server]
        private void SendFullStatePart(NetworkConnectionToClient conn, FullStatePart part, string payload)
        {
            payload ??= string.Empty;

            // Always send at least one (possibly empty) chunk so the receiver
            // resets its buffer for this part even when there is nothing to send.
            int sequence = 0;
            int offset = 0;
            do
            {
                int length = Mathf.Min(FullStateChunkSize, payload.Length - offset);
                TargetFullStateChunk(conn, (byte)part, sequence, payload.Substring(offset, length));
                offset += length;
                sequence++;
            }
            while (offset < payload.Length);
        }

        /// <summary>
        /// Copies authoritative match scalars into SyncVars so reconnecting
        /// clients receive consistent lobby/match headers alongside the snapshot.
        /// </summary>
        [Server]
        private void ServerSeedFromGameState(GameStateSnapshot snapshot)
        {
            if (_gameManager.Config == null)
                return;

            _phase = snapshot.Phase;
            _turnNumber = snapshot.TurnNumber;
            _currentPlayerId = snapshot.CurrentPlayerId;
            _randomSeed = snapshot.Config.RandomSeed;
            _bagCount = _gameManager.BagCount;

            if (_gameManager.Phase == MatchPhase.Playing && _turnManager != null)
                _turnDeadline = NetworkTime.time + _turnManager.RemainingSeconds;
            else
                _turnDeadline = 0d;

            RebuildScores();
        }

        [TargetRpc]
        private void TargetFullStateChunk(NetworkConnectionToClient _, byte part, int sequence, string chunk)
        {
            if (isServer) return;

            StringBuilder buffer = (FullStatePart)part == FullStatePart.Snapshot
                ? _incomingSnapshot
                : _incomingReplay;

            // Sequence 0 starts a part, so an abandoned transfer cannot leave
            // fragments glued to the front of the next one.
            if (sequence == 0)
                buffer.Clear();

            buffer.Append(chunk);
        }

        [TargetRpc]
        private void TargetFullStateComplete(NetworkConnectionToClient _)
        {
            if (isServer) return;

            GameStateSnapshot snapshot;
            try
            {
                snapshot = JsonUtility.FromJson<GameStateSnapshot>(_incomingSnapshot.ToString());
            }
            catch (System.ArgumentException ex)
            {
                Debug.LogError($"[Sync] Full state transfer is corrupt: {ex.Message}");
                _incomingSnapshot.Clear();
                _incomingReplay.Clear();
                return;
            }
            string replayJson = _incomingReplay.ToString();
            _incomingSnapshot.Clear();
            _incomingReplay.Clear();

            string snapshotError = null;
            if (snapshot == null || !snapshot.TryValidate(out snapshotError))
            {
                Debug.LogError($"[Sync] Full state transfer was incomplete or corrupt: {snapshotError ?? "snapshot is missing"}.");
                return;
            }

            _gameManager.RestoreSnapshot(snapshot);
            _replayManager.ImportJson(replayJson);
            Debug.Log($"[Sync] Full state restored at turn {snapshot.TurnNumber}.");
        }

        /// <summary>A desynced client asks the host for a fresh snapshot.</summary>
        private void OnClientDesync(DesyncDetectedEvent evt)
        {
            // A persistent desync fires this on every incoming turn; one
            // in-flight snapshot request at a time is enough.
            if (NetworkTime.time < _nextResyncRequestAt)
                return;

            _nextResyncRequestAt = NetworkTime.time + ResyncCooldownSeconds;
            Debug.LogWarning($"[Sync] Desync detected: {evt.Reason} — requesting resync.");
            CmdRequestResync();
        }

        [Command(requiresAuthority = false)]
        private void CmdRequestResync(NetworkConnectionToClient sender = null)
        {
            if (sender == null) return;

            // Full state is the most expensive thing the host can be asked for
            // and any client may ask, so it is rate limited per connection.
            if (_lastResyncPerConnection.TryGetValue(sender.connectionId, out double last)
                && NetworkTime.time - last < ResyncCooldownSeconds)
                return;

            _lastResyncPerConnection[sender.connectionId] = NetworkTime.time;
            ServerSendFullStateTo(sender);
        }

        #endregion

        #region Client: phase replication

        private void OnPhaseSynced(byte _, byte next)
        {
            if (isServer) return;

            // Adopted, not requested: the host has already decided this phase, so
            // running it through the live transition table would reject the legal
            // jumps replication produces — a rematch (Finished -> Playing) or a
            // hook that lands before RpcStartMatch (Lobby -> Playing) — and leave
            // the client stuck one phase behind the match it is in.
            _stateMachine.RestoreTo((MatchPhase)next);
        }

        #endregion
    }
}
