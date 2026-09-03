using AMath.Core;
using AMath.Core.Assistance;
using AMath.Core.Assistance.Context;
using AMath.Core.Events;
using AMath.Core.StateMachines;
using AMath.AI.Chat;
using AMath.AI.Context;
using AMath.AI.Interfaces;
using AMath.AI.Modes;
using AMath.AI.Modes.RuleAssistant;
using AMath.AI.Modes.StrategyCoach;
using AMath.AI.Restrictions;
using AMath.AI.UI;
using AMath.Gameplay.AI;
using AMath.Gameplay.Board;
using AMath.Gameplay.Interaction;
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
    /// and wired HERE. Missing scene network references are created at runtime
    /// so the code-driven play flow works from an empty SampleScene.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class NetworkedGameContext : MonoBehaviour
    {
        #region Scene references

        [Header("Scene components (optional — auto-built when empty)")]
        [SerializeField] private AMathNetworkManager _networkManager;
        [SerializeField] private RoomAuthenticator _authenticator;
        [SerializeField] private NetworkGameState _networkGameState;
        [SerializeField] private MigrationTableSync _migrationTableSync;

        [Header("Optional AI backend (non-secret config)")]
        [SerializeField] private AiBackendConfig _aiBackendConfig;
        [SerializeField] private AiPromptProfile _ruleAssistantProfile;
        [SerializeField] private AiChatWindow _aiChatWindow;

        #endregion

        #region Fields

        private ServiceRegistry _services;
        private static NetworkedGameContext _instance;

        #endregion

        #region Properties

        /// <summary>Resolved services (for presenters living in the same scene).</summary>
        public ServiceRegistry Services => _services;

        /// <summary>Active composition root, if any.</summary>
        public static NetworkedGameContext Instance => _instance;

        /// <summary>
        /// Receives a short-lived AI backend token from the application's
        /// authentication flow. The token remains memory-only.
        /// </summary>
        public bool SetAiAccessToken(string accessToken)
        {
            if (_services == null || !_services.TryResolve(out RuntimeAccessTokenProvider provider))
                return false;

            provider.SetAccessToken(accessToken);
            return true;
        }

        /// <summary>Clears the in-memory AI token when the application session ends.</summary>
        public void ClearAiAccessToken()
        {
            if (_services != null && _services.TryResolve(out RuntimeAccessTokenProvider provider))
                provider.Clear();
        }

        /// <summary>
        /// Connects a runtime-created chat view after the play canvas exists.
        /// Repeated calls keep the first configured view/controller pair.
        /// </summary>
        public void AttachAiChatWindow(AiChatWindow chatWindow)
        {
            if (chatWindow == null || _services == null)
                return;

            _aiChatWindow ??= chatWindow;
            if (_services.TryResolve(out AiAssistantController existingController))
            {
                _aiChatWindow.Configure(existingController);
                return;
            }

            var contextProvider = _services.Register<IGameContextProvider>(
                new LiveGameContextProvider(
                    _services.Resolve<BoardManager>(),
                    _services.Resolve<PlayerManager>(),
                    _services.Resolve<GameManager>(),
                    _services.Resolve<TurnManager>(),
                    _services.Resolve<TurnInputSession>(),
                    _services.Resolve<ICommandRejectionReader>(),
                    _services.Resolve<ITutorialProgressReader>(),
                    _services.Resolve<ReplayManager>()));

            var controller = _services.Register(new AiAssistantController(
                BuildAssistantModes(),
                contextProvider,
                new AiResponseRestrictionGuard(),
                _aiChatWindow,
                AMath.UI.Localization.UiLocalizationProvider.Shared));
            _services.Register<IAiEntryPoint>(controller);
            _aiChatWindow.Configure(controller);
        }

        /// <summary>
        /// Strategy Coach runs entirely offline, so it is always offered. Rule
        /// Assistant needs a reachable backend, and offering a button that can
        /// only ever fail is worse than not offering it at all.
        /// </summary>
        private IAiAssistantMode[] BuildAssistantModes()
        {
            bool backendReady = _ruleAssistantProfile != null
                && _aiBackendConfig != null
                && _aiBackendConfig.IsValid(out _)
                && _services.TryResolve(out IAiClient client)
                && client != null
                && _services.TryResolve(out IAccessTokenProvider tokenProvider)
                && !string.IsNullOrWhiteSpace(tokenProvider.GetAccessToken());

            ILocalizedTextProvider text = AMath.UI.Localization.UiLocalizationProvider.Shared;
            if (!backendReady)
            {
                Debug.Log("[AI] Rule Assistant is unavailable (no backend configured); Strategy Coach only.");
                return new IAiAssistantMode[] { new StrategyCoachMode(text) };
            }

            return new IAiAssistantMode[]
            {
                new RuleAssistantMode(
                    _services.Resolve<IAiClient>(),
                    _ruleAssistantProfile,
                    new GameContextPromptFormatter()),
                new StrategyCoachMode(text)
            };
        }

        #endregion

        #region Factory

        /// <summary>Finds or creates the DontDestroyOnLoad composition root.</summary>
        public static NetworkedGameContext EnsureExists()
        {
            if (_instance != null) return _instance;

            var existing = FindFirstObjectByType<NetworkedGameContext>();
            if (existing != null) return existing;

            var go = new GameObject("NetworkedGameContext");
            return go.AddComponent<NetworkedGameContext>();
        }

        #endregion

        #region Lifecycle

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadAiConfiguration();
            EnsureNetworkStack();
            BuildServices();
        }

        private void Update()
        {
            _services?.TickAll(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            NetworkContext.Clear();
            ClearAiAccessToken();
            _services?.Dispose();
            _services = null;
        }

        #endregion

        #region Internals

        private void EnsureNetworkStack()
        {
            if (_networkManager != null && _authenticator != null && _networkGameState != null)
                return;

            NetworkSessionFactory.BuiltStack stack = NetworkSessionFactory.Create();
            _networkManager = stack.NetworkManager;
            _authenticator = stack.Authenticator;
            _networkGameState = stack.GameState;
            _migrationTableSync = stack.MigrationTable;
        }

        private void BuildServices()
        {
            _services = new ServiceRegistry();

            IEventBus bus = _services.Register<IEventBus>(new EventBus());
            var stateMachine = _services.Register(new GameStateMachine(bus));
            var boardManager = _services.Register(new BoardManager());
            var playerManager = _services.Register(new PlayerManager(bus));
            var turnManager = _services.Register(new TurnManager(bus));
            var gameManager = _services.Register(new GameManager(bus, stateMachine, boardManager, playerManager, turnManager));
            var replayManager = _services.Register(new ReplayManager(bus));
            var saveManager = _services.Register(new SaveManager(bus, gameManager, replayManager));
            IAiMoveChooser moveChooser = _services.Register<IAiMoveChooser>(new MediumAiMoveChooser());
            _services.Register(new AiSeatController(bus, gameManager, playerManager, boardManager, turnManager, moveChooser));
            var turnInput = _services.Register(new TurnInputSession(bus, boardManager, playerManager));
            _services.Register<ISelectionStateReader>(turnInput);
            _services.Register<ICommandRejectionReader>(new CommandRejectionTracker(bus));
            _services.Register<ITutorialProgressReader>(new TutorialProgressTracker(bus));
            RegisterAiBackendServices();

            var session = _services.Register(new RoomSession());
            var discovery = _services.Register(new DiscoveryManager(bus));
            var roomManager = _services.Register(new RoomManager(bus, session, discovery, gameManager, _networkManager));
            var migrationManager = _services.Register(new HostMigrationManager(bus, discovery, roomManager, session));
            _services.Register(new ReconnectionManager(
                bus, stateMachine, gameManager, playerManager, saveManager,
                migrationManager, roomManager, discovery, session));

            _services.Register(_networkManager);
            _services.Register(_networkGameState);
            _networkManager.Configure(bus, playerManager, gameManager, session);
            _authenticator.Configure(session, playerManager, gameManager, bus);

            bus.Subscribe<HostStartedEvent>(_ => CopySessionToSaves(session, saveManager));
            bus.Subscribe<ClientConnectedEvent>(_ => CopySessionToSaves(session, saveManager));

            NetworkContext.Install(_services);
        }

        private void LoadAiConfiguration()
        {
            _aiBackendConfig ??= Resources.Load<AiBackendConfig>("AI/AiBackendConfig");
            _ruleAssistantProfile ??= Resources.Load<AiPromptProfile>("AI/RuleAssistantProfile");

            // The shipped asset is intentionally blank; the endpoint and model
            // belong to a deployment, not to the project.
            _aiBackendConfig?.ApplyRuntimeOverrides(AiRuntimeSettings.Endpoint, AiRuntimeSettings.Model);
        }

        private void RegisterAiBackendServices()
        {
            if (_aiBackendConfig == null)
                return;

            var tokenProvider = _services.Register(new RuntimeAccessTokenProvider());
            _services.Register<IAccessTokenProvider>(tokenProvider);
            _services.Register(_aiBackendConfig);
            _services.Register<IAiClient>(new OpenAiCompatibleClient(_aiBackendConfig, tokenProvider));

            // Without this the client has no bearer token and every Rule
            // Assistant request fails at the first line of SendAsync.
            SetAiAccessToken(AiRuntimeSettings.AccessToken);

            if (!_aiBackendConfig.IsValid(out string error))
                Debug.Log($"[AI] Rule Assistant backend not configured ({error}); set {AiRuntimeSettings.EndpointVariable} and {AiRuntimeSettings.ModelVariable} to enable it.");

            if (_aiChatWindow != null)
                AttachAiChatWindow(_aiChatWindow);
        }

        private static void CopySessionToSaves(RoomSession session, SaveManager saveManager)
        {
            saveManager.RoomName = session.RoomName;
            saveManager.RoomCode = session.RoomCode;
            saveManager.MaxPlayers = session.MaxPlayers;
            saveManager.Port = session.Port;
        }

        #endregion
    }
}
