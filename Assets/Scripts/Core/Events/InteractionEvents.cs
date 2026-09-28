namespace AMath.Core.Events
{
    // ------------------------------------------------------------------
    // Generic interaction signals. Published by Gameplay/UI, consumed by
    // whichever systems care (chiefly the Script Tutorial's
    // TutorialEventListener). Declared in Core so a publisher never has to
    // reference the consumer's assembly — the whole point of routing
    // everything through the event bus instead of direct calls.
    // ------------------------------------------------------------------

    /// <summary>The local player started or updated a tile drag (before drop/placement).</summary>
    public struct TileDraggedEvent
    {
        /// <summary>Tile id being dragged.</summary>
        public byte TileId;
    }

    /// <summary>A UI button the tutorial can reference by id was pressed.</summary>
    public struct ButtonPressedEvent
    {
        /// <summary>Stable identifier for the button (not the localized label).</summary>
        public string ButtonId;
    }

    /// <summary>A menu/panel was opened.</summary>
    public struct MenuOpenedEvent
    {
        /// <summary>Stable identifier for the menu/panel.</summary>
        public string MenuId;
    }

    /// <summary>The game board finished loading and is ready to be highlighted/inspected.</summary>
    public struct BoardLoadedEvent { }

    /// <summary>The local player added a tile to their turn draft on the board.</summary>
    public struct DraftTilePlacedEvent
    {
        public int X;
        public int Y;
        public byte TileId;
    }
}
