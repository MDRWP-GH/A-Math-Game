using System;
using AMath.Core.Events;

namespace AMath.Tutorial.Interfaces
{
    /// <summary>
    /// Decides when a running step is allowed to advance. Conditions are
    /// strictly event-driven — never polled — so a step like "wait until
    /// the player drags a tile" costs nothing per frame; it simply sleeps
    /// as a subscribed handler until the relevant event fires.
    /// </summary>
    public interface ITutorialCondition
    {
        /// <summary>
        /// Starts watching for satisfaction. Implementations subscribe to
        /// whatever <see cref="IEventBus"/> events they care about and
        /// invoke <paramref name="onSatisfied"/> at most once when met.
        /// </summary>
        void BeginWaiting(IEventBus eventBus, Action onSatisfied);

        /// <summary>
        /// Stops watching (unsubscribes everything <see cref="BeginWaiting"/>
        /// registered). Called when the step completes, is skipped, or the
        /// tutorial is restarted/torn down.
        /// </summary>
        void EndWaiting(IEventBus eventBus);
    }
}
