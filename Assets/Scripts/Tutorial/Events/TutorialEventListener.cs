using System;
using AMath.Core.Commands;
using AMath.Core.Events;

namespace AMath.Tutorial.Events
{
    /// <summary>
    /// Adapts Core gameplay/UI events into the small vocabulary of
    /// <see cref="TutorialGameplaySignalEvent"/>. This is the sole gameplay
    /// event subscriber in the tutorial runtime, keeping conditions unaware
    /// of gameplay event details and preserving the one-way dependency from
    /// Tutorial to Core.
    /// </summary>
    public sealed class TutorialEventListener : IDisposable
    {
        private readonly IEventBus _eventBus;

        /// <summary>Subscribes to Core events immediately.</summary>
        public TutorialEventListener(IEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _eventBus.Subscribe<BoardLoadedEvent>(OnBoardLoaded);
            _eventBus.Subscribe<TileDraggedEvent>(OnTileDragged);
            _eventBus.Subscribe<ButtonPressedEvent>(OnButtonPressed);
            _eventBus.Subscribe<MenuOpenedEvent>(OnMenuOpened);
            _eventBus.Subscribe<TurnResolvedEvent>(OnTurnResolved);
            _eventBus.Subscribe<DraftTilePlacedEvent>(OnDraftTilePlaced);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _eventBus.Unsubscribe<BoardLoadedEvent>(OnBoardLoaded);
            _eventBus.Unsubscribe<TileDraggedEvent>(OnTileDragged);
            _eventBus.Unsubscribe<ButtonPressedEvent>(OnButtonPressed);
            _eventBus.Unsubscribe<MenuOpenedEvent>(OnMenuOpened);
            _eventBus.Unsubscribe<TurnResolvedEvent>(OnTurnResolved);
            _eventBus.Unsubscribe<DraftTilePlacedEvent>(OnDraftTilePlaced);
        }

        private void OnBoardLoaded(BoardLoadedEvent evt) =>
            Publish(TutorialGameplaySignalKind.BoardLoaded);

        private void OnTileDragged(TileDraggedEvent evt) =>
            Publish(TutorialGameplaySignalKind.TileDragged, tileId: evt.TileId);

        private void OnButtonPressed(ButtonPressedEvent evt) =>
            Publish(TutorialGameplaySignalKind.ButtonPressed, evt.ButtonId);

        private void OnMenuOpened(MenuOpenedEvent evt) =>
            Publish(TutorialGameplaySignalKind.MenuOpened, evt.MenuId);

        private void OnTurnResolved(TurnResolvedEvent evt)
        {
            CommandType commandType = (CommandType)evt.Record.CommandType;
            Publish(TutorialGameplaySignalKind.TurnEnded, commandType: commandType);
        }

        private void OnDraftTilePlaced(DraftTilePlacedEvent evt) =>
            Publish(
                TutorialGameplaySignalKind.TilePlacedOnBoard,
                targetId: $"{evt.X},{evt.Y}",
                tileId: evt.TileId);

        private void Publish(
            TutorialGameplaySignalKind kind,
            string targetId = null,
            byte tileId = 0,
            CommandType? commandType = null)
        {
            _eventBus.Publish(new TutorialGameplaySignalEvent
            {
                Kind = kind,
                TargetId = targetId,
                TileId = tileId,
                CommandType = commandType
            });
        }
    }
}
