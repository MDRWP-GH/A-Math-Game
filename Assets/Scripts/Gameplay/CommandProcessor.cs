using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Commands;
using AMath.Core.RandomNumbers;
using AMath.Gameplay.Board;
using AMath.Gameplay.Players;
using AMath.Managers;

namespace AMath.Gameplay
{
    /// <summary>Result of executing one command through the pipeline.</summary>
    public sealed class CommandOutcome
    {
        public bool Success;
        public string Error;
        /// <summary>Points awarded (placements only).</summary>
        public int ScoreDelta;
        /// <summary>True for pass/exchange (feeds the match-end pass counter).</summary>
        public bool CountsAsPass;
    }

    /// <summary>
    /// Validates and executes player commands against the domain state.
    ///
    /// The exact same code runs in three contexts, which is what guarantees
    /// consistency across the whole architecture:
    ///  - on the host, for authoritative validation of client requests;
    ///  - on clients, re-applying host-accepted records to their local mirror;
    ///  - in the replay reconstructor, rebuilding a match from its event log.
    /// </summary>
    public sealed class CommandProcessor
    {
        #region Fields

        private readonly BoardManager _boardManager;
        private readonly PlayerManager _playerManager;
        private readonly TurnManager _turnManager;
        private readonly TileBag _tileBag;

        // Reused buffers to avoid per-turn allocations.
        private readonly List<byte> _drawBuffer = new(GameRules.RackSize);
        private readonly List<byte> _removeBuffer = new(GameRules.RackSize);

        #endregion

        #region Construction

        public CommandProcessor(BoardManager boardManager, PlayerManager playerManager, TurnManager turnManager, TileBag tileBag)
        {
            _boardManager = boardManager;
            _playerManager = playerManager;
            _turnManager = turnManager;
            _tileBag = tileBag;
        }

        #endregion

        #region Execution

        /// <summary>
        /// Validates and, when valid, executes <paramref name="command"/> for
        /// <paramref name="playerId"/>. Never mutates state on failure.
        /// </summary>
        public CommandOutcome Execute(int playerId, IGameCommand command, DeterministicRandom rng)
        {
            // Turn ownership is the first line of defense for every command.
            if (_turnManager.CurrentPlayerId != playerId)
                return Fail("It is not your turn.");

            PlayerState player = _playerManager.GetById(playerId);
            if (player == null)
                return Fail("Unknown player.");

            switch (command)
            {
                case PlaceTilesCommand place:
                    return ExecutePlace(player, place, rng);
                case ExchangeTilesCommand exchange:
                    return ExecuteExchange(player, exchange, rng);
                case PassTurnCommand:
                    return new CommandOutcome { Success = true, CountsAsPass = true };
                default:
                    return Fail("Unknown command.");
            }
        }

        private CommandOutcome ExecutePlace(PlayerState player, PlaceTilesCommand command, DeterministicRandom rng)
        {
            PlacementValidation validation = _boardManager.Validate(player.Rack, command.Placements);
            if (!validation.IsValid)
                return Fail(validation.Error);

            int score = _boardManager.Commit(validation, command.Placements);
            player.Score += score;

            // Remove used tiles, then refill from the bag (deterministic draw).
            _removeBuffer.Clear();
            foreach (TilePlacement placement in command.Placements)
                _removeBuffer.Add(placement.TileId);
            _playerManager.RemoveFromRack(player.PlayerId, _removeBuffer);

            _drawBuffer.Clear();
            _tileBag.Draw(GameRules.RackSize - player.Rack.Count, _drawBuffer);
            _playerManager.AddToRack(player.PlayerId, _drawBuffer);

            return new CommandOutcome { Success = true, ScoreDelta = score, CountsAsPass = false };
        }

        private CommandOutcome ExecuteExchange(PlayerState player, ExchangeTilesCommand command, DeterministicRandom rng)
        {
            if (command.TileIds.Count == 0 || command.TileIds.Count > GameRules.RackSize)
                return Fail("Invalid exchange count.");

            if (_tileBag.Count < command.TileIds.Count)
                return Fail("Not enough tiles left in the bag to exchange.");

            // Multiset ownership check — a client must not exchange tiles it does not hold.
            _removeBuffer.Clear();
            _removeBuffer.AddRange(player.Rack);
            foreach (byte tileId in command.TileIds)
            {
                if (!_removeBuffer.Remove(tileId))
                    return Fail("You do not own those tiles.");
            }

            _drawBuffer.Clear();
            _tileBag.Exchange(command.TileIds, _drawBuffer, rng);
            _playerManager.RemoveFromRack(player.PlayerId, command.TileIds);
            _playerManager.AddToRack(player.PlayerId, _drawBuffer);

            return new CommandOutcome { Success = true, CountsAsPass = true };
        }

        private static CommandOutcome Fail(string error) => new() { Success = false, Error = error };

        #endregion
    }
}
