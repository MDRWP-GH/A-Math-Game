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
        private const uint RuntimePlayerAssetId = 0xA001u;
        private const uint RuntimeGameStateAssetId = 0xA002u;
        private const uint RuntimeMigrationAssetId = 0xA003u;

        #region Dependencies (injected by the composition root)

        private IEventBus _eventBus;
        private PlayerManager _playerManager;
        private GameManager _gameManager;
        private RoomSession _session;
        private GameObject _runtimePlayerPrefab;
        private GameObject _runtimeGameStatePrefab;
        private GameObject _runtimeMigrationPrefab;

        /// <summary>Injects domain dependencies. Must be called before any connection.</summary>
        public void Configure(IEventBus eventBus, PlayerManager playerManager, GameManager gameManager, RoomSession session)
        {
            _eventBus = eventBus;
            _playerManager = playerManager;
            _gameManager = gameManager;
            _session = session;
        }

        public void ConfigureRuntimePrefabs(
            GameObject playerPrefab,
            GameObject gameStatePrefab,
            GameObject migrationPrefab)
        {
            _runtimePlayerPrefab = playerPrefab;
            _runtimeGameStatePrefab = gameStatePrefab;
            _runtimeMigrationPrefab = migrationPrefab;
        }

        #endregion

        #region Server callbacks

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            var identity = (AuthenticatedIdentity)conn.authenticationData;

            GameObject playerObject = Instantiate(_runtimePlayerPrefab);
            var player = playerObject.GetComponent<NetworkPlayer>();

            // Reconnections resume their original seat; lobby joins are
            // unseated (-1) until the host starts the match and assigns seats.
            player.ServerInitialize(
                identity.IsReconnection ? identity.ExistingPlayerId : -1,
                identity.DisplayName,
                identity.PersistentGuid,
                isHost: conn == NetworkServer.localConnection);

            NetworkServer.AddPlayerForConnection(conn, playerObject, RuntimePlayerAssetId);

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
            RefreshNetworkPlayerHostFlags();
            _eventBus.Publish(new HostStartedEvent());
        }

        public override void OnStopHost()
        {
            _session.IsHost = false;
            _gameManager.IsAuthority = false;
            _eventBus.Publish(new HostStoppedEvent());
        }

        #endregion

        #region Host badge replication

        [Server]
        private void RefreshNetworkPlayerHostFlags()
        {
            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            {
                if (connection?.identity == null ||
                    !connection.identity.TryGetComponent(out NetworkPlayer player))
                {
                    continue;
                }

                player.ServerSetIsHost(connection == NetworkServer.localConnection);
            }
        }

        #endregion

        #region Client callbacks

        public override void OnStartClient()
        {
            RegisterRuntimePrefab(_runtimePlayerPrefab, RuntimePlayerAssetId);
            RegisterRuntimePrefab(_runtimeGameStatePrefab, RuntimeGameStateAssetId);
            RegisterRuntimePrefab(_runtimeMigrationPrefab, RuntimeMigrationAssetId);
        }

        public override void OnClientConnect()
        {
            if (!clientLoadedScene)
            {
                if (!NetworkClient.ready)
                    NetworkClient.Ready();

                if (NetworkClient.localPlayer == null)
                    NetworkClient.AddPlayer();
            }

            Debug.Log($"[Network] Connected to {_session?.RoomName ?? networkAddress}.");
            _eventBus.Publish(new ClientConnectedEvent());
        }

        public override void OnClientSceneChanged()
        {
            if (NetworkClient.connection.isAuthenticated && !NetworkClient.ready)
                NetworkClient.Ready();

            if (NetworkClient.connection.isAuthenticated
                && NetworkClient.localPlayer == null)
            {
                NetworkClient.AddPlayer();
            }
        }

        public override void OnClientDisconnect()
        {
            bool matchWasRunning =
                _gameManager.Config != null
                && _gameManager.Phase != MatchPhase.Lobby
                && _gameManager.Phase != MatchPhase.Finished;

            base.OnClientDisconnect();

            // The reconnection pipeline reacts to this event.
            Debug.LogWarning(
                $"[Network] Client disconnected (match running: {matchWasRunning}, RTT: {NetworkTime.rtt * 1000d:0} ms).");
            _eventBus.Publish(new ClientDisconnectedEvent { MatchWasRunning = matchWasRunning });
        }

        #endregion

        private static void RegisterRuntimePrefab(GameObject prefab, uint assetId)
        {
            if (prefab == null)
                return;

            NetworkClient.RegisterPrefab(prefab, assetId);
        }

        public static uint GetRuntimeAssetId<T>() where T : NetworkBehaviour
        {
            if (typeof(T) == typeof(NetworkPlayer))
                return RuntimePlayerAssetId;
            if (typeof(T) == typeof(NetworkGameState))
                return RuntimeGameStateAssetId;
            if (typeof(T) == typeof(AMath.Networking.HostMigration.MigrationTableSync))
                return RuntimeMigrationAssetId;

            throw new System.ArgumentOutOfRangeException(nameof(T), typeof(T), "No runtime asset id registered.");
        }
    }
}
