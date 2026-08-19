using System;
using AMath.Core.Events;

namespace AMath.Core.StateMachines
{
    /// <summary>
    /// The game's phase state machine:
    /// Lobby → Loading → Playing ⇄ Paused → Finished.
    ///
    /// Phase changes are published on the event bus so networking, UI, save and
    /// replay systems react without referencing each other. On clients the
    /// machine is driven by the replicated <see cref="MatchPhase"/> SyncVar; on
    /// the host it is driven by game logic — either way, all peers pass through
    /// the same validated transitions.
    /// </summary>
    public sealed class GameStateMachine : ITickable
    {
        #region Fields

        private readonly StateMachine<MatchPhase> _machine = new();
        private readonly IEventBus _eventBus;

        #endregion

        #region Construction

        /// <summary>Builds the machine with the complete legal transition table.</summary>
        public GameStateMachine(IEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            _machine
                .AddState(MatchPhase.Lobby, new PhaseState())
                .AddState(MatchPhase.Loading, new PhaseState())
                .AddState(MatchPhase.Playing, new PhaseState())
                .AddState(MatchPhase.Paused, new PhaseState())
                .AddState(MatchPhase.Finished, new PhaseState())
                // Normal flow
                .AllowTransition(MatchPhase.Lobby, MatchPhase.Loading)
                .AllowTransition(MatchPhase.Loading, MatchPhase.Playing)
                .AllowTransition(MatchPhase.Playing, MatchPhase.Paused)
                .AllowTransition(MatchPhase.Paused, MatchPhase.Playing)
                .AllowTransition(MatchPhase.Playing, MatchPhase.Finished)
                // Connection loss / host migration paths
                .AllowTransition(MatchPhase.Paused, MatchPhase.Loading)   // resync after reconnection
                .AllowTransition(MatchPhase.Loading, MatchPhase.Paused)   // migrated host waits for players
                .AllowTransition(MatchPhase.Paused, MatchPhase.Finished)  // players chose to end while waiting
                .AllowTransition(MatchPhase.Lobby, MatchPhase.Paused)     // reconnect flow entered from menu
                // Post-match
                .AllowTransition(MatchPhase.Finished, MatchPhase.Lobby);  // back to room for a rematch

            _machine.StateChanged += OnStateChanged;
            _machine.Start(MatchPhase.Lobby);
        }

        #endregion

        #region Public API

        /// <summary>Currently active phase.</summary>
        public MatchPhase CurrentPhase => _machine.CurrentKey;

        /// <summary>Requests a phase change; returns false when illegal.</summary>
        public bool TransitionTo(MatchPhase phase) => _machine.TransitionTo(phase);

        /// <summary>
        /// Adopts a phase carried by a snapshot (save load, host migration,
        /// reconnection resync) without running it through the live transition
        /// table. Still publishes <see cref="MatchPhaseChangedEvent"/>.
        /// </summary>
        public void RestoreTo(MatchPhase phase) => _machine.RestoreTo(phase);

        /// <inheritdoc />
        public void Tick(float deltaTime) => _machine.Tick(deltaTime);

        #endregion

        #region Internals

        private void OnStateChanged(MatchPhase previous, MatchPhase current)
        {
            _eventBus.Publish(new MatchPhaseChangedEvent { Previous = previous, Current = current });
        }

        /// <summary>
        /// Phases carry no per-frame behaviour of their own (turn timing lives in
        /// TurnManager, which checks the phase); a shared empty state keeps the
        /// machine strictly about legal transitions.
        /// </summary>
        private sealed class PhaseState : IState
        {
            public void Enter() { }
            public void Exit() { }
            public void Tick(float deltaTime) { }
        }

        #endregion
    }
}
