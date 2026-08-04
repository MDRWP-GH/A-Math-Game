using System;
using System.Collections.Generic;

namespace AMath.Utilities
{
    /// <summary>
    /// Minimal allocation-free object pool for frequently reused objects
    /// (discovery packets, line buffers, UI row views). Not thread-safe by
    /// design — network threads hand data to the main thread first.
    /// </summary>
    public sealed class ObjectPool<T> where T : class
    {
        #region Fields

        private readonly Stack<T> _items;
        private readonly Func<T> _factory;
        private readonly Action<T> _onRelease;

        #endregion

        #region Construction

        /// <param name="factory">Creates a new instance when the pool is empty.</param>
        /// <param name="onRelease">Optional reset hook applied when an item returns.</param>
        /// <param name="initialCapacity">Instances to pre-warm.</param>
        public ObjectPool(Func<T> factory, Action<T> onRelease = null, int initialCapacity = 0)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _onRelease = onRelease;
            _items = new Stack<T>(Math.Max(4, initialCapacity));

            for (int i = 0; i < initialCapacity; i++)
                _items.Push(_factory());
        }

        #endregion

        #region API

        /// <summary>Takes an instance from the pool (or creates one).</summary>
        public T Get() => _items.Count > 0 ? _items.Pop() : _factory();

        /// <summary>Returns an instance to the pool.</summary>
        public void Release(T item)
        {
            if (item == null) return;
            _onRelease?.Invoke(item);
            _items.Push(item);
        }

        #endregion
    }
}
