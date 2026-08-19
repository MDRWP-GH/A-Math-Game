using System;
using System.Collections.Generic;
using UnityEngine;

namespace AMath.Core.StateMachines
{
    /// <summary>A single state inside a <see cref="StateMachine{TKey}"/>.</summary>
    public interface IState
    {
        /// <summary>Called when the state becomes active.</summary>
        void Enter();

        /// <summary>Called when the state stops being active.</summary>
        void Exit();

        /// <summary>Called every frame while active.</summary>
        void Tick(float deltaTime);
    }

    /// <summary>
    /// Generic, table-driven finite state machine.
    /// Transitions must be declared explicitly; an undeclared transition is a
    /// programming error and is rejected loudly, which prevents a live match
    /// from silently entering an inconsistent phase.
    /// </summary>
    /// <typeparam name="TKey">Enum identifying states.</typeparam>
    public sealed class StateMachine<TKey> where TKey : struct, Enum
    {
        #region Fields

        private readonly Dictionary<TKey, IState> _states = new();
        private readonly HashSet<(TKey from, TKey to)> _allowedTransitions = new();
        private IState _currentState;

        #endregion

        #region Properties / Events

        /// <summary>Currently active state key.</summary>
        public TKey CurrentKey { get; private set; }

        /// <summary>Raised after a transition completes: (previous, current).</summary>
        public event Action<TKey, TKey> StateChanged;

        #endregion

        #region Configuration

        /// <summary>Registers a state implementation for a key.</summary>
        public StateMachine<TKey> AddState(TKey key, IState state)
        {
            _states[key] = state ?? throw new ArgumentNullException(nameof(state));
            return this;
        }

        /// <summary>Declares a legal transition.</summary>
        public StateMachine<TKey> AllowTransition(TKey from, TKey to)
        {
            _allowedTransitions.Add((from, to));
            return this;
        }

        /// <summary>Sets the initial state without transition validation.</summary>
        public void Start(TKey initial)
        {
            CurrentKey = initial;
            _currentState = _states[initial];
            _currentState.Enter();
        }

        #endregion

        #region Runtime

        /// <summary>
        /// Attempts a transition. Returns false (and logs) when the transition
        /// is not declared, so callers can react instead of crashing a match.
        /// </summary>
        public bool TransitionTo(TKey next)
        {
            if (EqualityComparer<TKey>.Default.Equals(CurrentKey, next))
                return true;

            if (!_allowedTransitions.Contains((CurrentKey, next)))
            {
                Debug.LogError($"[StateMachine] Illegal transition {CurrentKey} -> {next} rejected.");
                return false;
            }

            TKey previous = CurrentKey;
            _currentState?.Exit();
            CurrentKey = next;
            _currentState = _states[next];
            _currentState.Enter();
            StateChanged?.Invoke(previous, next);
            return true;
        }

        /// <summary>
        /// Rehydrates the machine from persisted state, bypassing the
        /// transition table. Restoring a snapshot is not a transition: the
        /// phase it names was already reached legally on the machine that
        /// wrote it, and the path back to it (e.g. Lobby -> Playing on a
        /// reconnecting client) is deliberately not a legal live transition.
        /// </summary>
        public void RestoreTo(TKey state)
        {
            if (EqualityComparer<TKey>.Default.Equals(CurrentKey, state))
                return;

            TKey previous = CurrentKey;
            _currentState?.Exit();
            CurrentKey = state;
            _currentState = _states[state];
            _currentState.Enter();
            StateChanged?.Invoke(previous, state);
        }

        /// <summary>Ticks the active state.</summary>
        public void Tick(float deltaTime) => _currentState?.Tick(deltaTime);

        #endregion
    }
}
