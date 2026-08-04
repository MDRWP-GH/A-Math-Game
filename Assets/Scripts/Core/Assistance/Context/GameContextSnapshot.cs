using System.Collections.Generic;
using AMath.Core.StateMachines;
using AMath.Gameplay.Board;

namespace AMath.Core.Assistance.Context
{
    /// <summary>
    /// A single, immutable-by-convention snapshot of everything the AI
    /// Assistant is allowed to see at the moment a question is asked:
    /// board, local hand, turn, objective, tutorial step, score, match
    /// state, selected tile and the last rule-relevant rejection.
    ///
    /// Built fresh by <see cref="IGameContextProvider.Capture"/> for every
    /// question — never cached, never subscribed to — so an answer is always
    /// based on the current state and can never go stale mid-conversation.
    /// Every field here is safe to show the asking player; hidden
    /// information (opponents' racks, bag contents) is excluded by
    /// construction, not filtered afterwards.
    /// </summary>
    public sealed class GameContextSnapshot
    {
        /// <summary>Occupied board cells.</summary>
        public IReadOnlyList<TilePlacement> BoardCells;

        /// <summary>The local player's own rack tiles.</summary>
        public IReadOnlyList<byte> LocalPlayerHand;

        /// <summary>Public info for every seated player (never includes racks).</summary>
        public IReadOnlyList<PlayerPublicInfo> Players;

        /// <summary>PlayerId of the local player asking the question.</summary>
        public int LocalPlayerId;

        /// <summary>PlayerId whose turn it currently is.</summary>
        public int CurrentPlayerId;

        /// <summary>1-based current turn number.</summary>
        public int TurnNumber;

        /// <summary>Current match phase.</summary>
        public MatchPhase MatchPhase;

        /// <summary>Tiles remaining in the shared bag.</summary>
        public int TilesRemainingInBag;

        /// <summary>Tile currently selected/held by the local player, if any.</summary>
        public byte? SelectedTileId;

        /// <summary>Reason the local player's last command was rejected, if any.</summary>
        public string LastCommandRejectionReason;

        /// <summary>True while a scripted tutorial is active.</summary>
        public bool IsTutorialActive;

        /// <summary>Id of the active tutorial step, for tutorial-aware answers.</summary>
        public string CurrentTutorialStepId;

        /// <summary>Localized objective text of the active tutorial step.</summary>
        public string CurrentObjectiveText;

        /// <summary>
        /// Public turn history available to Replay Coach. Entries omit raw
        /// command payloads and all hidden historical state.
        /// </summary>
        public IReadOnlyList<ReplayTurnContext> ReplayTurns;
    }
}
