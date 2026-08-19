using System;
using AMath.Core;
using AMath.Core.Assistance;
using AMath.Core.Events;
using AMath.Core.StateMachines;
using AMath.Tutorial.Events;
using AMath.Tutorial.Hints;
using AMath.Tutorial.Interfaces;
using AMath.Tutorial.Save;
using AMath.Tutorial.StateMachine;
using AMath.Tutorial.Steps;

namespace AMath.Tutorial
{
    /// <summary>
    /// Owns the Script Tutorial lifecycle and ordered step cursor. It
    /// orchestrates authored steps, save progress, lifecycle events and
    /// timed hints, but never owns or mutates gameplay. Gameplay movement is
    /// observed solely through <see cref="TutorialEventListener"/>.
    /// </summary>
    public sealed class TutorialManager : ITickable, IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly ITutorialRuntimeContext _runtimeContext;
        private readonly ILocalizedTextProvider _textProvider;
        private readonly ITutorialSaveStore _saveStore;
        private readonly TutorialEventListener _eventListener;
        private readonly TutorialHint _hintScheduler;
        private readonly StateMachine<TutorialPhase> _stateMachine;

        private ITutorialSequenceDefinition _sequence;
        private TutorialStep _currentStep;
        private int _stepIndex;
        private int _shownHintCount;
        private bool _isAiUnlocked;

        /// <summary>Creates the manager with explicit, independently mockable dependencies.</summary>
        public TutorialManager(
            IEventBus eventBus,
            ITutorialRuntimeContext runtimeContext,
            ILocalizedTextProvider textProvider,
            ITutorialSaveStore saveStore)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _runtimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));
            _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
            _saveStore = saveStore ?? throw new ArgumentNullException(nameof(saveStore));
            _eventListener = new TutorialEventListener(_eventBus);
            _hintScheduler = new TutorialHint(_runtimeContext.Ui, _textProvider);
            _stateMachine = CreateStateMachine();
        }

        /// <summary>Current lifecycle phase.</summary>
        public TutorialPhase Phase => _stateMachine.CurrentKey;

        /// <summary>Current zero-based step index, or -1 while not running a sequence.</summary>
        public int CurrentStepIndex => _sequence == null ? -1 : _stepIndex;

        /// <summary>Current sequence id, or null before the first start.</summary>
        public string CurrentTutorialId => _sequence?.TutorialId;

        /// <summary>
        /// Starts a scripted sequence. When <paramref name="resumeProgress"/>
        /// is true, an unfinished saved cursor is used; a completed save
        /// starts from its first step so the tutorial can be replayed.
        /// </summary>
        public void Start(ITutorialSequenceDefinition sequence, bool resumeProgress = true)
        {
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));
            if (sequence.Steps == null || sequence.Steps.Count == 0)
                throw new ArgumentException("A tutorial must contain at least one step.", nameof(sequence));
            if (string.IsNullOrWhiteSpace(sequence.TutorialId))
                throw new ArgumentException("A tutorial id is required.", nameof(sequence));

            StopCurrentStep();
            _runtimeContext.Dialogue.Skip();
            _sequence = sequence;
            _stepIndex = ResolveStartIndex(sequence, resumeProgress);
            _shownHintCount = 0;
            _isAiUnlocked = false;
            _runtimeContext.Ui.SetAiButtonAvailable(false);

            TransitionToNotStarted();
            _stateMachine.TransitionTo(TutorialPhase.Running);
            EnterCurrentStep();
        }

        /// <summary>Temporarily suspends presentation and condition listening.</summary>
        public void Pause()
        {
            if (Phase != TutorialPhase.Running) return;

            StopCurrentStep();
            _runtimeContext.Dialogue.Skip();
            _stateMachine.TransitionTo(TutorialPhase.Paused);
        }

        /// <summary>Resumes the current step from its beginning.</summary>
        public void Resume()
        {
            if (Phase != TutorialPhase.Paused) return;

            _stateMachine.TransitionTo(TutorialPhase.Running);
            EnterCurrentStep();
        }

        /// <summary>Returns the current step to its beginning without changing saved progress.</summary>
        public void ReplayCurrentStep()
        {
            if (Phase != TutorialPhase.Running && Phase != TutorialPhase.Paused) return;

            StopCurrentStep();
            _runtimeContext.Dialogue.Skip();
            if (Phase == TutorialPhase.Paused)
                _stateMachine.TransitionTo(TutorialPhase.Running);

            EnterCurrentStep();
        }

        /// <summary>Ends the tutorial at the player's explicit request.</summary>
        public void Skip()
        {
            if (Phase != TutorialPhase.Running && Phase != TutorialPhase.Paused) return;

            StopCurrentStep();
            _runtimeContext.Dialogue.Skip();
            _runtimeContext.Highlighter.ClearHighlight();
            _runtimeContext.Ui.SetAiButtonAvailable(false);
            _stateMachine.TransitionTo(TutorialPhase.Skipped);
            PersistProgress(isCompleted: true);
            PublishProgress(isActive: false, objectiveText: null);
        }

        /// <summary>Restarts the active sequence from its first step and overwrites saved cursor progress.</summary>
        public void Restart()
        {
            if (_sequence == null) return;
            Start(_sequence, resumeProgress: false);
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (Phase == TutorialPhase.Running)
                _hintScheduler.Tick(deltaTime);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            StopCurrentStep();
            _runtimeContext.Dialogue.Skip();
            _eventListener.Dispose();
        }

        private void EnterCurrentStep()
        {
            ITutorialStepDefinition definition = _sequence.Steps[_stepIndex];
            if (definition == null)
                throw new InvalidOperationException($"Tutorial '{_sequence.TutorialId}' has a null step at index {_stepIndex}.");

            _currentStep = new TutorialStep(definition);
            string objectiveText = _textProvider.GetText(_currentStep.ObjectiveTextKey);
            _runtimeContext.Ui.SetObjective(objectiveText);
            _runtimeContext.Ui.SetProgress(_stepIndex + 1, _sequence.Steps.Count);
            PublishProgress(isActive: true, objectiveText);
            PersistProgress(isCompleted: false);
            _currentStep.Begin(_runtimeContext, AdvanceStep, BeginHintsForCurrentStep);
        }

        private void BeginHintsForCurrentStep()
        {
            _shownHintCount = 0;
            _hintScheduler.Begin(_currentStep.Hints, OnHintShown);
        }

        private void OnHintShown(int shownHintCount)
        {
            _shownHintCount = shownHintCount;
            if (_isAiUnlocked ||
                _sequence.AiUnlockAfterHintCount < 0 ||
                _shownHintCount < _sequence.AiUnlockAfterHintCount)
            {
                return;
            }

            _isAiUnlocked = true;
            _runtimeContext.Ui.SetAiButtonAvailable(true);
            _eventBus.Publish(new AiButtonUnlockedEvent { StepId = _currentStep.StepId });
        }

        private void AdvanceStep()
        {
            if (Phase != TutorialPhase.Running) return;

            _hintScheduler.Stop();
            _runtimeContext.Highlighter.ClearHighlight();
            _stepIndex++;
            if (_stepIndex < _sequence.Steps.Count)
            {
                EnterCurrentStep();
                return;
            }

            _currentStep = null;
            _runtimeContext.Ui.ClearHint();
            _stateMachine.TransitionTo(TutorialPhase.Completed);
            PersistProgress(isCompleted: true);
            PublishProgress(isActive: false, objectiveText: null);
        }

        private void StopCurrentStep()
        {
            _hintScheduler.Stop();
            _currentStep?.Stop();
            _currentStep = null;
        }

        private int ResolveStartIndex(ITutorialSequenceDefinition sequence, bool resumeProgress)
        {
            if (!resumeProgress ||
                !_saveStore.TryLoad(sequence.TutorialId, out TutorialProgressData saved) ||
                saved == null ||
                saved.IsCompleted)
            {
                return 0;
            }

            return Clamp(saved.StepIndex, 0, sequence.Steps.Count - 1);
        }

        private void PersistProgress(bool isCompleted)
        {
            _saveStore.Save(_sequence.TutorialId, new TutorialProgressData
            {
                TutorialId = _sequence.TutorialId,
                StepIndex = isCompleted ? _sequence.Steps.Count : _stepIndex,
                IsCompleted = isCompleted,
                TimestampUtcTicks = DateTime.UtcNow.Ticks
            });
        }

        private void PublishProgress(bool isActive, string objectiveText)
        {
            _eventBus.Publish(new TutorialStepChangedEvent
            {
                TutorialId = isActive ? _sequence.TutorialId : null,
                StepIndex = isActive ? _stepIndex : -1,
                TotalSteps = isActive ? _sequence.Steps.Count : 0,
                ObjectiveText = objectiveText,
                IsActive = isActive
            });
        }

        private static int Clamp(int value, int minimum, int maximum) =>
            value < minimum ? minimum : value > maximum ? maximum : value;

        private void TransitionToNotStarted()
        {
            switch (Phase)
            {
                case TutorialPhase.Running:
                case TutorialPhase.Paused:
                    _stateMachine.TransitionTo(TutorialPhase.Skipped);
                    break;
                case TutorialPhase.Completed:
                case TutorialPhase.Skipped:
                    _stateMachine.TransitionTo(TutorialPhase.NotStarted);
                    break;
            }

            if (Phase == TutorialPhase.Skipped)
                _stateMachine.TransitionTo(TutorialPhase.NotStarted);
        }

        private static StateMachine<TutorialPhase> CreateStateMachine()
        {
            var machine = new StateMachine<TutorialPhase>();
            var state = new EmptyTutorialState();
            machine
                .AddState(TutorialPhase.NotStarted, state)
                .AddState(TutorialPhase.Running, state)
                .AddState(TutorialPhase.Paused, state)
                .AddState(TutorialPhase.Completed, state)
                .AddState(TutorialPhase.Skipped, state)
                .AllowTransition(TutorialPhase.NotStarted, TutorialPhase.Running)
                .AllowTransition(TutorialPhase.Running, TutorialPhase.Paused)
                .AllowTransition(TutorialPhase.Paused, TutorialPhase.Running)
                .AllowTransition(TutorialPhase.Running, TutorialPhase.Completed)
                .AllowTransition(TutorialPhase.Running, TutorialPhase.Skipped)
                .AllowTransition(TutorialPhase.Paused, TutorialPhase.Skipped)
                .AllowTransition(TutorialPhase.Completed, TutorialPhase.NotStarted)
                .AllowTransition(TutorialPhase.Skipped, TutorialPhase.NotStarted);
            machine.Start(TutorialPhase.NotStarted);
            return machine;
        }

        private sealed class EmptyTutorialState : IState
        {
            public void Enter() { }
            public void Exit() { }
            public void Tick(float deltaTime) { }
        }
    }
}
