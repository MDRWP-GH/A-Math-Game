using System.Collections.Generic;

namespace AMath.Core.Assistance
{
    /// <summary>
    /// Read-only view over the player roster. The local player's own hand is
    /// exposed in full (it is their own information); every other player is
    /// exposed only through <see cref="PlayerPublicInfo"/>, which cannot carry
    /// a rack. This asymmetry is what keeps opponents' tiles out of reach of
    /// both the Tutorial and the AI Assistant.
    /// </summary>
    public interface IPlayerStateReader
    {
        /// <summary>PlayerId of the local player on this machine; -1 when unknown.</summary>
        int LocalPlayerId { get; }

        /// <summary>The local player's own rack tiles (tile ids from <see cref="Gameplay.Board.AMathTileSet"/>).</summary>
        IReadOnlyList<byte> GetLocalHand();

        /// <summary>Public info for every seated player, in turn order.</summary>
        IReadOnlyList<PlayerPublicInfo> GetPublicPlayers();
    }
}
