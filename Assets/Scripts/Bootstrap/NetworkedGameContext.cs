using AMath.Core;
using AMath.Core.Events;
using AMath.Core.StateMachines;
using AMath.Gameplay.Board;
using AMath.Gameplay.Players;
using AMath.Managers;
using AMath.Networking;
using AMath.Networking.Discovery;
using AMath.Networking.HostMigration;
using AMath.Networking.Messages;
using AMath.Networking.Room;
using AMath.Networking.RPC;
using AMath.Replay;
using AMath.Save;
using UnityEngine;

namespace AMath.Bootstrap
{
    /// <summary>
    /// The single composition root. Every manager is a plain C# class created
    /// and wired HERE, with dependencies passed through constructors — no
    /// singletons, no scattered FindObjectOfType, no hidden coupling. Scene
    /// components (NetworkManager, authenticator, replicated scene objects)
    /// are injected via serialized references and Configure() calls.
    ///
    /// Scene setup (one object each, see project docs):
    ///  - AMathNetworkManager + KcpTransport + RoomAuthenticator on one object;
    ///  - NetworkGameState and MigrationTableSync on scene NetworkIdentity objects;
    ///  - this component anywhere in the boot scene.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class NetworkedGameContext : MonoBehaviour
    {
        #region Scene references

        [Header("Scene components")]
        [SerializeField] private AMathNetworkManager _networkManager;
        [SerializeField] private RoomAuthenticator _authenticator;
        [SerializeField] private NetworkGameState _networkGameState;
        [SerializeField] private MigrationTableSync _migrationTableSync;

        #endregion

        #region Fields

        private ServiceRegistry _services;

        #endregion

        #region Properties

        /// <summary>Resolved services (for presenters living in the same scene).</summary>
        public ServiceRegistry Services => _services;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _services = new ServiceRegistry();

            // --- Core (no networking dependencies) -----------------------
            IEventBus bus = _services.Register<IEventBus>(new EventBus());
            var stateMachine = _services.Register(new GameStateMachine(bus));
            var boardManager = _services.Register(new BoardManager());
            var playerManager = _services.Register(new PlayerManager(bus));
            var turnManager = _services.Register(new TurnManager(bus));
            var gameManager = _services.Register(new GameManager(bus, stateMachine, boardManager, playerManager, turnManager));
            var replayManager = _services.Register(new ReplayManager(bus));
            var saveManager = _services.Register(new SaveManager(bus, gameManager, replayManager));

            // --- Networking ----------------------------------------------
            var session = _services.Register(new RoomSession());
            var discovery = _services.Register(new DiscoveryManager(bus));
            var roomManager = _services.Register(new RoomManager(bus, session, discovery, gameManager, stateMachine, _networkManager));
            var migrationManager = _services.Register(new HostMigrationManager(bus, discovery, roomManager, saveManager, replayManager, playerManager, session));
            _services.Register(new ReconnectionManager(bus, stateMachine, gameManager, playerManager, saveManager, migrationManager, session));

            // Scene components: registered for spawn-time resolution and injected.
            _services.Register(_networkManager);
            _services.Register(_networkGameState);
            _networkManager.Configure(bus, playerManager, gameManager, session);
            _authenticator.Configure(session, playerManager, gameManager);

            // Saves must always carry current room metadata for re-hosting.
            bus.Subscribe<HostStartedEvent>(_ => CopySessionToSaves(session, saveManager));
            bus.Subscribe<ClientConnectedEvent>(_ => CopySessionToSaves(session, saveManager));

            // Spawned network objects resolve their services through this bridge.
            NetworkContext.Install(_services);
        }

        private void Update()
        {
            // Single tick loop for all ITickable managers
            // (GameManager timer, state machine, discovery, migration).
            _services.TickAll(Time.deltaTime);
        }

        private void OnDestroy()
        {
            NetworkContext.Clear();
            _services?.Dispose();
            _services = null;
        }

        #endregion

        #region Helpers

        private static void CopySessionToSaves(RoomSession session, SaveManager saveManager)
        {
            saveManager.RoomName = session.RoomName;
            saveManager.RoomCode = session.RoomCode;
            saveManager.MaxPlayers = session.MaxPlayers;
        }

        #endregion
    }
}
