using System;
using AMath.Core.Events;

namespace AMath.Core.Assistance.Context
{
    /// <summary>
    /// Core-side <see cref="ITutorialProgressReader"/>. It listens for
    /// <see cref="TutorialStepChangedEvent"/> on the shared bus, which is how
    /// the AI context can describe the active tutorial step without Core ever
    /// referencing AMath.Tutorial.
    /// </summary>
    public sealed class TutorialProgressTracker : ITutorialProgressReader, IDisposable
    {
        private readonly IEventBus _eventBus;

        public TutorialProgressTracker(IEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _eventBus.Subscribe<TutorialStepChangedEvent>(OnStepChanged);
        }

        /// <inheritdoc />
        public bool IsTutorialActive { get; private set; }

        /// <inheritdoc />
        public string ActiveTutorialId { get; private set; }

        /// <inheritdoc />
        public int CurrentStepIndex { get; private set; } = -1;

        /// <inheritdoc />
        public int TotalStepCount { get; private set; }

        /// <inheritdoc />
        public string CurrentObjectiveText { get; private set; }

        /// <inheritdoc />
        public string CurrentStepId { get; private set; }

        private void OnStepChanged(TutorialStepChangedEvent evt)
        {
            IsTutorialActive = evt.IsActive;

            if (!evt.IsActive)
            {
                ActiveTutorialId = null;
                CurrentStepId = null;
                CurrentStepIndex = -1;
                TotalStepCount = 0;
                CurrentObjectiveText = null;
                return;
            }

            ActiveTutorialId = evt.TutorialId;
            CurrentStepId = evt.StepId;
            CurrentStepIndex = evt.StepIndex;
            TotalStepCount = evt.TotalSteps;
            CurrentObjectiveText = evt.ObjectiveText;
        }

        /// <inheritdoc />
        public void Dispose() => _eventBus.Unsubscribe<TutorialStepChangedEvent>(OnStepChanged);
    }
}
