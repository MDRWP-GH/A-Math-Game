using System;
using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Assistance;
using AMath.Core.Commands;
using AMath.Core.Events;
using AMath.Core.StateMachines;
using AMath.Gameplay;
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
        private ScriptedTutorialMatchScript _script;
        private readonly ScriptedTurnInputConstraint _constraint;
        private readonly CommandRejectionTracker _rejectionTracker;

        private int _alignedStepIndex = -1;
        private bool _botPending;
        private float _botThinkRemaining;
        private bool _disposed;

        public TutorialMatchHost(IEventBus eventBus, ILocalizedTextProvider text, ScriptedTutorialMatchScript script)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            SetScript(script);
            _constraint = new ScriptedTurnInputConstraint(text);
            var stateMachine = new GameStateMachine(_eventBus);
            Board = new BoardManager();
            Players = new PlayerManager(_eventBus);
            Players.LocalPlayerId = ScriptedTutorialMatchScript.HumanPlayerId;
            Turns = new TurnManager(_eventBus);
            Game = new GameManager(_eventBus, stateMachine, Board, Players, Turns) { IsAuthority = true };
            Input = new TurnInputSession(_eventBus, Board, Players, _constraint);
            _rejectionTracker = new CommandRejectionTracker(_eventBus);
            Readers = new TutorialMatchReaders(Board, Players, Game, Turns, _rejectionTracker);

            Input.Changed += RaiseChanged;

            _eventBus.Subscribe<TutorialStepChangedEvent>(OnTutorialStepChanged);
            _eventBus.Subscribe<LocalCommandRequestedEvent>(OnLocalCommandRequested);
            _eventBus.Subscribe<TurnResolvedEvent>(OnTurnResolved);
            _eventBus.Subscribe<CommandRejectedEvent>(OnCommandRejected);
        }

        public GameManager Game { get; }
        public BoardManager Board { get; }
        public PlayerManager Players { get; }
        public TurnManager Turns { get; }
        public TurnInputSession Input { get; }
        public TutorialMatchReaders Readers { get; }
        public byte? GuidedRackTileId { get; private set; }
        public IReadOnlyList<int> GuidedExchangeIndices { get; private set; } = Array.Empty<int>();
        public bool IsPlayerInputEnabled => _constraint.InputEnabled;
        public bool IsPassActionEnabled => _constraint.PassEnabled;
        public bool IsExchangeActionEnabled => _constraint.ExchangeEnabled;

        public IReadOnlyList<TilePlacement> RemainingGuidePlacements =>
            _constraint.InputEnabled && _constraint.PlacementEnabled
                ? (IReadOnlyList<TilePlacement>)ScriptedPlacementRules.Remaining(
                    _constraint.Expected,
                    Input.PendingPlacements)
                : Array.Empty<TilePlacement>();

        public event Action Changed;

        public void SetScript(ScriptedTutorialMatchScript script)
        {
            _script = script ?? throw new ArgumentNullException(nameof(script));
            if (Players != null)
                Players.LocalPlayerId = ScriptedTutorialMatchScript.HumanPlayerId;
        }

        public void Tick(float deltaTime)
        {
            if (!_botPending || Game.Phase != MatchPhase.Playing)
                return;

            _botThinkRemaining -= deltaTime;
            if (_botThinkRemaining > 0f) return;

            _botPending = false;
            PlayScriptedTurn(_script.Turns[1]);
        }

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
                GuidedRackTileId = null;
                GuidedExchangeIndices = Array.Empty<int>();
                RaiseChanged();
                return;
            }

            // The pass and exchange lessons use fresh authored racks and a
            // separate board. Entering their introductions must reset the
            // match even when the tutorial advances forward from placement.
            bool enteringLesson = evt.StepId == PremiumTutorialSequence.PassIntroStepId
                || evt.StepId == PremiumTutorialSequence.ExchangeIntroStepId;
            bool rebuild = _alignedStepIndex < 0 || evt.StepIndex <= _alignedStepIndex || enteringLesson;
            if (rebuild)
            {
                RestartMatchAndFastForward(evt.StepId);
                RestorePendingPlacements(evt.StepId);
            }

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

            if (TutorialStepRouting.IsExchangeLessonRestart(stepId))
            {
                StartLessonMatch(_script.ExchangeLessonRack, _script.LessonBoard);
                return;
            }

            if (TutorialStepRouting.IsPassLessonRestart(stepId))
            {
                StartLessonMatch(_script.PassLessonRack, _script.LessonBoard);
                return;
            }

            Game.StartMatch(_script.Config, _script.OpeningRacks);
            ApplyBoard(_script.InitialBoard);

            int completed = TutorialStepRouting.CompletedTurnsBefore(stepId);
            for (int i = 0; i < completed && i < _script.Turns.Count; i++)
                PlayScriptedTurn(_script.Turns[i]);
        }

        private void StartLessonMatch(IReadOnlyList<byte> rack, IReadOnlyList<TilePlacement> board)
        {
            IReadOnlyList<IReadOnlyList<byte>> racks = rack.Count > 0
                ? _script.OpeningRacks.Count > 1
                    ? new IReadOnlyList<byte>[] { rack, _script.OpeningRacks[1] }
                    : new IReadOnlyList<byte>[] { rack }
                : _script.OpeningRacks;

            Game.StartMatch(_script.Config, racks);
            ApplyBoard(board.Count > 0 ? board : _script.InitialBoard);
        }

        private void ApplyBoard(IReadOnlyList<TilePlacement> placements)
        {
            for (int i = 0; i < placements.Count; i++)
            {
                TilePlacement placement = placements[i];
                Board.Grid.Place(in placement);
            }
        }

        private void RestorePendingPlacements(string stepId)
        {
            int count = TutorialStepRouting.PendingPlacementsBefore(stepId);
            if (count <= 0)
                return;

            IReadOnlyList<TilePlacement> turn = ActiveHumanTurnPlacements();
            PlayerState local = Players.GetById(Players.LocalPlayerId);
            if (local == null)
                return;

            _constraint.InputEnabled = true;
            _constraint.PlacementEnabled = true;
            _constraint.Expected = turn;

            for (int i = 0; i < count && i < turn.Count; i++)
            {
                TilePlacement placement = turn[i];
                int rackIndex = FindRackIndex(local.Rack, placement.TileId);
                if (rackIndex < 0)
                    break;

                Input.SelectFromRack(rackIndex);
                Input.PendingDeclaration = placement.DeclaredAs == TilePlacement.NoDeclaration
                    ? null
                    : placement.DeclaredAs;
                if (!Input.TryPlaceOnCell(placement.X, placement.Y, out string error))
                {
                    UnityEngine.Debug.LogWarning(
                        $"[Tutorial] Could not restore draft for '{stepId}': {error}");
                    break;
                }
            }
        }

        private static int FindRackIndex(IReadOnlyList<byte> rack, byte tileId)
        {
            for (int i = 0; i < rack.Count; i++)
            {
                if (rack[i] == tileId)
                    return i;
            }

            return -1;
        }

        private void ConfigureForStep(string stepId)
        {
            CancelBot();
            GuidedRackTileId = null;
            GuidedExchangeIndices = Array.Empty<int>();
            _constraint.ExpectedExchangeIndices = Array.Empty<int>();
            _constraint.PassEnabled = false;
            _constraint.ExchangeEnabled = false;

            if (TutorialStepRouting.TryGetSelectTile(stepId, out byte tileId))
            {
                _constraint.InputEnabled = true;
                _constraint.PlacementEnabled = false;
                _constraint.Expected = ActiveHumanTurnPlacements();
                GuidedRackTileId = tileId;
                return;
            }

            if (TutorialStepRouting.TryGetPlaceCell(stepId, out int x, out int y))
            {
                _constraint.InputEnabled = true;
                _constraint.PlacementEnabled = true;
                _constraint.Expected = SingleExpectedPlacement(x, y);
                if (_constraint.Expected.Count > 0)
                    GuidedRackTileId = _constraint.Expected[0].TileId;
                return;
            }

            if (TutorialStepRouting.IsConfirmStep(stepId))
            {
                _constraint.InputEnabled = true;
                _constraint.PlacementEnabled = true;
                _constraint.Expected = ActiveHumanTurnPlacements();
                return;
            }

            if (TutorialStepRouting.IsPassStep(stepId))
            {
                _constraint.InputEnabled = false;
                _constraint.PlacementEnabled = false;
                _constraint.PassEnabled = true;
                _constraint.Expected = Array.Empty<TilePlacement>();
                return;
            }

            if (TutorialStepRouting.IsExchangeStep(stepId))
            {
                _constraint.InputEnabled = false;
                _constraint.PlacementEnabled = false;
                _constraint.ExchangeEnabled = true;
                _constraint.Expected = Array.Empty<TilePlacement>();
                GuidedExchangeIndices = TutorialAuthoredBoard.Premium.ExchangeIndices;
                _constraint.ExpectedExchangeIndices = TutorialAuthoredBoard.Premium.ExchangeIndices;
                return;
            }

            LockInput();

            if (TutorialStepRouting.IsOpponentStep(stepId))
            {
                _botPending = true;
                _botThinkRemaining = BotThinkSeconds;
            }
        }

        private IReadOnlyList<TilePlacement> ActiveHumanTurnPlacements()
        {
            for (int i = 0; i < _script.Turns.Count; i++)
            {
                if (_script.Turns[i].PlayerId == ScriptedTutorialMatchScript.HumanPlayerId)
                    return _script.Turns[i].Placements;
            }

            return Array.Empty<TilePlacement>();
        }

        private IReadOnlyList<TilePlacement> SingleExpectedPlacement(int x, int y)
        {
            IReadOnlyList<TilePlacement> turn = ActiveHumanTurnPlacements();
            for (int i = 0; i < turn.Count; i++)
            {
                TilePlacement placement = turn[i];
                if (placement.X == x && placement.Y == y)
                    return new[] { placement };
            }

            return Array.Empty<TilePlacement>();
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
            _constraint.PlacementEnabled = true;
            _constraint.PassEnabled = false;
            _constraint.ExchangeEnabled = false;
            _constraint.ExpectedExchangeIndices = Array.Empty<int>();
            _constraint.Expected = Array.Empty<TilePlacement>();
        }

        private void CancelBot()
        {
            _botPending = false;
            _botThinkRemaining = 0f;
        }

        private void RaiseChanged() => Changed?.Invoke();
    }
}
