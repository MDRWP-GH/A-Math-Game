using System;
using System.Collections.Generic;

namespace AMath.Core
{
    /// <summary>
    /// Object that needs a per-frame update but is not a MonoBehaviour.
    /// The composition root drives all tickables from a single Update loop,
    /// which keeps managers as plain, testable C# classes.
    /// </summary>
    public interface ITickable
    {
        /// <summary>Called once per frame by the composition root.</summary>
        void Tick(float deltaTime);
    }

    /// <summary>
    /// Minimal dependency-injection container used by the composition root.
    /// Deliberately tiny: explicit registration, constructor injection at the
    /// root, no reflection magic and no global static access — this is how the
    /// project avoids singleton abuse.
    /// </summary>
    public sealed class ServiceRegistry : IDisposable
    {
        #region Fields

        private readonly Dictionary<Type, object> _services = new();
        private readonly List<ITickable> _tickables = new();

        #endregion

        #region Registration / Resolution

        /// <summary>Registers a service instance for interface/class <typeparamref name="TService"/>.</summary>
        public TService Register<TService>(TService instance) where TService : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (_services.ContainsKey(typeof(TService)))
                throw new InvalidOperationException($"Service {typeof(TService).Name} is already registered.");

            _services.Add(typeof(TService), instance);
            if (instance is ITickable tickable && !_tickables.Contains(tickable))
                _tickables.Add(tickable);
            return instance;
        }

        /// <summary>Resolves a previously registered service. Throws when missing (fail fast).</summary>
        public TService Resolve<TService>() where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out object service))
                return (TService)service;
            throw new InvalidOperationException($"Service {typeof(TService).Name} is not registered.");
        }

        /// <summary>Attempts to resolve a service without throwing.</summary>
        public bool TryResolve<TService>(out TService service) where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out object obj))
            {
                service = (TService)obj;
                return true;
            }

            service = null;
            return false;
        }

        #endregion

        #region Lifecycle

        /// <summary>Ticks every registered <see cref="ITickable"/>. Driven by the composition root.</summary>
        public void TickAll(float deltaTime)
        {
            for (int i = 0; i < _tickables.Count; i++)
                _tickables[i].Tick(deltaTime);
        }

        /// <summary>Disposes all registered services that implement <see cref="IDisposable"/>.</summary>
        public void Dispose()
        {
            foreach (object service in _services.Values)
            {
                if (service is IDisposable disposable && !ReferenceEquals(disposable, this))
                    disposable.Dispose();
            }

            _services.Clear();
            _tickables.Clear();
        }

        #endregion
    }
}
