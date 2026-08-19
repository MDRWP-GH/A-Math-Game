using AMath.Networking;
using AMath.Networking.HostMigration;
using AMath.Networking.Messages;
using AMath.Networking.RPC;
using AMath.Networking.Transport;
using kcp2k;
using Mirror;
using UnityEngine;

namespace AMath.Bootstrap
{
    /// <summary>
    /// Builds the Mirror stack at runtime when the boot scene has no wired
    /// network objects yet (code-driven play flow).
    /// </summary>
    public static class NetworkSessionFactory
    {
        public sealed class BuiltStack
        {
            public AMathNetworkManager NetworkManager;
            public RoomAuthenticator Authenticator;
            public NetworkGameState GameState;
            public MigrationTableSync MigrationTable;
            public GameObject PlayerPrefab;
            public GameObject GameStatePrefab;
            public GameObject MigrationPrefab;
        }

        /// <summary>Creates manager, transport, authenticator and spawnable prefabs.</summary>
        public static BuiltStack Create()
        {
            var root = new GameObject("AMath Network");
            Object.DontDestroyOnLoad(root);

            var transport = root.AddComponent<KcpTransport>();
            TransportConfigurator.Configure(transport);

            var authenticator = root.AddComponent<RoomAuthenticator>();
            var networkManager = root.AddComponent<AMathNetworkManager>();
            networkManager.transport = transport;
            networkManager.authenticator = authenticator;
            networkManager.autoCreatePlayer = true;
            networkManager.playerPrefab = CreatePlayerPrefab();
            Transport.active = transport;

            var gameStatePrefab = CreateNetworkBehaviourPrefab<NetworkGameState>("NetworkGameStatePrefab");
            var migrationPrefab = CreateNetworkBehaviourPrefab<MigrationTableSync>("MigrationTableSyncPrefab");
            networkManager.spawnPrefabs.Add(gameStatePrefab);
            networkManager.spawnPrefabs.Add(migrationPrefab);

            // Inactive scene stand-ins so NetworkedGameContext can register them;
            // live copies are spawned when the host starts.
            var gameState = Object.Instantiate(gameStatePrefab);
            gameState.name = "NetworkGameState";
            gameState.SetActive(false);
            Object.DontDestroyOnLoad(gameState);

            var migration = Object.Instantiate(migrationPrefab);
            migration.name = "MigrationTableSync";
            migration.SetActive(false);
            Object.DontDestroyOnLoad(migration);

            var spawner = root.AddComponent<HostSpawnedSingletons>();
            spawner.Configure(gameStatePrefab, migrationPrefab);

            return new BuiltStack
            {
                NetworkManager = networkManager,
                Authenticator = authenticator,
                GameState = gameState.GetComponent<NetworkGameState>(),
                MigrationTable = migration.GetComponent<MigrationTableSync>(),
                PlayerPrefab = networkManager.playerPrefab,
                GameStatePrefab = gameStatePrefab,
                MigrationPrefab = migrationPrefab
            };
        }

        private static GameObject CreatePlayerPrefab()
        {
            var prefab = new GameObject("NetworkPlayerPrefab");
            prefab.AddComponent<NetworkIdentity>();
            prefab.AddComponent<NetworkPlayer>();
            prefab.SetActive(false);
            Object.DontDestroyOnLoad(prefab);
            return prefab;
        }

        private static GameObject CreateNetworkBehaviourPrefab<T>(string name) where T : NetworkBehaviour
        {
            var prefab = new GameObject(name);
            prefab.AddComponent<NetworkIdentity>();
            prefab.AddComponent<T>();
            prefab.SetActive(false);
            Object.DontDestroyOnLoad(prefab);
            return prefab;
        }

        /// <summary>Spawns the singleton networked state objects once the host is up.</summary>
        private sealed class HostSpawnedSingletons : MonoBehaviour
        {
            private GameObject _gameStatePrefab;
            private GameObject _migrationPrefab;
            private bool _spawned;

            public void Configure(GameObject gameStatePrefab, GameObject migrationPrefab)
            {
                _gameStatePrefab = gameStatePrefab;
                _migrationPrefab = migrationPrefab;
            }

            private void Update()
            {
                // Re-arm whenever the server goes down so a second CreateRoom in
                // the same play session spawns a fresh pair. This object is
                // DontDestroyOnLoad and never disabled, so OnDisable alone would
                // leave the flag latched for the rest of the process.
                if (!NetworkServer.active)
                {
                    _spawned = false;
                    return;
                }

                if (_spawned) return;
                _spawned = true;

                GameObject gameStateObject = Object.Instantiate(_gameStatePrefab);
                gameStateObject.SetActive(true);
                NetworkServer.Spawn(gameStateObject);

                GameObject migrationObject = Object.Instantiate(_migrationPrefab);
                migrationObject.SetActive(true);
                NetworkServer.Spawn(migrationObject);

                if (NetworkContext.Services != null)
                {
                    NetworkContext.Services.RegisterOrReplace(gameStateObject.GetComponent<NetworkGameState>());
                    NetworkContext.Services.RegisterOrReplace(migrationObject.GetComponent<MigrationTableSync>());
                }
            }

            private void OnDisable()
            {
                _spawned = false;
            }
        }
    }
}
