using System.Collections.Generic;
using AMath.Core.Assistance;
using AMath.Core.Assistance.Context;
using AMath.Core.Commands;
using AMath.Gameplay.Board;
using AMath.Gameplay.Interaction;
using AMath.Gameplay.Players;
using AMath.Managers;
using AMath.Replay;

namespace AMath.AI.Context
{
    /// <summary>
    /// Builds the allowed AI context from the local runtime state. Opponent
    /// racks are intentionally represented only by their tile counts.
    /// </summary>
    public sealed class LiveGameContextProvider : IGameContextProvider
    {
        /// <summary>
        /// Most recent turns handed to Replay Coach. The whole history would
        /// grow the prompt without bound as a match runs long, and the recent
        /// turns are the ones a coaching answer is actually about.
        /// </summary>
        private const int MaxReplayTurns = 20;

        private readonly BoardManager _board;
        private readonly PlayerManager _players;
        private readonly GameManager _game;
        private readonly TurnManager _turns;
        private readonly TurnInputSession _input;
        private readonly ICommandRejectionReader _rejections;
        private readonly ITutorialProgressReader _tutorial;
        private readonly ReplayManager _replay;

        public LiveGameContextProvider(
            BoardManager board,
            PlayerManager players,
            GameManager game,
            TurnManager turns,
            TurnInputSession input,
            ICommandRejectionReader rejections = null,
            ITutorialProgressReader tutorial = null,
            ReplayManager replay = null)
        {
            _board = board;
            _players = players;
            _game = game;
            _turns = turns;
            _input = input;
            _rejections = rejections;
            _tutorial = tutorial;
            _replay = replay;
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
            bool tutorialActive = _tutorial != null && _tutorial.IsTutorialActive;

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
                LastCommandRejectionReason = ResolveRejectionReason(),
                IsTutorialActive = tutorialActive,
                CurrentTutorialStepId = tutorialActive ? DescribeTutorialStep() : null,
                CurrentObjectiveText = tutorialActive ? _tutorial.CurrentObjectiveText : null,
                ReplayTurns = CaptureRecentTurns()
            };
        }

        /// <summary>
        /// A rejection the host sent back outranks a local preview error: it is
        /// the one the player just saw. Otherwise fall back to why the current
        /// draft would not be accepted.
        /// </summary>
        private string ResolveRejectionReason()
        {
            string hostReason = _rejections?.LastRejectionReason;
            if (!string.IsNullOrWhiteSpace(hostReason))
                return hostReason;

            PlacementValidation preview = _input.PreviewValidation;
            return preview is { IsValid: false } ? preview.Error : null;
        }

        private string DescribeTutorialStep()
        {
            if (!string.IsNullOrWhiteSpace(_tutorial.CurrentStepId))
                return _tutorial.CurrentStepId;

            return _tutorial.ActiveTutorialId == null
                ? null
                : $"{_tutorial.ActiveTutorialId}#{_tutorial.CurrentStepIndex}";
        }

        private List<ReplayTurnContext> CaptureRecentTurns()
        {
            var turns = new List<ReplayTurnContext>();
            List<ReplayEvent> events = _replay?.Log?.Events;
            if (events == null || events.Count == 0)
                return turns;

            int start = events.Count > MaxReplayTurns ? events.Count - MaxReplayTurns : 0;
            for (int i = start; i < events.Count; i++)
            {
                ReplayEvent recorded = events[i];
                turns.Add(new ReplayTurnContext
                {
                    TurnNumber = recorded.Turn,
                    PlayerId = recorded.PlayerId,
                    CommandType = (CommandType)recorded.CommandType,
                    ScoreDelta = recorded.ScoreDelta
                });
            }

            return turns;
        }
    }
}
