using AMath.Core.Events;
using AMath.Core.StateMachines;
using AMath.Gameplay.Players;
using AMath.Managers;
using AMath.Networking.Messages;
using AMath.Networking.RPC;
using AMath.Networking.Room;
using Mirror;
using UnityEngine;

namespace AMath.Networking
{
    /// <summary>
    /// Project-specific Mirror <see cref="NetworkManager"/>.
    ///
    /// Deliberately contains no game logic: it only maps connections to seats,
    /// spawns <see cref="NetworkPlayer"/> objects and translates Mirror
    /// callbacks into event-bus events that the rest of the architecture
    /// (room, migration, UI) consumes. Host-authority is established here:
    /// the machine running the server side is the single source of truth.
    /// </summary>
    public sealed class AMathNetworkManager : NetworkManager
    {
        #region Dependencies (injected by the composition root)

        private IEventBus _eventBus;
        private PlayerManager _playerManager;
        private GameManager _gameManager;
        private RoomSession _session;

        /// <summary>Injects domain dependencies. Must be called before any connection.</summary>
        public void Configure(IEventBus eventBus, PlayerManager playerManager, GameManager gameManager, RoomSession session)
        {
            _eventBus = eventBus;
            _playerManager = playerManager;
            _gameManager = gameManager;
            _session = session;
        }

        #endregion

        #region Server callbacks

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            var identity = (AuthenticatedIdentity)conn.authenticationData;

            GameObject playerObject = Instantiate(playerPrefab);
            var player = playerObject.GetComponent<NetworkPlayer>();

            // Reconnections resume their original seat; lobby joins are
            // unseated (-1) until the host starts the match and assigns seats.
            player.ServerInitialize(
                identity.IsReconnection ? identity.ExistingPlayerId : -1,
                identity.DisplayName,
                identity.PersistentGuid);

            NetworkServer.AddPlayerForConnection(conn, playerObject);

            if (identity.IsReconnection)
            {
                _playerManager.SetConnected(identity.ExistingPlayerId, true);

                // Push the full match state to the returning client.
                if (NetworkContext.Services != null
                    && NetworkContext.Services.TryResolve(out NetworkGameState gameState))
                {
                    gameState.ServerSendFullStateTo(conn);
                }
            }
            else
            {
                _eventBus.Publish(new PlayerRosterChangedEvent());
            }
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // During a match the seat is kept so the player can reconnect;
            // only the live-connection flag changes.
            if (conn.identity != null
                && conn.identity.TryGetComponent(out NetworkPlayer player)
                && player.PlayerId >= 0
                && _gameManager.Phase != MatchPhase.Lobby)
            {
                _playerManager.SetConnected(player.PlayerId, false);
            }

            base.OnServerDisconnect(conn);
            _eventBus.Publish(new PlayerRosterChangedEvent());
        }

        #endregion

        #region Host lifecycle

        public override void OnStartHost()
        {
            _session.IsHost = true;
            _gameManager.IsAuthority = true;
            _eventBus.Publish(new HostStartedEvent());
        }

        public override void OnStopHost()
        {
            _session.IsHost = false;
            _gameManager.IsAuthority = false;
            _eventBus.Publish(new HostStoppedEvent());
        }

        #endregion

        #region Client callbacks

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            _eventBus.Publish(new ClientConnectedEvent());
        }

        public override void OnClientDisconnect()
        {
            bool matchWasRunning =
                _gameManager.Config != null
                && _gameManager.Phase != MatchPhase.Lobby
                && _gameManager.Phase != MatchPhase.Finished;

            base.OnClientDisconnect();

            // The reconnection/host-migration pipeline reacts to this event.
            _eventBus.Publish(new ClientDisconnectedEvent { MatchWasRunning = matchWasRunning });
        }

        #endregion
    }
}
