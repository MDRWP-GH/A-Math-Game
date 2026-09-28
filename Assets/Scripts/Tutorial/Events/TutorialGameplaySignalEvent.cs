using AMath.Core.Commands;

namespace AMath.Tutorial.Events
{
    /// <summary>
    /// Tutorial-owned, normalized signal emitted by
    /// <see cref="TutorialEventListener"/>. Conditions subscribe to this
    /// event instead of each subscribing to a growing set of gameplay events.
    /// </summary>
    public struct TutorialGameplaySignalEvent
    {
        /// <summary>Kind of player/game interaction observed.</summary>
        public TutorialGameplaySignalKind Kind;

        /// <summary>Optional stable target id, such as a button or menu id.</summary>
        public string TargetId;

        /// <summary>Optional tile involved in the interaction.</summary>
        public byte TileId;

        /// <summary>Optional command type when the signal came from a resolved turn.</summary>
        public CommandType? CommandType;
    }

    /// <summary>Interactions that authored tutorial conditions can wait for.</summary>
    public enum TutorialGameplaySignalKind
    {
        /// <summary>The board scene finished loading.</summary>
        BoardLoaded = 0,

        /// <summary>The local player dragged a tile.</summary>
        TileDragged = 1,

        /// <summary>A turn resolved, regardless of command type.</summary>
        TurnEnded = 2,

        /// <summary>A registered UI button was pressed.</summary>
        ButtonPressed = 3,

        /// <summary>A registered menu or panel was opened.</summary>
        MenuOpened = 4,

        /// <summary>The local player placed a tile on the board draft.</summary>
        TilePlacedOnBoard = 5
    }
}
