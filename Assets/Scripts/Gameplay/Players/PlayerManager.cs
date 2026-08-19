using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Events;
using AMath.Core.Snapshot;

namespace AMath.Gameplay.Players
{
    /// <summary>
    /// Owns the roster of seated players for the current match: identity,
    /// scores, racks and connection flags. Pure domain class — the networking
    /// layer maps connections to <see cref="PlayerState.PlayerId"/> and this
    /// manager never sees a socket.
    /// </summary>
    public sealed class PlayerManager
    {
        #region Fields

        private readonly IEventBus _eventBus;
        private readonly List<PlayerState> _players = new(GameRules.MaxPlayers);

        #endregion

        #region Properties

        /// <summary>Players in turn order.</summary>
        public IReadOnlyList<PlayerState> Players => _players;

        /// <summary>PlayerId of the local player on this machine (set by the network layer). -1 when unknown.</summary>
        public int LocalPlayerId { get; set; } = -1;

        #endregion

        #region Construction

        public PlayerManager(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        #endregion

        #region Roster

        /// <summary>Builds the roster from a match config (identical on every peer).</summary>
        public void Setup(MatchConfig config)
        {
            _players.Clear();
            foreach (PlayerIdentity identity in config.Players)
            {
                _players.Add(new PlayerState(
                    identity.PlayerId,
                    identity.PersistentGuid,
                    identity.DisplayName,
                    identity.IsAi));
            }

            _eventBus.Publish(new PlayerRosterChangedEvent());
        }

        /// <summary>Finds a player by match-local id; null when absent.</summary>
        public PlayerState GetById(int playerId)
        {
            if (playerId < 0) return null;

            // Seats are handed out as 0..n-1 (see MatchConfig), so the index is
            // almost always the answer — but the lookup stays keyed on PlayerId
            // so a non-contiguous roster degrades to a search instead of
            // silently returning the wrong player.
            if (playerId < _players.Count && _players[playerId].PlayerId == playerId)
                return _players[playerId];

            return _players.Find(p => p.PlayerId == playerId);
        }

        /// <summary>Finds a player by persistent GUID (reconnection identity).</summary>
        public PlayerState FindByGuid(string persistentGuid) =>
            _players.Find(p => p.PersistentGuid == persistentGuid);

        /// <summary>Marks connection state and notifies listeners (UI, host migration).</summary>
        public void SetConnected(int playerId, bool connected)
        {
            PlayerState player = GetById(playerId);
            if (player == null || player.IsConnected == connected) return;

            player.IsConnected = connected;
            _eventBus.Publish(new PlayerConnectionChangedEvent { PlayerId = playerId, IsConnected = connected });
        }

        #endregion

        #region Rack operations (invoked by the command pipeline only)

        /// <summary>Removes exactly these tiles from a rack. Assumes prior validation.</summary>
        public void RemoveFromRack(int playerId, IReadOnlyList<byte> tileIds)
        {
            PlayerState player = GetById(playerId);
            if (player == null) return;

            for (int i = 0; i < tileIds.Count; i++)
                player.Rack.Remove(tileIds[i]);

            _eventBus.Publish(new LocalRackChangedEvent { PlayerId = playerId });
        }

        /// <summary>Adds drawn tiles to a rack.</summary>
        public void AddToRack(int playerId, IReadOnlyList<byte> tileIds)
        {
            PlayerState player = GetById(playerId);
            if (player == null) return;

            for (int i = 0; i < tileIds.Count; i++)
                player.Rack.Add(tileIds[i]);

            _eventBus.Publish(new LocalRackChangedEvent { PlayerId = playerId });
        }

        #endregion

        #region Snapshot support

        /// <summary>Writes all players into a snapshot.</summary>
        public void ExportTo(GameStateSnapshot snapshot)
        {
            snapshot.Players.Clear();
            foreach (PlayerState player in _players)
            {
                snapshot.Players.Add(new PlayerSnapshot
                {
                    PlayerId = player.PlayerId,
                    PersistentGuid = player.PersistentGuid,
                    DisplayName = player.DisplayName,
                    Score = player.Score,
                    IsAi = player.IsAi,
                    Rack = new List<byte>(player.Rack)
                });
            }
        }

        /// <summary>Restores all players from a snapshot.</summary>
        public void RestoreFrom(GameStateSnapshot snapshot)
        {
            _players.Clear();
            foreach (PlayerSnapshot saved in snapshot.Players)
            {
                var player = new PlayerState(
                    saved.PlayerId,
                    saved.PersistentGuid,
                    saved.DisplayName,
                    saved.IsAi)
                {
                    Score = saved.Score,
                    // Players are considered disconnected until the network
                    // layer re-associates their live connection.
                    // AI seats stay "connected" so migration wait logic treats them as present.
                    IsConnected = saved.IsAi
                };
                player.Rack.AddRange(saved.Rack);
                _players.Add(player);
            }

            _eventBus.Publish(new PlayerRosterChangedEvent());
        }

        #endregion
    }
}
