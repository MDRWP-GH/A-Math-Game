using System.Collections.Generic;
using AMath.Core.Assistance;
using AMath.Core.Assistance.Context;
using AMath.Gameplay.Board;
using AMath.Gameplay.Interaction;
using AMath.Gameplay.Players;
using AMath.Managers;

namespace AMath.AI.Context
{
    /// <summary>
    /// Builds the allowed AI context from the local runtime state. Opponent
    /// racks are intentionally represented only by their tile counts.
    /// </summary>
    public sealed class LiveGameContextProvider : IGameContextProvider
    {
        private readonly BoardManager _board;
        private readonly PlayerManager _players;
        private readonly GameManager _game;
        private readonly TurnManager _turns;
        private readonly TurnInputSession _input;

        public LiveGameContextProvider(
            BoardManager board,
            PlayerManager players,
            GameManager game,
            TurnManager turns,
            TurnInputSession input)
        {
            _board = board;
            _players = players;
            _game = game;
            _turns = turns;
            _input = input;
        }

        /// <inheritdoc />
        public GameContextSnapshot Capture()
        {
            var boardCells = new List<TilePlacement>(_board.Grid.PlacedCount);
            _board.Grid.ExportOccupied(boardCells);

            var publicPlayers = new List<PlayerPublicInfo>(_players.Players.Count);
            foreach (PlayerState player in _players.Players)
            {
                publicPlayers.Add(new PlayerPublicInfo
                {
                    PlayerId = player.PlayerId,
                    DisplayName = player.DisplayName,
                    Score = player.Score,
                    IsConnected = player.IsConnected,
                    RackTileCount = player.Rack.Count
                });
            }

            PlayerState local = _players.GetById(_players.LocalPlayerId);
            return new GameContextSnapshot
            {
                BoardCells = boardCells,
                LocalPlayerHand = local == null ? new List<byte>() : new List<byte>(local.Rack),
                Players = publicPlayers,
                LocalPlayerId = _players.LocalPlayerId,
                CurrentPlayerId = _turns.CurrentPlayerId,
                TurnNumber = _turns.TurnNumber,
                MatchPhase = _game.Phase,
                TilesRemainingInBag = _game.BagCount,
                SelectedTileId = _input.SelectedTileId,
                ReplayTurns = new List<ReplayTurnContext>()
            };
        }
    }
}
