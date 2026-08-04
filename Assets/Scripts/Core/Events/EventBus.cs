using System;
using System.Collections.Generic;
using UnityEngine;

namespace AMath.Core.Events
{
    /// <summary>
    /// Default <see cref="IEventBus"/> implementation.
    /// Handlers are copied into a depth-pooled buffer before invocation, so
    /// (a) subscribing/unsubscribing from inside a handler is safe, and
    /// (b) handlers may publish further events (nested publish) without
    /// corrupting the outer iteration — event chains like
    /// TurnResolved → autosave → SaveCompleted are the normal case here.
    /// Buffers are reused per nesting depth, so steady-state publishing is
    /// allocation-free.
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        #region Fields

        private readonly Dictionary<Type, List<Delegate>> _handlers = new();
        // One reusable buffer per publish nesting depth (index == depth).
        private readonly List<List<Delegate>> _bufferPool = new();
        private int _publishDepth;

        #endregion

        #region IEventBus

        /// <inheritdoc />
        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            if (!_handlers.TryGetValue(typeof(TEvent), out List<Delegate> list))
            {
                list = new List<Delegate>(4);
                _handlers.Add(typeof(TEvent), list);
            }

            list.Add(handler);
        }

        /// <inheritdoc />
        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            if (handler == null) return;
            if (_handlers.TryGetValue(typeof(TEvent), out List<Delegate> list))
                list.Remove(handler);
        }

        /// <inheritdoc />
        public void Publish<TEvent>(TEvent evt) where TEvent : struct
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out List<Delegate> list) || list.Count == 0)
                return;

            // Rent the buffer for the current nesting depth: a handler that
            // publishes another event gets its own buffer one level deeper.
            if (_publishDepth == _bufferPool.Count)
                _bufferPool.Add(new List<Delegate>(16));

            List<Delegate> buffer = _bufferPool[_publishDepth];
            _publishDepth++;

            try
            {
                // Snapshot so handlers may mutate subscriptions mid-publish.
                buffer.Clear();
                buffer.AddRange(list);

                for (int i = 0; i < buffer.Count; i++)
                {
                    try
                    {
                        ((Action<TEvent>)buffer[i]).Invoke(evt);
                    }
                    catch (Exception ex)
                    {
                        // One faulty subscriber must never break the others.
                        Debug.LogError($"[EventBus] Handler for {typeof(TEvent).Name} threw: {ex}");
                    }
                }
            }
            finally
            {
                buffer.Clear();
                _publishDepth--;
            }
        }

        #endregion
    }
}
