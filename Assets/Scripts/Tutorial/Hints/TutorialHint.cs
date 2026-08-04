using System;
using System.Collections.Generic;
using AMath.Core.Assistance;
using AMath.Tutorial.Interfaces;

namespace AMath.Tutorial.Hints
{
    /// <summary>
    /// Runs one step's authored, time-based hint schedule. Timed hints are
    /// the only tutorial behavior that receives a frame delta; gameplay
    /// progression itself remains event-driven through
    /// <see cref="ITutorialCondition"/>.
    /// </summary>
    public sealed class TutorialHint
    {
        private readonly ITutorialUiService _ui;
        private readonly ILocalizedTextProvider _textProvider;

        private IReadOnlyList<TutorialHintDefinition> _definitions;
        private Action<int> _onHintShown;
        private float _elapsedSeconds;
        private int _nextHintIndex;
        private bool _isRunning;

        /// <summary>Creates the hint scheduler.</summary>
        public TutorialHint(ITutorialUiService ui, ILocalizedTextProvider textProvider)
        {
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
        }

        /// <summary>Starts a new hint schedule, replacing any active one.</summary>
        public void Begin(IReadOnlyList<TutorialHintDefinition> definitions, Action<int> onHintShown)
        {
            _definitions = definitions ?? Array.Empty<TutorialHintDefinition>();
            _onHintShown = onHintShown;
            _elapsedSeconds = 0f;
            _nextHintIndex = 0;
            _isRunning = _definitions.Count > 0;
            _ui.ClearHint();
        }

        /// <summary>Stops the schedule and clears its visible hint.</summary>
        public void Stop()
        {
            _isRunning = false;
            _definitions = null;
            _onHintShown = null;
            _ui.ClearHint();
        }

        /// <summary>Advances scripted hint timing. Called only while a step is waiting for its condition.</summary>
        public void Tick(float deltaTime)
        {
            if (!_isRunning || deltaTime <= 0f) return;

            _elapsedSeconds += deltaTime;
            while (_nextHintIndex < _definitions.Count &&
                   _elapsedSeconds >= _definitions[_nextHintIndex].DelaySeconds)
            {
                TutorialHintDefinition hint = _definitions[_nextHintIndex];
                _ui.ShowHint(_textProvider.GetText(hint.HintTextKey));
                _nextHintIndex++;
                _onHintShown?.Invoke(_nextHintIndex);
            }

            if (_nextHintIndex >= _definitions.Count)
                _isRunning = false;
        }
    }
}
