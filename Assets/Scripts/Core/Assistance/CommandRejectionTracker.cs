using System;
using AMath.Core.Events;

namespace AMath.Core.Assistance
{
    /// <summary>
    /// Remembers the most recent rejection so it can be handed to the AI
    /// context. Cleared as soon as a turn resolves or a new match starts,
    /// because a stale reason is worse than none — it would make the assistant
    /// explain a problem the player already fixed.
    /// </summary>
    public sealed class CommandRejectionTracker : ICommandRejectionReader, IDisposable
    {
        private readonly IEventBus _eventBus;

        public CommandRejectionTracker(IEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _eventBus.Subscribe<CommandRejectedEvent>(OnRejected);
            _eventBus.Subscribe<TurnResolvedEvent>(OnTurnResolved);
            _eventBus.Subscribe<MatchStartedEvent>(OnMatchStarted);
        }

        /// <inheritdoc />
        public string LastRejectionReason { get; private set; }

        private void OnRejected(CommandRejectedEvent evt) => LastRejectionReason = evt.Reason;

        private void OnTurnResolved(TurnResolvedEvent _) => LastRejectionReason = null;

        private void OnMatchStarted(MatchStartedEvent _) => LastRejectionReason = null;

        /// <inheritdoc />
        public void Dispose()
        {
            _eventBus.Unsubscribe<CommandRejectedEvent>(OnRejected);
            _eventBus.Unsubscribe<TurnResolvedEvent>(OnTurnResolved);
            _eventBus.Unsubscribe<MatchStartedEvent>(OnMatchStarted);
        }
    }
}
