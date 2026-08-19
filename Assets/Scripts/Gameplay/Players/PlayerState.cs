using System.Collections.Generic;
using AMath.Core;

namespace AMath.Gameplay.Players
{
    /// <summary>
    /// Authoritative runtime state of one seated player.
    /// Racks exist on every peer (reconstructed deterministically) but the UI
    /// only ever shows the local player's rack; the host is the only peer whose
    /// copy is treated as official.
    /// </summary>
    public sealed class PlayerState
    {
        /// <summary>Match-local id; also the turn order index.</summary>
        public int PlayerId { get; }

        /// <summary>Stable per-installation GUID used to re-identify the player after a disconnect.</summary>
        public string PersistentGuid { get; }

        /// <summary>Display name.</summary>
        public string DisplayName { get; }

        /// <summary>Current official score. Mutated only by the command pipeline.</summary>
        public int Score { get; set; }

        /// <summary>Tiles currently on the rack.</summary>
        public List<byte> Rack { get; } = new(GameRules.RackSize);

        /// <summary>Live connection state (transient; not part of saves).</summary>
        public bool IsConnected { get; set; } = true;

        /// <summary>True when the host's scripted AI plays this seat.</summary>
        public bool IsAi { get; }

        public PlayerState(int playerId, string persistentGuid, string displayName, bool isAi = false)
        {
            PlayerId = playerId;
            PersistentGuid = persistentGuid;
            DisplayName = displayName;
            IsAi = isAi;
        }
    }
}
