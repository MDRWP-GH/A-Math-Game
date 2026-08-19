using System;
using AMath.Core;
using AMath.Core.Events;
using AMath.Core.StateMachines;
using AMath.Gameplay.Interaction;
using AMath.Gameplay.Players;
using AMath.Managers;
using AMath.Networking;
using AMath.Networking.RPC;
using AMath.Networking.Room;
using UnityEngine;

namespace AMath.UI
{
    /// <summary>
    /// Presenter for the in-match HUD. Views bind to events; this class only
    /// translates domain/network state into UI-facing snapshots.
    /// </summary>
    public sealed class MatchHudPresenter : MonoBehaviour
    {
        public event Action StateChanged;
        public event Action<string> ErrorRaised;

        private IEventBus _eventBus;
        private GameManager _gameManager;
        private PlayerManager _playerManager;
        private TurnManager _turnManager;
        private TurnInputSession _turnInput;
        private RoomManager _roomManager;
        private NetworkGameState _networkGameState;
        private string _lastError;

        // Kept so OnDestroy can unsubscribe the exact delegates that were registered.
        private Action<TurnStartedEvent> _onTurnStarted;
        private Action<TurnResolvedEvent> _onTurnResolved;
        private Action<LocalRackChangedEvent> _onRackChanged;
        private Action<MatchPhaseChangedEvent> _onPhaseChanged;
        private Action<MatchFinishedEvent> _onMatchFinished;

        public MatchPhase Phase => _gameManager != null ? _gameManager.Phase : MatchPhase.Lobby;
        public int TurnNumber => _turnManager?.TurnNumber ?? 0;
        public int CurrentPlayerId => _turnManager?.CurrentPlayerId ?? -1;
        public int LocalPlayerId => _playerManager?.LocalPlayerId ?? -1;
        public bool IsLocalTurn => LocalPlayerId >= 0 && LocalPlayerId == CurrentPlayerId;
        public int BagCount => _networkGameState != null ? _networkGameState.BagCount : _gameManager?.BagCount ?? 0;
        public float RemainingTurnSeconds => _networkGameState != null ? _networkGameState.RemainingTurnSeconds : 0f;
        public TurnInputSession TurnInput => _turnInput;
        public PlayerManager Players => _playerManager;
        public GameManager Game => _gameManager;
        public string LastError => _lastError;

        public bool IsMyTurnReady
        {
            get
            {
                if (!IsLocalTurn || Phase != MatchPhase.Playing || _playerManager == null)
                    return false;
                PlayerState local = _playerManager.GetById(LocalPlayerId);
                return local != null && !local.IsAi;
            }
        }

        private void Start()
        {
            TryResolve();
        }

        private void TryResolve()
        {
            if (_eventBus != null || NetworkContext.Services == null) return;

            _eventBus = NetworkContext.Services.Resolve<IEventBus>();
            _gameManager = NetworkContext.Services.Resolve<GameManager>();
            _playerManager = NetworkContext.Services.Resolve<PlayerManager>();
            _turnManager = NetworkContext.Services.Resolve<TurnManager>();
            _turnInput = NetworkContext.Services.Resolve<TurnInputSession>();
            _roomManager = NetworkContext.Services.Resolve<RoomManager>();
            NetworkContext.Services.TryResolve(out _networkGameState);

            _onTurnStarted = _ => Raise();
            _onTurnResolved = _ => { _lastError = null; Raise(); };
            _onRackChanged = _ => Raise();
            _onPhaseChanged = _ => Raise();
            _onMatchFinished = _ => Raise();

            _eventBus.Subscribe(_onTurnStarted);
            _eventBus.Subscribe(_onTurnResolved);
            _eventBus.Subscribe(_onRackChanged);
            _eventBus.Subscribe(_onPhaseChanged);
            _eventBus.Subscribe<CommandRejectedEvent>(OnRejected);
            _eventBus.Subscribe(_onMatchFinished);
            _turnInput.Changed += Raise;
        }

        private void OnDestroy()
        {
            if (_turnInput != null)
                _turnInput.Changed -= Raise;
            if (_eventBus == null) return;
            _eventBus.Unsubscribe(_onTurnStarted);
            _eventBus.Unsubscribe(_onTurnResolved);
            _eventBus.Unsubscribe(_onRackChanged);
            _eventBus.Unsubscribe(_onPhaseChanged);
            _eventBus.Unsubscribe<CommandRejectedEvent>(OnRejected);
            _eventBus.Unsubscribe(_onMatchFinished);
        }

        private void Update()
        {
            TryResolve();
            if (NetworkContext.Services != null)
                NetworkContext.Services.TryResolve(out _networkGameState);
        }

        private void OnRejected(CommandRejectedEvent evt)
        {
            _lastError = evt.Reason;
            ErrorRaised?.Invoke(evt.Reason);
            Raise();
        }

        public void SelectRack(int index) => _turnInput?.SelectFromRack(index);

        public void PlaceCell(int x, int y)
        {
            if (_turnInput == null) return;
            if (!_turnInput.TryPlaceOnCell(x, y, out string error))
            {
                _lastError = error;
                ErrorRaised?.Invoke(error);
                Raise();
            }
        }

        public void RemovePending(int x, int y) => _turnInput?.RemovePendingAt(x, y);

        public void ClearDraft() => _turnInput?.ClearDraft();

        public void SetDeclaration(byte declaredAs)
        {
            if (_turnInput == null) return;
            _turnInput.PendingDeclaration = declaredAs;
            Raise();
        }

        public void ConfirmPlace()
        {
            if (_turnInput == null) return;
            if (!_turnInput.TryConfirmPlace(out string error))
            {
                _lastError = error;
                ErrorRaised?.Invoke(error);
                Raise();
            }
        }

        public void Pass() => _turnInput?.RequestPass();

        public void ExchangeSelected(System.Collections.Generic.IReadOnlyList<int> indices)
        {
            if (_turnInput == null) return;
            if (!_turnInput.TryRequestExchange(indices, out string error))
            {
                _lastError = error;
                ErrorRaised?.Invoke(error);
                Raise();
            }
        }

        public void LeaveRoom() => _roomManager?.LeaveRoom();

        private void Raise() => StateChanged?.Invoke();
    }
}
