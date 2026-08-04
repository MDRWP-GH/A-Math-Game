using System;

namespace AMath.Core.Events
{
    /// <summary>
    /// Typed publish/subscribe bus used to decouple systems from each other.
    /// Core publishes domain events; networking, save, replay and UI layers
    /// subscribe without any direct references between them.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>Subscribes <paramref name="handler"/> to events of type <typeparamref name="TEvent"/>.</summary>
        void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;

        /// <summary>Removes a previously registered handler.</summary>
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;

        /// <summary>Publishes an event synchronously to all current subscribers.</summary>
        void Publish<TEvent>(TEvent evt) where TEvent : struct;
    }
}
