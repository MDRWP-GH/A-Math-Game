using System;
using AMath.Core.Commands;
using AMath.Core.Events;
using AMath.Tutorial.Events;
using AMath.Tutorial.Interfaces;

namespace AMath.Tutorial.Conditions
{
    /// <summary>
    /// Event-driven condition that waits for one normalized tutorial signal.
    /// Optionally narrows the match to a stable target id (button/menu) or a
    /// tile id. It never polls gameplay state.
    /// </summary>
    public sealed class TutorialCondition : ITutorialCondition
    {
        private readonly TutorialGameplaySignalKind _expectedKind;
        private readonly string _expectedTargetId;
        private readonly byte? _expectedTileId;
        private readonly CommandType? _expectedCommandType;

        private Action _onSatisfied;
        private bool _isWaiting;
        private bool _isSubscribed;

        /// <summary>Creates a condition for any signal of <paramref name="expectedKind"/>.</summary>
        public TutorialCondition(TutorialGameplaySignalKind expectedKind)
            : this(expectedKind, null, null, null)
        {
        }

        /// <summary>Creates a condition requiring the given stable button/menu id.</summary>
        public TutorialCondition(TutorialGameplaySignalKind expectedKind, string expectedTargetId)
            : this(expectedKind, expectedTargetId, null, null)
        {
        }

        /// <summary>Creates a condition requiring the given tile id.</summary>
        public TutorialCondition(TutorialGameplaySignalKind expectedKind, byte expectedTileId)
            : this(expectedKind, null, expectedTileId, null)
        {
        }

        /// <summary>Creates a condition requiring a resolved turn of the given command type.</summary>
        public TutorialCondition(CommandType expectedCommandType)
            : this(TutorialGameplaySignalKind.TurnEnded, null, null, expectedCommandType)
        {
        }

        private TutorialCondition(
            TutorialGameplaySignalKind expectedKind,
            string expectedTargetId,
            byte? expectedTileId,
            CommandType? expectedCommandType)
        {
            _expectedKind = expectedKind;
            _expectedTargetId = expectedTargetId;
            _expectedTileId = expectedTileId;
            _expectedCommandType = expectedCommandType;
        }

        /// <inheritdoc />
        public void BeginWaiting(IEventBus eventBus, Action onSatisfied)
        {
            if (eventBus == null) throw new ArgumentNullException(nameof(eventBus));
            if (onSatisfied == null) throw new ArgumentNullException(nameof(onSatisfied));
            if (_isWaiting) throw new InvalidOperationException("Condition is already waiting.");

            _onSatisfied = onSatisfied;
            _isWaiting = true;
            _isSubscribed = true;
            eventBus.Subscribe<TutorialGameplaySignalEvent>(OnSignal);
        }

        /// <inheritdoc />
        public void EndWaiting(IEventBus eventBus)
        {
            if (!_isSubscribed) return;

            eventBus.Unsubscribe<TutorialGameplaySignalEvent>(OnSignal);
            _isWaiting = false;
            _isSubscribed = false;
            _onSatisfied = null;
        }

        private void OnSignal(TutorialGameplaySignalEvent evt)
        {
            if (!_isWaiting ||
                evt.Kind != _expectedKind ||
                (_expectedTargetId != null && evt.TargetId != _expectedTargetId) ||
                (_expectedTileId.HasValue && evt.TileId != _expectedTileId.Value) ||
                (_expectedCommandType.HasValue && evt.CommandType != _expectedCommandType))
            {
                return;
            }

            Action callback = _onSatisfied;
            _isWaiting = false;
            _onSatisfied = null;
            callback?.Invoke();
        }
    }
}
