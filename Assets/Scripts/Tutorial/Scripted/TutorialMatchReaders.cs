using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Assistance;
using AMath.Core.StateMachines;
using AMath.Gameplay.Board;
using AMath.Gameplay.Players;
using AMath.Managers;

namespace AMath.Tutorial.Scripted
{
    /// <summary>
    /// Live read-only gameplay views for the tutorial match. Replaces the
    /// empty stub readers now that the tutorial scene owns a real board.
    /// </summary>
    public sealed class TutorialMatchReaders : IBoardStateReader, IPlayerStateReader, IMatchStateReader
    {
        private readonly BoardManager _board;
        private readonly PlayerManager _players;
        private readonly GameManager _game;
        private readonly TurnManager _turns;
        private readonly CommandRejectionTracker _rejections;
        private readonly List<TilePlacement> _occupiedBuffer = new();
        private readonly List<PlayerPublicInfo> _publicBuffer = new();

        public TutorialMatchReaders(
            BoardManager board,
            PlayerManager players,
            GameManager game,
            TurnManager turns,
            CommandRejectionTracker rejections)
        {
            _board = board;
            _players = players;
            _game = game;
            _turns = turns;
            _rejections = rejections;
        }

        /// <inheritdoc />
        public int BoardSize => GameRules.BoardSize;

        /// <inheritdoc />
        public IReadOnlyList<TilePlacement> GetOccupiedCells()
        {
            _occupiedBuffer.Clear();
            _board.Grid.ExportOccupied(_occupiedBuffer);
            return _occupiedBuffer;
        }

        /// <inheritdoc />
        public int LocalPlayerId => _players.LocalPlayerId;

        /// <inheritdoc />
        public IReadOnlyList<byte> GetLocalHand()
        {
            PlayerState local = _players.GetById(_players.LocalPlayerId);
            return local != null ? local.Rack : System.Array.Empty<byte>();
        }

        /// <inheritdoc />
        public IReadOnlyList<PlayerPublicInfo> GetPublicPlayers()
        {
            _publicBuffer.Clear();
            foreach (PlayerState player in _players.Players)
            {
                _publicBuffer.Add(new PlayerPublicInfo
                {
                    PlayerId = player.PlayerId,
                    DisplayName = player.DisplayName,
                    Score = player.Score,
                    IsConnected = player.IsConnected,
                    RackTileCount = player.Rack.Count
                });
            }

            return _publicBuffer;
        }

        /// <inheritdoc />
        public MatchPhase Phase => _game.Phase;

        /// <inheritdoc />
        public int TurnNumber => _turns.TurnNumber;

        /// <inheritdoc />
        public int CurrentPlayerId => _turns.CurrentPlayerId;

        /// <inheritdoc />
        public int TilesRemainingInBag => _game.BagCount;

        /// <inheritdoc />
        public string LastCommandRejectionReason => _rejections.LastRejectionReason;
    }
}
