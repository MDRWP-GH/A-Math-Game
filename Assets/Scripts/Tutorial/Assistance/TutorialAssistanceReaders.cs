using System;
using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Assistance;
using AMath.Core.StateMachines;
using AMath.Gameplay.Board;

namespace AMath.Tutorial.Assistance
{
    /// <summary>
    /// Minimal read-only gameplay views for the standalone tutorial scene
    /// before a live match is wired in.
    /// </summary>
    public sealed class TutorialAssistanceReaders : IBoardStateReader, IPlayerStateReader, IMatchStateReader
    {
        /// <inheritdoc />
        public int BoardSize => GameRules.BoardSize;

        /// <inheritdoc />
        public IReadOnlyList<TilePlacement> GetOccupiedCells() => Array.Empty<TilePlacement>();

        /// <inheritdoc />
        public int LocalPlayerId => 0;

        /// <inheritdoc />
        public IReadOnlyList<byte> GetLocalHand() => Array.Empty<byte>();

        /// <inheritdoc />
        public IReadOnlyList<PlayerPublicInfo> GetPublicPlayers() => Array.Empty<PlayerPublicInfo>();

        /// <inheritdoc />
        public MatchPhase Phase => MatchPhase.Playing;

        /// <inheritdoc />
        public int TurnNumber => 1;

        /// <inheritdoc />
        public int CurrentPlayerId => 0;

        /// <inheritdoc />
        public int TilesRemainingInBag => 0;

        /// <inheritdoc />
        public string LastCommandRejectionReason => null;
    }
}
