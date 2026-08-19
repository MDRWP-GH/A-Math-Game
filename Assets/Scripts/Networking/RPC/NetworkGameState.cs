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
            _eventBus.Subscribe<TurnStartedEvent>(OnServerTurnStarted);
            _eventBus.Subscribe<TurnResolvedEvent>(OnServerTurnResolved);
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
            _eventBus.Unsubscribe<MatchStartedEvent>(OnServerMatchStarted);
            _eventBus.Unsubscribe<TurnStartedEvent>(OnServerTurnStarted);
            _eventBus.Unsubscribe<TurnResolvedEvent>(OnServerTurnResolved);
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
            _gameManager.ApplyRecord(record);
        }

        #endregion

        #region Resync (late join / reconnect / desync recovery)

        /// <summary>Server: pushes the complete state to one connection only.</summary>
        [Server]
        public void ServerSendFullStateTo(NetworkConnectionToClient conn)
        {
            ServerSeedFromGameState();
            GameStateSnapshot snapshot = _gameManager.CaptureSnapshot();
            TargetFullState(conn, JsonUtility.ToJson(snapshot), _replayManager.ExportJson());
        }

        /// <summary>
        /// Copies authoritative match scalars into SyncVars so reconnecting
        /// clients receive consistent lobby/match headers alongside the snapshot.
        /// </summary>
        [Server]
        private void ServerSeedFromGameState()
        {
            if (_gameManager.Config == null)
                return;

            GameStateSnapshot snapshot = _gameManager.CaptureSnapshot();
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
        private void TargetFullState(NetworkConnectionToClient _, string snapshotJson, string replayJson)
        {
            if (isServer) return;

            var snapshot = JsonUtility.FromJson<GameStateSnapshot>(snapshotJson);
            _gameManager.RestoreSnapshot(snapshot);
            _replayManager.ImportJson(replayJson);
            _stateMachine.TransitionTo((MatchPhase)snapshot.Phase);
            Debug.Log($"[Sync] Full state restored at turn {snapshot.TurnNumber}.");
        }

        /// <summary>A desynced client asks the host for a fresh snapshot.</summary>
        private void OnClientDesync(DesyncDetectedEvent evt)
        {
            Debug.LogWarning($"[Sync] Desync detected: {evt.Reason} — requesting resync.");
            CmdRequestResync();
        }

        [Command(requiresAuthority = false)]
        private void CmdRequestResync(NetworkConnectionToClient sender = null)
        {
            if (sender != null)
                ServerSendFullStateTo(sender);
        }

        #endregion

        #region Client: phase replication

        private void OnPhaseSynced(byte _, byte next)
        {
            if (isServer) return;
            _stateMachine.TransitionTo((MatchPhase)next);
        }

        #endregion
    }
}
