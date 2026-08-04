using AMath.Core.Assistance;
using AMath.Core.Events;
using AMath.Tutorial.Interfaces;

namespace AMath.Tutorial.Bootstrap
{
    /// <summary>
    /// Concrete dependency bag handed to tutorial actions and conditions.
    /// </summary>
    public sealed class TutorialRuntimeContext : ITutorialRuntimeContext
    {
        public TutorialRuntimeContext(
            IEventBus eventBus,
            IBoardStateReader boardState,
            IPlayerStateReader playerState,
            IMatchStateReader matchState,
            ITutorialHighlightService highlighter,
            ITutorialDialogueService dialogue,
            ITutorialUiService ui)
        {
            EventBus = eventBus;
            BoardState = boardState;
            PlayerState = playerState;
            MatchState = matchState;
            Highlighter = highlighter;
            Dialogue = dialogue;
            Ui = ui;
        }

        /// <inheritdoc />
        public IEventBus EventBus { get; }

        /// <inheritdoc />
        public IBoardStateReader BoardState { get; }

        /// <inheritdoc />
        public IPlayerStateReader PlayerState { get; }

        /// <inheritdoc />
        public IMatchStateReader MatchState { get; }

        /// <inheritdoc />
        public ITutorialHighlightService Highlighter { get; }

        /// <inheritdoc />
        public ITutorialDialogueService Dialogue { get; }

        /// <inheritdoc />
        public ITutorialUiService Ui { get; }
    }
}
