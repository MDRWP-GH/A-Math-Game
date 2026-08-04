namespace AMath.Core.Assistance
{
    /// <summary>
    /// Publicly known facts about a player: identity, score, connection.
    /// Deliberately omits <c>Rack</c>. This is the data-level hidden-information
    /// guard described in the architecture — a field that must never reach the
    /// AI or leak through the tutorial simply does not exist on this type,
    /// so there is nothing for a careless caller to forward by accident.
    /// </summary>
    public sealed class PlayerPublicInfo
    {
        /// <summary>Match-local id; also the turn order index.</summary>
        public int PlayerId;

        /// <summary>Display name.</summary>
        public string DisplayName;

        /// <summary>Current official score.</summary>
        public int Score;

        /// <summary>True while this player's connection is live.</summary>
        public bool IsConnected;

        /// <summary>Number of tiles currently on this player's rack (count only, never the tiles).</summary>
        public int RackTileCount;
    }
}
