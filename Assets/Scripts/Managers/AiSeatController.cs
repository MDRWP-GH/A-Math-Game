using System;
using AMath.Core;
using AMath.Core.Commands;
using AMath.Core.Events;
using AMath.Core.StateMachines;
using AMath.Gameplay.AI;
using AMath.Gameplay.Board;
using AMath.Gameplay.Players;

namespace AMath.Managers
{
    /// <summary>
    /// Host-only driver for AI seats: when an AI player's turn starts, waits a
    /// short think delay then submits a medium scripted move through the same
    /// command pipeline as human players.
    /// </summary>
    public sealed class AiSeatController : ITickable, IDisposable
    {
        private const float MinThinkSeconds = 0.55f;
        private const float MaxThinkSeconds = 1.35f;

        private readonly IEventBus _eventBus;
        private readonly GameManager _gameManager;
        private readonly PlayerManager _playerManager;
        private readonly BoardManager _boardManager;
        private readonly TurnManager _turnManager;
        private readonly IAiMoveChooser _moveChooser;

        private bool _pending;
        private float _thinkRemaining;
        private int _pendingPlayerId = -1;

        public AiSeatController(
            IEventBus eventBus,
            GameManager gameManager,
            PlayerManager playerManager,
            BoardManager boardManager,
            TurnManager turnManager,
            IAiMoveChooser moveChooser)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            _playerManager = playerManager ?? throw new ArgumentNullException(nameof(playerManager));
            _boardManager = boardManager ?? throw new ArgumentNullException(nameof(boardManager));
            _turnManager = turnManager ?? throw new ArgumentNullException(nameof(turnManager));
            _moveChooser = moveChooser ?? throw new ArgumentNullException(nameof(moveChooser));

            _eventBus.Subscribe<TurnStartedEvent>(OnTurnStarted);
            _eventBus.Subscribe<MatchPhaseChangedEvent>(OnPhaseChanged);
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!_pending || !_gameManager.IsAuthority || _gameManager.Phase != MatchPhase.Playing)
                return;

            _thinkRemaining -= deltaTime;
            if (_thinkRemaining > 0f) return;

            _pending = false;
            int playerId = _pendingPlayerId;
            _pendingPlayerId = -1;

            PlayerState player = _playerManager.GetById(playerId);
            if (player == null || !player.IsAi) return;
            if (_turnManager.CurrentPlayerId != playerId) return;

            int choiceSeed = unchecked(
                (_gameManager.Config?.RandomSeed ?? 0) * 397
                ^ _turnManager.TurnNumber * 7919
                ^ playerId * 104729);

            IGameCommand command = _moveChooser.Choose(
                _boardManager,
                player.Rack,
                _gameManager.BagCount,
                choiceSeed);

            _gameManager.SubmitCommand(playerId, command, out _);
        }

        private void OnTurnStarted(TurnStartedEvent evt)
        {
            _pending = false;
            _pendingPlayerId = -1;

            if (!_gameManager.IsAuthority || _gameManager.Phase != MatchPhase.Playing)
                return;

            PlayerState player = _playerManager.GetById(evt.PlayerId);
            if (player == null || !player.IsAi) return;

            _pending = true;
            _pendingPlayerId = evt.PlayerId;
            // Spread think time a little so medium AI does not feel instant.
            float t = MinThinkSeconds
                      + (MaxThinkSeconds - MinThinkSeconds)
                      * ((evt.TurnNumber * 17 + evt.PlayerId * 31) % 100) / 100f;
            _thinkRemaining = t;
        }

        private void OnPhaseChanged(MatchPhaseChangedEvent evt)
        {
            if (evt.Current != MatchPhase.Playing)
            {
                _pending = false;
                _pendingPlayerId = -1;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _eventBus.Unsubscribe<TurnStartedEvent>(OnTurnStarted);
            _eventBus.Unsubscribe<MatchPhaseChangedEvent>(OnPhaseChanged);
        }
    }
}
