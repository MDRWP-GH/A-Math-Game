using System;
using AMath.Core.Events;
using AMath.Tutorial.Interfaces;

namespace AMath.Tutorial.Steps
{
    /// <summary>
    /// Runtime executor for one authored step. It has exactly two jobs:
    /// execute its actions in authored order, then wait for its event-driven
    /// completion condition. Selecting the next step is deliberately left to
    /// <c>TutorialManager</c>.
    /// </summary>
    public sealed class TutorialStep
    {
        private readonly ITutorialStepDefinition _definition;

        private ITutorialRuntimeContext _context;
        private Action _onCompleted;
        private Action _onReadyToWait;
        private int _nextActionIndex;
        private bool _isActive;
        private bool _isWaitingForCondition;

        /// <summary>Creates a runtime step for an authored definition.</summary>
        public TutorialStep(ITutorialStepDefinition definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        /// <summary>Stable authored id.</summary>
        public string StepId => _definition.StepId;

        /// <summary>Localization key for the active objective.</summary>
        public string ObjectiveTextKey => _definition.ObjectiveTextKey;

        /// <summary>Authored hint schedule for this step.</summary>
        public System.Collections.Generic.IReadOnlyList<TutorialHintDefinition> Hints => _definition.Hints;

        /// <summary>Starts this step from its first action.</summary>
        public void Begin(ITutorialRuntimeContext context, Action onCompleted, Action onReadyToWait)
        {
            if (_isActive) throw new InvalidOperationException("Tutorial step is already active.");

            _context = context ?? throw new ArgumentNullException(nameof(context));
            _onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
            _onReadyToWait = onReadyToWait ?? throw new ArgumentNullException(nameof(onReadyToWait));
            _nextActionIndex = 0;
            _isActive = true;
            RunNextAction();
        }

        /// <summary>Stops all event subscriptions and ignores late action callbacks.</summary>
        public void Stop()
        {
            if (!_isActive) return;

            if (_isWaitingForCondition)
                _definition.AdvanceCondition.EndWaiting(_context.EventBus);

            _isActive = false;
            _isWaitingForCondition = false;
            _onCompleted = null;
            _onReadyToWait = null;
            _context = null;
        }

        private void RunNextAction()
        {
            if (!_isActive) return;

            if (_nextActionIndex >= _definition.Actions.Count)
            {
                BeginCondition();
                return;
            }

            ITutorialAction action = _definition.Actions[_nextActionIndex++];
            if (action == null)
                throw new InvalidOperationException($"Tutorial step '{StepId}' contains a null action.");

            action.Execute(_context, RunNextAction);
        }

        private void BeginCondition()
        {
            if (!_isActive) return;

            _isWaitingForCondition = true;
            _definition.AdvanceCondition.BeginWaiting(_context.EventBus, Complete);
            _onReadyToWait?.Invoke();
        }

        private void Complete()
        {
            if (!_isActive) return;

            if (_isWaitingForCondition)
                _definition.AdvanceCondition.EndWaiting(_context.EventBus);

            _isWaitingForCondition = false;
            _isActive = false;
            Action callback = _onCompleted;
            _onCompleted = null;
            _onReadyToWait = null;
            _context = null;
            callback?.Invoke();
        }
    }
}
