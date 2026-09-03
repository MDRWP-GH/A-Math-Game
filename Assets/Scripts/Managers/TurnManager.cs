using AMath.Core;
using AMath.Core.Events;

namespace AMath.Managers
{
    /// <summary>
    /// Owns turn order, turn numbering, the pass counter that ends a match and
    /// the per-turn timer.
    ///
    /// The timer is host-authoritative: only the host ticks it (via
    /// <see cref="TickTimer"/>) and a timeout is converted by GameManager into
    /// a regular, replayable pass command. Clients merely display a countdown
    /// derived from the replicated turn deadline.
    /// </summary>
    public sealed class TurnManager
    {
        #region Fields

        private readonly IEventBus _eventBus;
        private int _playerCount;
        private float _remainingSeconds;

        #endregion

        #region Properties

        /// <summary>1-based number of the turn currently being played.</summary>
        public int TurnNumber { get; private set; }

        /// <summary>Player whose turn it is.</summary>
        public int CurrentPlayerId { get; private set; }

        /// <summary>Consecutive pass/exchange turns across all players.</summary>
        public int ConsecutivePasses { get; private set; }

        /// <summary>Per-turn limit in seconds (from MatchConfig).</summary>
        public int TurnSeconds { get; private set; } = GameRules.DefaultTurnSeconds;

        /// <summary>Seconds remaining in the current turn (host-side clock).</summary>
        public float RemainingSeconds => _remainingSeconds;

        /// <summary>True when every player has passed for the configured number of rounds.</summary>
        public bool ShouldEndByPasses =>
            _playerCount > 0 && ConsecutivePasses >= _playerCount * GameRules.ConsecutivePassRoundsToEnd;

        #endregion

        #region Construction

        public TurnManager(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        #endregion

        #region Match flow

        /// <summary>Resets for a new match and starts turn 1 with player 0.</summary>
        public void StartMatch(int playerCount, int turnSeconds)
        {
            RequireSeats(playerCount);
            _playerCount = playerCount;
            TurnSeconds = turnSeconds;
            TurnNumber = 1;
            CurrentPlayerId = 0;
            ConsecutivePasses = 0;
            BeginTurn();
        }

        /// <summary>
        /// A seatless match cannot be sequenced: <see cref="AdvanceTurn"/> would
        /// divide by zero and take the host down. Rejecting the roster here
        /// turns a corrupt snapshot or malformed config into a catchable load
        /// failure instead of a crash three turns later.
        /// </summary>
        private static void RequireSeats(int playerCount)
        {
            if (playerCount < 1)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(playerCount),
                    playerCount,
                    "A match needs at least one seat.");
            }
        }

        /// <summary>Records how the current turn ended and advances to the next player.</summary>
        public void AdvanceTurn(bool countsAsPass)
        {
            ConsecutivePasses = countsAsPass ? ConsecutivePasses + 1 : 0;
            TurnNumber++;
            CurrentPlayerId = (CurrentPlayerId + 1) % _playerCount;
            BeginTurn();
        }

        private void BeginTurn()
        {
            _remainingSeconds = TurnSeconds;
            _eventBus.Publish(new TurnStartedEvent
            {
                TurnNumber = TurnNumber,
                PlayerId = CurrentPlayerId,
                TurnSeconds = TurnSeconds
            });
        }

        #endregion

        #region Timer (host only)

        /// <summary>
        /// Ticks the turn clock. Returns true exactly once when the current
        /// turn just timed out. Only the host calls this.
        /// </summary>
        public bool TickTimer(float deltaTime)
        {
            if (_remainingSeconds <= 0f) return false;
            _remainingSeconds -= deltaTime;
            return _remainingSeconds <= 0f;
        }

        /// <summary>
        /// Grants the current player a full clock again. Called when the match
        /// resumes from a pause so the enforced timeout matches the fresh
        /// deadline that is replicated to clients.
        /// </summary>
        public void ResetClock() => _remainingSeconds = TurnSeconds;

        #endregion

        #region Snapshot support

        /// <summary>Restores turn state from a snapshot.</summary>
        public void Restore(int playerCount, int turnSeconds, int turnNumber, int currentPlayerId, int consecutivePasses)
        {
            RequireSeats(playerCount);
            _playerCount = playerCount;
            TurnSeconds = turnSeconds;
            TurnNumber = turnNumber;
            CurrentPlayerId = currentPlayerId;
            ConsecutivePasses = consecutivePasses;
            _remainingSeconds = turnSeconds;
        }

        #endregion
    }
}
