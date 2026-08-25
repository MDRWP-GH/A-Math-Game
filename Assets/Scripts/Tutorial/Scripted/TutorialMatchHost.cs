using System;
using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Assistance;
using AMath.Core.Commands;
using AMath.Core.Events;
using AMath.Core.StateMachines;
using AMath.Gameplay.Board;
using AMath.Gameplay.Interaction;
using AMath.Gameplay.Players;
using AMath.Managers;
using AMath.Tutorial.Definitions;

namespace AMath.Tutorial.Scripted
{
    /// <summary>
    /// Offline tutorial match: predetermined racks, scripted bot turns, and
    /// constrained human input. Never uses <c>IAiMoveChooser</c>.
    /// </summary>
    public sealed class TutorialMatchHost : ITickable, IDisposable
    {
        private const float BotThinkSeconds = 0.75f;

        private readonly IEventBus _eventBus;
        private readonly ScriptedTutorialMatchScript _script;
        private readonly ScriptedTurnInputConstraint _constraint;
        private readonly CommandRejectionTracker _rejectionTracker;

        private int _alignedStepIndex = -1;
        private bool _botPending;
        private float _botThinkRemaining;
        private bool _disposed;

        public TutorialMatchHost(IEventBus eventBus, ILocalizedTextProvider text, ScriptedTutorialMatchScript script)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _script = script ?? throw new ArgumentNullException(nameof(script));
            _constraint = new ScriptedTurnInputConstraint(text);
            var stateMachine = new GameStateMachine(_eventBus);
            Board = new BoardManager();
            Players = new PlayerManager(_eventBus);
            Turns = new TurnManager(_eventBus);
            Game = new GameManager(_eventBus, stateMachine, Board, Players, Turns) { IsAuthority = true };
            Input = new TurnInputSession(_eventBus, Board, Players, _constraint);
            _rejectionTracker = new CommandRejectionTracker(_eventBus);
            Readers = new TutorialMatchReaders(Board, Players, Game, Turns, _rejectionTracker);

            Players.LocalPlayerId = ScriptedTutorialMatchScript.HumanPlayerId;
            Input.Changed += RaiseChanged;

            _eventBus.Subscribe<TutorialStepChangedEvent>(OnTutorialStepChanged);
            _eventBus.Subscribe<LocalCommandRequestedEvent>(OnLocalCommandRequested);
            _eventBus.Subscribe<TurnResolvedEvent>(OnTurnResolved);
            _eventBus.Subscribe<CommandRejectedEvent>(OnCommandRejected);
        }

        /// <summary>Authoritative match.</summary>
        public GameManager Game { get; }

        /// <summary>Board domain.</summary>
        public BoardManager Board { get; }

        /// <summary>Roster and racks.</summary>
        public PlayerManager Players { get; }

        /// <summary>Turn cursor.</summary>
        public TurnManager Turns { get; }

        /// <summary>Local human draft.</summary>
        public TurnInputSession Input { get; }

        /// <summary>Read-only views for tutorial actions.</summary>
        public TutorialMatchReaders Readers { get; }

        /// <summary>True while the player may place the current scripted equation.</summary>
        public bool IsPlayerInputEnabled => _constraint.InputEnabled;

        /// <summary>Authored cells still waiting in the current player draft.</summary>
        public IReadOnlyList<TilePlacement> RemainingGuidePlacements =>
            _constraint.InputEnabled
                ? ScriptedPlacementRules.Remaining(_constraint.Expected, Input.PendingPlacements)
                : Array.Empty<TilePlacement>();

        /// <summary>Raised when the board, rack, draft or turn changes.</summary>
        public event Action Changed;

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!_botPending || Game.Phase != MatchPhase.Playing)
                return;

            _botThinkRemaining -= deltaTime;
            if (_botThinkRemaining > 0f) return;

            _botPending = false;
            PlayScriptedTurn(_script.Turns[1]);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Input.Changed -= RaiseChanged;
            _eventBus.Unsubscribe<TutorialStepChangedEvent>(OnTutorialStepChanged);
            _eventBus.Unsubscribe<LocalCommandRequestedEvent>(OnLocalCommandRequested);
            _eventBus.Unsubscribe<TurnResolvedEvent>(OnTurnResolved);
            _eventBus.Unsubscribe<CommandRejectedEvent>(OnCommandRejected);
            _rejectionTracker.Dispose();
        }

        private void OnTutorialStepChanged(TutorialStepChangedEvent evt)
        {
            if (!evt.IsActive)
            {
                CancelBot();
                LockInput();
                RaiseChanged();
                return;
            }

            bool rebuild = _alignedStepIndex < 0 || evt.StepIndex <= _alignedStepIndex;
            if (rebuild)
                RestartMatchAndFastForward(evt.StepId);

            ConfigureForStep(evt.StepId);
            _alignedStepIndex = evt.StepIndex;
            RaiseChanged();
        }

        private void RestartMatchAndFastForward(string stepId)
        {
            CancelBot();
            Input.ClearDraft();

            if (Game.Phase == MatchPhase.Playing || Game.Phase == MatchPhase.Paused)
                Game.EndMatchManually();

            Game.StartMatch(_script.Config, _script.OpeningRacks);

            int completed = CompletedTurnsBefore(stepId);
            for (int i = 0; i < completed; i++)
                PlayScriptedTurn(_script.Turns[i]);
        }

        private void ConfigureForStep(string stepId)
        {
            CancelBot();
            Input.ClearDraft();

            if (stepId == IntroTutorialSequence.PlayerPlaceStepId)
            {
                _constraint.InputEnabled = true;
                _constraint.Expected = _script.Turns[0].Placements;
                return;
            }

            LockInput();

            if (stepId == IntroTutorialSequence.OpponentStepId)
            {
                _botPending = true;
                _botThinkRemaining = BotThinkSeconds;
            }
        }

        private void PlayScriptedTurn(ScriptedTutorialTurn turn)
        {
            var command = new PlaceTilesCommand();
            command.Placements.AddRange(turn.Placements);
            CommandOutcome outcome = Game.SubmitCommand(turn.PlayerId, command, out _);
            if (!outcome.Success)
            {
                _eventBus.Publish(new CommandRejectedEvent
                {
                    Reason = outcome.Error ?? "Scripted tutorial turn failed."
                });
            }
        }

        private void OnLocalCommandRequested(LocalCommandRequestedEvent evt)
        {
            if (evt.Command == null || Players.LocalPlayerId < 0)
                return;

            CommandOutcome outcome = Game.SubmitCommand(Players.LocalPlayerId, evt.Command, out _);
            if (!outcome.Success)
            {
                _eventBus.Publish(new CommandRejectedEvent
                {
                    Reason = outcome.Error ?? "Command rejected."
                });
            }
        }

        private void OnTurnResolved(TurnResolvedEvent _) => RaiseChanged();

        private void OnCommandRejected(CommandRejectedEvent _) => RaiseChanged();

        private void LockInput()
        {
            _constraint.InputEnabled = false;
            _constraint.Expected = Array.Empty<TilePlacement>();
        }

        private void CancelBot()
        {
            _botPending = false;
            _botThinkRemaining = 0f;
        }

        private static int CompletedTurnsBefore(string stepId)
        {
            if (stepId == IntroTutorialSequence.OpponentStepId) return 1;
            if (stepId == IntroTutorialSequence.CompleteStepId) return 2;
            return 0;
        }

        private void RaiseChanged() => Changed?.Invoke();
    }
}
