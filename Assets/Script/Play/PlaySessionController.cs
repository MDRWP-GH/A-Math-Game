using System;
using System.Collections.Generic;
using System.Linq;
using AMath.Art;
using AMath.AI.UI;
using AMath.Bootstrap;
using AMath.Core;
using AMath.Core.Assistance;
using AMath.Core.Events;
using AMath.Core.StateMachines;
using AMath.Gameplay.Board;
using AMath.Gameplay.Interaction;
using AMath.Gameplay.Players;
using AMath.Networking;
using AMath.Networking.Discovery;
using AMath.Networking.HostMigration;
using AMath.Networking.Room;
using AMath.Networking.RPC;
using AMath.Settings;
using AMath.UI.Audio;
using AMath.UI.Localization;
using AMath.Utilities;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static AMath.Gameplay.Board.AMathTileSet;

namespace AMath.UI
{
    /// <summary>
    /// Code-driven multiplayer play flow: start choice → host/join → lobby →
    /// match HUD → results, plus pause and connection-lost overlays. Bootstraps
    /// <see cref="NetworkedGameContext"/> when the scene has none.
    /// </summary>
    public sealed class PlaySessionController : MonoBehaviour
    {
        private enum ScreenId
        {
            None,
            StartChoice,
            HostSetup,
            Browser,
            Lobby,
            Match,
            Result
        }

        private const float TurnWarningSeconds = 10f;

        /// <summary>Horizontal distance between rack tiles.</summary>
        private const float RackPitch = 78f;

        private const int VisibleSeatCount = 4;

        /// <summary>Diameter of a colour swatch in the lobby picker.</summary>
        private const float ColorSwatchSize = 42f;

        /// <summary>Outer ring around a swatch; doubles as the "selected" marker.</summary>
        private const float ColorRingSize = 54f;

        /// <summary>Width the 13 swatches are spread across.</summary>
        private const float ColorRowWidth = 660f;

        /// <summary>Colour dot shown on each lobby member card.</summary>
        private const float MemberSwatchSize = 30f;

        /// <summary>How long a browser notice survives the ~1 Hz discovery refresh.</summary>
        private const float BrowserNoticeSeconds = 8f;

        /// <summary>
        /// Outline drawn around a colour swatch or avatar when it is not the
        /// selected one. Dark and opaque enough that the black and grey entries
        /// still read as swatches against the panel behind them.
        /// </summary>
        private static readonly Color ColorRingIdle = new(0f, 0f, 0f, 0.55f);

        private static readonly Vector2[] SeatAnchors =
        {
            new(0f, 0f), // P1 bottom-left
            new(0f, 1f), // P2 top-left
            new(1f, 1f), // P3 top-right
            new(1f, 0f)  // P4 bottom-right
        };

        private static readonly Vector2[] SeatPivots =
        {
            new(0f, 0f),
            new(0f, 1f),
            new(1f, 1f),
            new(1f, 0f)
        };

        private static readonly Vector2[] SeatPositions =
        {
            new(28f, 28f),
            new(28f, -28f),
            new(-28f, -28f),
            new(-28f, 28f)
        };

        private UiFactory _ui;
        private ILocalizedTextProvider _text;
        private Canvas _canvas;

        private GameObject _startRoot;
        private GameObject _hostRoot;
        private GameObject _browserRoot;
        private GameObject _lobbyRoot;
        private GameObject _matchRoot;
        private GameObject _resultRoot;
        private GameObject _recoveryRoot;
        private GameObject _pauseRoot;
        private MenuEntranceAnimator _startEntrance;
        private Button _startHostButton;
        private Button _startJoinButton;
        private Button _startBackButton;

        private RoomBrowserPresenter _browserPresenter;
        private LobbyPresenter _lobbyPresenter;
        private MatchHudPresenter _matchPresenter;
        private MatchResultPresenter _resultPresenter;
        private ConnectionLostPresenter _recoveryPresenter;
        private SettingsMenuController _matchSettings;

        private Button[] _colorButtons;
        private Image[] _colorRings;
        private GameObject _teamPickerRoot;
        private Button _team1Button;
        private Button _team2Button;

        private Text _browserStatus;
        private Text _lobbyStatus;
        private Text _lobbyCode;
        private Transform _lobbyMembersRoot;
        private Button _formatIndividualButton;
        private Button _formatTeamButton;
        private Transform _roomListRoot;
        private InputField _roomNameField;
        private InputField _joinCodeField;

        private Text _matchStatus;
        private Text _matchTimer;
        private Text _bagLabel;
        private Text _matchPreview;
        private Transform _rackRoot;
        private Button _confirmButton;
        private Button _clearButton;
        private Button _passButton;
        private Button _exchangeButton;
        private MatchBoardView _boardView;
        private readonly List<int> _exchangeSelection = new();
        private readonly List<Button> _rackButtons = new(GameRules.RackSize);
        private readonly List<Text> _rackLabels = new(GameRules.RackSize);
        private readonly List<Image> _rackImages = new(GameRules.RackSize);
        private readonly PlayerSeatHud[] _seats = new PlayerSeatHud[VisibleSeatCount];
        private bool _exchangeMode;
        private Transform _declareRoot;

        private Button _pauseResumeButton;
        private Button _pausePlayOnButton;
        private Button _pauseSettingsButton;
        private Button _pauseLeaveButton;
        private readonly List<Button> _visiblePauseButtons = new(4);

        private sealed class PlayerSeatHud
        {
            public GameObject Root;
            public Image Avatar;
            public Text Label;
            public Text Score;
            public Image TurnRing;
        }

        private Text _resultWinner;
        private Text _resultMeta;
        private Text _resultBody;
        private Text _recoveryStatus;
        private Button _recoveryEndButton;

        private ScreenId _screen = ScreenId.None;
        private IEventBus _eventBus;
        private MainMenuController _mainMenu;
        private InputAction _cancelAction;
        private int _settingsClosedFrame = -1;

        /// <summary>
        /// Why the last session ended, shown on the browser screen. Discovery
        /// rewrites the status line about once a second, so the notice holds
        /// its place for a while instead of flashing past unread.
        /// </summary>
        private string _browserNotice;
        private float _browserNoticeUntil;

        // Stored delegates so OnDestroy can unsubscribe exactly what was registered.
        private Action<HostStartedEvent> _onHostStarted;
        private Action<ClientConnectedEvent> _onClientConnected;
        private Action<MatchStartedEvent> _onMatchStarted;
        private Action<MatchRestoredEvent> _onMatchRestored;
        private Action<HostStoppedEvent> _onHostStopped;
        private Action<ConnectionRejectedEvent> _onConnectionRejected;
        private Action<string> _onBrowserError;
        private Action<IReadOnlyList<NetworkPlayer>> _onMembersChanged;
        private Action<string> _onMatchError;
        private Action<MatchResult> _onResultChanged;
        private Action<bool> _onRecoveryFinished;

        public static PlaySessionController EnsureExists()
        {
            var existing = FindFirstObjectByType<PlaySessionController>();
            if (existing != null) return existing;

            var go = new GameObject("Play Session");
            return go.AddComponent<PlaySessionController>();
        }

        public void OpenFromMainMenu(MainMenuController mainMenu)
        {
            _mainMenu = mainMenu;
            if (_mainMenu != null)
                _mainMenu.gameObject.SetActive(false);

            LocalIdentity.DisplayName = GameSettings.PlayerName;

            NetworkedGameContext.EnsureExists();
            EnsureBuilt();
            ShowStartChoice();
        }

        private void EnsureBuilt()
        {
            if (_canvas != null) return;

            DontDestroyOnLoad(gameObject);
            _ui = new UiFactory(GameFonts.Jersey25);
            _text = UiLocalizationProvider.Shared;
            _canvas = _ui.CreateCanvas(transform, "PlayCanvas", 120);
            _cancelAction = InputSystem.actions?.FindAction("UI/Cancel", false);
            AiChatWindow chatWindow = AiChatWindow.CreateRuntime(
                _canvas.transform, UiLocalizationProvider.Shared);
            NetworkedGameContext.Instance?.AttachAiChatWindow(chatWindow);

            // Score previews and rejection reasons come from the gameplay layer;
            // route their language through the same settings switch as the UI.
            PlacementPreviewFormatter.ThaiSelector = static () => GameSettings.LanguageIndex == 1;

            gameObject.AddComponent<RoomBrowserPresenter>();
            gameObject.AddComponent<LobbyPresenter>();
            gameObject.AddComponent<MatchHudPresenter>();
            gameObject.AddComponent<MatchResultPresenter>();
            gameObject.AddComponent<ConnectionLostPresenter>();
            gameObject.AddComponent<GameAudioPresenter>();

            _browserPresenter = GetComponent<RoomBrowserPresenter>();
            _lobbyPresenter = GetComponent<LobbyPresenter>();
            _matchPresenter = GetComponent<MatchHudPresenter>();
            _resultPresenter = GetComponent<MatchResultPresenter>();
            _recoveryPresenter = GetComponent<ConnectionLostPresenter>();

            BuildStartChoice();
            BuildHostSetup();
            BuildBrowser();
            BuildLobby();
            BuildMatch();
            BuildResult();
            BuildRecovery();
            BuildPause();

            _onBrowserError = msg =>
            {
                SetBrowserNotice(_text.GetText(msg));
                UpdateBrowserStatus(string.Empty);
            };
            _onMembersChanged = _ => RefreshLobby();
            _onMatchError = msg =>
            {
                if (_matchStatus != null)
                    _matchStatus.text = PlacementPreviewFormatter.LocalizeError(msg);
            };
            _onResultChanged = _ => ShowResult();
            _onRecoveryFinished = ok =>
            {
                HideRecovery();
                if (ok) return;

                // A rejected join already put the host's own explanation on the
                // browser; the generic message must not replace it.
                if (_screen == ScreenId.Browser) return;

                SetBrowserNotice(_text.GetText("ui.play.connection_lost"));
                ShowBrowser();
            };

            _browserPresenter.RoomsChanged += RefreshRoomList;
            _browserPresenter.ErrorRaised += _onBrowserError;
            _lobbyPresenter.MembersChanged += _onMembersChanged;
            _matchPresenter.StateChanged += RefreshMatch;
            _matchPresenter.ErrorRaised += _onMatchError;
            _resultPresenter.ResultChanged += _onResultChanged;
            _recoveryPresenter.StatusChanged += OnRecoveryStatus;
            _recoveryPresenter.RecoveryFinished += _onRecoveryFinished;

            GameSettings.Changed += OnSettingsChanged;

            if (NetworkContext.Services != null)
            {
                _eventBus = NetworkContext.Services.Resolve<IEventBus>();
                _onHostStarted = _ => ShowLobby();
                _onClientConnected = _ =>
                {
                    if (NetworkContext.Services.TryResolve(out AMath.Managers.GameManager gameManager)
                        && gameManager.Config != null
                        && gameManager.Phase != MatchPhase.Lobby
                        && gameManager.Phase != MatchPhase.Finished)
                        ShowMatch();
                    else
                        ShowLobby();
                };
                _onMatchStarted = _ => ShowMatch();

                // Reconnecting mid-match arrives as a restore, not a start, and
                // still has to put the player back on the board.
                _onMatchRestored = _ => ShowMatch();
                _onHostStopped = _ => ReturnToMenu();

                // The host's refusal text explains things the client cannot
                // deduce (wrong version, duplicate identity, room full), so it
                // is shown verbatim instead of a generic "could not join".
                _onConnectionRejected = evt =>
                {
                    SetBrowserNotice(string.IsNullOrEmpty(evt.Reason)
                        ? _text.GetText("ui.play.err_join")
                        : evt.Reason);
                    ShowBrowser();
                };

                _eventBus.Subscribe(_onHostStarted);
                _eventBus.Subscribe(_onClientConnected);
                _eventBus.Subscribe(_onMatchStarted);
                _eventBus.Subscribe(_onMatchRestored);
                _eventBus.Subscribe(_onHostStopped);
                _eventBus.Subscribe(_onConnectionRejected);
            }
        }

        private void OnDestroy()
        {
            if (_browserPresenter != null)
            {
                _browserPresenter.RoomsChanged -= RefreshRoomList;
                _browserPresenter.ErrorRaised -= _onBrowserError;
            }

            if (_lobbyPresenter != null) _lobbyPresenter.MembersChanged -= _onMembersChanged;

            if (_matchPresenter != null)
            {
                _matchPresenter.StateChanged -= RefreshMatch;
                _matchPresenter.ErrorRaised -= _onMatchError;
            }

            if (_resultPresenter != null) _resultPresenter.ResultChanged -= _onResultChanged;

            if (_recoveryPresenter != null)
            {
                _recoveryPresenter.StatusChanged -= OnRecoveryStatus;
                _recoveryPresenter.RecoveryFinished -= _onRecoveryFinished;
            }

            GameSettings.Changed -= OnSettingsChanged;

            if (_eventBus != null)
            {
                _eventBus.Unsubscribe(_onHostStarted);
                _eventBus.Unsubscribe(_onClientConnected);
                _eventBus.Unsubscribe(_onMatchStarted);
                _eventBus.Unsubscribe(_onMatchRestored);
                _eventBus.Unsubscribe(_onHostStopped);
                _eventBus.Unsubscribe(_onConnectionRejected);
            }
        }

        private void Update()
        {
            if (_screen == ScreenId.StartChoice || _screen == ScreenId.HostSetup || _screen == ScreenId.Browser)
                HandlePlayFlowCancel();

            if (_screen == ScreenId.Match && _matchRoot != null && _matchRoot.activeSelf && _matchPresenter != null)
            {
                UpdateTimerAndBag();

                // The wait countdown is the only status that changes without an
                // event behind it, so it is re-rendered per frame.
                if (_matchPresenter.IsWaitingForPlayers && _matchStatus != null)
                    _matchStatus.text = DescribeWaitingForPlayers();

                PulseTurnRings();
                HandlePauseInput();
            }
        }

        private void HandlePlayFlowCancel()
        {
            if (!WasCancelPressed()) return;

            switch (_screen)
            {
                case ScreenId.StartChoice:
                    if (_startEntrance != null && _startEntrance.IsPlaying)
                        _startEntrance.Skip();
                    else
                        ReturnToMenu();
                    break;
                case ScreenId.HostSetup:
                    ShowStartChoice();
                    break;
                case ScreenId.Browser:
                    _browserPresenter?.StopSearching();
                    ShowStartChoice();
                    break;
            }
        }

        private void UpdateTimerAndBag()
        {
            if (_matchTimer == null) return;

            float remaining = _matchPresenter.RemainingTurnSeconds;
            _matchTimer.text = $"{Mathf.CeilToInt(remaining)}s";
            _matchTimer.color = remaining <= TurnWarningSeconds && remaining > 0f
                ? UiPalette.TimerWarning
                : UiPalette.Primary;

            if (_bagLabel != null)
                _bagLabel.text = _matchPresenter.BagCount.ToString();
        }

        private void HandlePauseInput()
        {
            // Skip the frame the settings screen closed itself on Esc, so the
            // same key press does not immediately toggle the pause overlay too.
            if (Time.frameCount == _settingsClosedFrame) return;
            if (_matchSettings != null && _matchSettings.IsOpen) return;
            if (_recoveryRoot != null && _recoveryRoot.activeSelf) return;
            if (!WasCancelPressed()) return;

            if (_pauseRoot.activeSelf) ClosePause();
            else OpenPause();
        }

        private bool WasCancelPressed()
        {
            if (_cancelAction != null && _cancelAction.enabled && _cancelAction.WasPressedThisFrame())
                return true;

            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        }

        private void OnSettingsChanged()
        {
            // Language may have changed: re-render every dynamic string on the
            // active screen (static labels update through LocalizedText).
            switch (_screen)
            {
                case ScreenId.Browser:
                    if (_browserRoot.activeSelf && _browserPresenter != null)
                        UpdateBrowserStatus(_text.GetText(_browserPresenter.NoRoomsFound ? "ui.play.no_rooms" : "ui.play.searching"));
                    break;
                case ScreenId.Lobby:
                    RefreshLobby();
                    break;
                case ScreenId.Match:
                    // While the settings screen is open, sliders raise Changed every
                    // frame; defer the (heavy) HUD refresh until it closes.
                    if (_matchSettings == null || !_matchSettings.IsOpen)
                        RefreshMatch();
                    break;
                case ScreenId.Result:
                    RefreshResult();
                    break;
            }
        }

        #region Screen switches

        private void ShowStartChoice()
        {
            _screen = ScreenId.StartChoice;
            HideRecovery();
            _browserPresenter?.StopSearching();
            SetActiveScreens(start: true);
            if (_startEntrance != null)
                _startEntrance.Play();
            UiFactory.Select(_startHostButton);
        }

        private void ShowHostSetup()
        {
            _screen = ScreenId.HostSetup;
            HideRecovery();
            _browserPresenter?.StopSearching();
            SetActiveScreens(host: true);
            if (_roomNameField != null)
                UiFactory.Select(_roomNameField);
        }

        private void ShowBrowser()
        {
            _screen = ScreenId.Browser;
            HideRecovery();
            SetActiveScreens(browser: true);
            _browserPresenter.StartSearching();
            UpdateBrowserStatus(_text.GetText("ui.play.searching"));
        }

        /// <summary>Shows <paramref name="message"/> on the browser for a few seconds.</summary>
        private void SetBrowserNotice(string message)
        {
            _browserNotice = message;
            _browserNoticeUntil = Time.unscaledTime + BrowserNoticeSeconds;
        }

        private void ClearBrowserNotice()
        {
            _browserNotice = null;
            _browserNoticeUntil = 0f;
        }

        /// <summary>A new attempt is under way, so the previous verdict is stale.</summary>
        private void ShowConnectingStatus()
        {
            ClearBrowserNotice();
            if (_browserStatus != null)
                _browserStatus.text = _text.GetText("ui.play.connecting");
        }

        /// <summary>
        /// Writes the browser status line, letting an active notice win over
        /// the routine "searching…" / "no rooms" text.
        /// </summary>
        private void UpdateBrowserStatus(string fallback)
        {
            if (_browserStatus == null) return;

            if (!string.IsNullOrEmpty(_browserNotice) && Time.unscaledTime < _browserNoticeUntil)
            {
                _browserStatus.text = _browserNotice;
                return;
            }

            ClearBrowserNotice();
            _browserStatus.text = fallback;
        }

        private void ShowLobby()
        {
            _screen = ScreenId.Lobby;
            _browserPresenter.StopSearching();
            SetActiveScreens(lobby: true);
            _lobbyPresenter?.RefreshMembers();
            RefreshLobby();
        }

        private void ShowMatch()
        {
            _screen = ScreenId.Match;
            SetActiveScreens(match: true);
            _eventBus?.Publish(new BoardLoadedEvent());
            RefreshMatch();
        }

        private void ShowResult()
        {
            _screen = ScreenId.Result;
            // The recovery overlay covers the whole screen and swallows every
            // click, so it must never outlive the screen it was shown over.
            HideRecovery();
            if (_startRoot != null) _startRoot.SetActive(false);
            if (_hostRoot != null) _hostRoot.SetActive(false);
            _browserRoot.SetActive(false);
            _lobbyRoot.SetActive(false);
            // Keep the board under the result overlay so the end screen matches
            // the mockup (winner text on top of the still-visible match).
            _matchRoot.SetActive(true);
            OverlayFade.Ensure(_resultRoot).FadeIn();
            OverlayFade.Ensure(_pauseRoot)?.HideInstant();
            RefreshResult();
        }

        private void HideRecovery()
        {
            if (_recoveryRoot != null)
                OverlayFade.Ensure(_recoveryRoot).HideInstant();
        }

        private void SetActiveScreens(
            bool start = false,
            bool host = false,
            bool browser = false,
            bool lobby = false,
            bool match = false,
            bool result = false)
        {
            if (_startRoot != null) _startRoot.SetActive(start);
            if (_hostRoot != null) _hostRoot.SetActive(host);
            _browserRoot.SetActive(browser);
            _lobbyRoot.SetActive(lobby);
            _matchRoot.SetActive(match);
            _resultRoot.SetActive(result);
        }

        private void ReturnToMenu()
        {
            _browserPresenter?.StopSearching();
            SetActiveScreens();
            HideRecovery();
            _pauseRoot.SetActive(false);
            ClearBrowserNotice();
            _screen = ScreenId.None;
            if (_mainMenu != null)
            {
                _mainMenu.gameObject.SetActive(true);
                Destroy(gameObject);
            }
        }

        #endregion

        #region Start / Host / Join

        private void BuildStartChoice()
        {
            _startRoot = ForestScreen("Start Choice");
            var title = _ui.CreateOutlinedTitle(_startRoot.transform, "Title", string.Empty, 72);
            title.font = GameFonts.JainiPurva;
            UiFactory.SetAnchoredRect(
                title.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(980f, 100f), new Vector2(0f, -72f));
            LocalizedText.Bind(title, "ui.menu.title");

            _startHostButton = _ui.CreateTextMenuButton(_startRoot.transform, "Host", string.Empty, 46, ShowHostSetup);
            UiFactory.SetCenteredRect(_startHostButton.GetComponent<RectTransform>(), new Vector2(0f, 80f), new Vector2(560f, 72f));
            LocalizedText.Bind(_startHostButton.GetComponentInChildren<Text>(), "ui.play.host");

            _startJoinButton = _ui.CreateTextMenuButton(_startRoot.transform, "Join", string.Empty, 46, ShowBrowser);
            UiFactory.SetCenteredRect(_startJoinButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(560f, 72f));
            LocalizedText.Bind(_startJoinButton.GetComponentInChildren<Text>(), "ui.play.join");

            _startBackButton = _ui.CreateTextMenuButton(_startRoot.transform, "Back", string.Empty, 46, ReturnToMenu);
            UiFactory.SetCenteredRect(_startBackButton.GetComponent<RectTransform>(), new Vector2(0f, -80f), new Vector2(560f, 72f));
            LocalizedText.Bind(_startBackButton.GetComponentInChildren<Text>(), "ui.play.back");

            UiFactory.SetVerticalNavigation(_startHostButton, _startBackButton, _startJoinButton);
            UiFactory.SetVerticalNavigation(_startJoinButton, _startHostButton, _startBackButton);
            UiFactory.SetVerticalNavigation(_startBackButton, _startJoinButton, _startHostButton);

            _startEntrance = _startRoot.AddComponent<MenuEntranceAnimator>();
            _startEntrance.SetTargets(title, _startHostButton, _startJoinButton, _startBackButton);
            _startRoot.SetActive(false);
        }

        private void BuildHostSetup()
        {
            _hostRoot = ForestScreen("Host Setup");

            var title = _ui.CreateOutlinedTitle(_hostRoot.transform, "Title", string.Empty, 64);
            UiFactory.SetAnchoredRect(
                title.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(900f, 90f), new Vector2(0f, -80f));
            LocalizedText.Bind(title, "ui.play.host_title");

            var nameLabel = _ui.CreateText(
                "RoomLabel", _hostRoot.transform, string.Empty, 32, FontStyle.Normal,
                Color.white, TextAnchor.MiddleRight);
            UiFactory.SetCenteredRect(nameLabel.rectTransform, new Vector2(-220f, 40f), new Vector2(280f, 48f));
            UiFactory.AddDoubleOutline(nameLabel.gameObject, new Vector2(2.5f, -2.5f), new Vector2(1.2f, -1.2f));
            LocalizedText.Bind(nameLabel, "ui.play.room_name");

            // Left empty on purpose: the placeholder shows the name the room
            // gets if the host just presses play, so naming it is optional
            // rather than something they have to clear first.
            _roomNameField = _ui.CreateInputField(_hostRoot.transform, "RoomName", string.Empty, RoomSession.DefaultRoomName);
            UiFactory.SetCenteredRect(_roomNameField.GetComponent<RectTransform>(), new Vector2(160f, 40f), new Vector2(420f, 56f));

            var back = _ui.CreateAccentButton(
                _hostRoot.transform, "Back", string.Empty,
                UiPalette.Danger, UiPalette.DangerHighlight, ShowStartChoice, 28);
            UiFactory.SetAnchoredRect(
                back.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(200f, 64f), new Vector2(72f, 56f));
            LocalizedText.Bind(back.GetComponentInChildren<Text>(), "ui.play.back");

            var play = _ui.CreateAccentButton(
                _hostRoot.transform, "Play", string.Empty,
                UiPalette.Success, UiPalette.SuccessHighlight, () =>
                {
                    ShowConnectingStatus();
                    _browserPresenter.CreateRoom(
                        _roomNameField != null ? _roomNameField.text : string.Empty,
                        GameRules.MaxPlayers);
                }, 28);
            UiFactory.SetAnchoredRect(
                play.GetComponent<RectTransform>(),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(200f, 64f), new Vector2(-72f, 56f));
            LocalizedText.Bind(play.GetComponentInChildren<Text>(), "ui.play.play_action");

            _hostRoot.SetActive(false);
        }

        private void BuildBrowser()
        {
            _browserRoot = ForestScreen("Join");

            var title = _ui.CreateOutlinedTitle(_browserRoot.transform, "Title", string.Empty, 64);
            UiFactory.SetAnchoredRect(
                title.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(720f, 90f), new Vector2(0f, -64f));
            LocalizedText.Bind(title, "ui.play.browser_title");

            var panel = UiFactory.CreateGlassPanel(_browserRoot.transform, "Panel", UiPalette.Glass);
            UiFactory.SetCenteredRect(panel.rectTransform, new Vector2(0f, -20f), new Vector2(860f, 560f));

            _browserStatus = _ui.CreateText(
                "Status", panel.transform, string.Empty, 28, FontStyle.Normal,
                Color.white, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_browserStatus.rectTransform, new Vector2(0f, 220f), new Vector2(760f, 48f));
            UiFactory.AddDoubleOutline(_browserStatus.gameObject, new Vector2(2.5f, -2.5f), new Vector2(1.2f, -1.2f));

            var viewport = UiFactory.CreateImage("Viewport", panel.transform, UiPalette.GlassRow);
            viewport.raycastTarget = true;
            UiFactory.SetCenteredRect(viewport.rectTransform, new Vector2(0f, 20f), new Vector2(780f, 280f));
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = UiFactory.CreateRect("List", viewport.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            UiFactory.AddVerticalLayout(content.gameObject, 8f, new RectOffset(8, 8, 8, 8));
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _roomListRoot = content;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            _joinCodeField = _ui.CreateInputField(panel.transform, "JoinCode", string.Empty, string.Empty);
            UiFactory.SetCenteredRect(_joinCodeField.GetComponent<RectTransform>(), new Vector2(-120f, -200f), new Vector2(420f, 56f));
            if (_joinCodeField.placeholder is Text joinCodeHint)
                LocalizedText.Bind(joinCodeHint, "ui.play.room_code");

            var joinButton = _ui.CreateAccentButton(
                panel.transform, "JoinCodeBtn", string.Empty,
                UiPalette.Success, UiPalette.SuccessHighlight, () =>
                {
                    ShowConnectingStatus();
                    _browserPresenter.JoinByCode(_joinCodeField.text);
                }, 24);
            UiFactory.SetCenteredRect(joinButton.GetComponent<RectTransform>(), new Vector2(220f, -200f), new Vector2(220f, 56f));
            LocalizedText.Bind(joinButton.GetComponentInChildren<Text>(), "ui.play.join_code");

            var back = _ui.CreateAccentButton(
                _browserRoot.transform, "Back", string.Empty,
                UiPalette.Danger, UiPalette.DangerHighlight, () =>
                {
                    _browserPresenter.StopSearching();
                    ShowStartChoice();
                }, 28);
            UiFactory.SetAnchoredRect(
                back.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(200f, 64f), new Vector2(72f, 40f));
            LocalizedText.Bind(back.GetComponentInChildren<Text>(), "ui.play.back");

            _browserRoot.SetActive(false);
        }

        private void RefreshRoomList(IReadOnlyList<RoomInfo> rooms)
        {
            for (int i = _roomListRoot.childCount - 1; i >= 0; i--)
                Destroy(_roomListRoot.GetChild(i).gameObject);

            if (rooms == null || rooms.Count == 0)
            {
                UpdateBrowserStatus(_text.GetText(_browserPresenter.NoRoomsFound ? "ui.play.no_rooms" : "ui.play.searching"));
                return;
            }

            UpdateBrowserStatus(string.Empty);
            foreach (RoomInfo room in rooms)
            {
                RoomInfo captured = room;
                string label = $"{room.Advertisement.RoomName}  [{room.Advertisement.RoomCode}]  {room.Advertisement.CurrentPlayers}/{room.Advertisement.MaxPlayers}";
                var row = UiFactory.CreateGlassPanel(_roomListRoot, "Room", UiPalette.GlassRow);
                UiFactory.SetLayoutSize(row.gameObject, 0f, 56f, 1f);

                var button = _ui.CreateFlatButton(row.transform, "Join", label, 22, TextAnchor.MiddleLeft, () =>
                {
                    ShowConnectingStatus();
                    _browserPresenter.JoinRoom(captured);
                });
                UiFactory.Stretch(button.GetComponent<RectTransform>());
                var labelText = button.GetComponentInChildren<Text>();
                if (labelText != null)
                    labelText.color = Color.white;
            }
        }

        #endregion

        #region Lobby

        private void BuildLobby()
        {
            _lobbyRoot = ForestScreen("Lobby");

            var title = _ui.CreateOutlinedTitle(_lobbyRoot.transform, "Title", string.Empty, 60);
            UiFactory.SetAnchoredRect(
                title.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(720f, 80f), new Vector2(0f, -48f));
            LocalizedText.Bind(title, "ui.play.lobby_title");

            // Taller than the rest of the flow's panels to make room for the
            // colour picker without squeezing the member list.
            var panel = UiFactory.CreateGlassPanel(_lobbyRoot.transform, "Panel", UiPalette.GlassStrong);
            UiFactory.SetCenteredRect(panel.rectTransform, new Vector2(0f, -10f), new Vector2(820f, 860f));

            _lobbyCode = _ui.CreateText("Code", panel.transform, string.Empty, 40, FontStyle.Normal, UiPalette.Primary, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_lobbyCode.rectTransform, new Vector2(0f, 310f), new Vector2(720f, 56f));
            UiFactory.AddDoubleOutline(_lobbyCode.gameObject, new Vector2(2.5f, -2.5f), new Vector2(1.2f, -1.2f));

            var membersTitle = _ui.CreateText("MembersTitle", panel.transform, string.Empty, 24, FontStyle.Normal, UiPalette.MutedText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(membersTitle.rectTransform, new Vector2(0f, 262f), new Vector2(660f, 36f));
            LocalizedText.Bind(membersTitle, "ui.play.members");

            _lobbyMembersRoot = UiFactory.CreateRect("Members", panel.transform).transform;
            UiFactory.SetCenteredRect((RectTransform)_lobbyMembersRoot, new Vector2(0f, 120f), new Vector2(700f, 240f));

            BuildColorPicker(panel.transform);
            BuildTeamPicker(panel.transform);

            var formatTitle = _ui.CreateText("FormatTitle", panel.transform, string.Empty, 22, FontStyle.Normal, UiPalette.MutedText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(formatTitle.rectTransform, new Vector2(0f, -208f), new Vector2(660f, 32f));
            LocalizedText.Bind(formatTitle, "ui.play.format");

            _formatIndividualButton = _ui.CreateButton(
                panel.transform, "FormatIndividual", string.Empty,
                UiPalette.Secondary, UiPalette.SecondaryHighlight, () => SetMatchFormat(MatchFormat.Individual));
            UiFactory.SetCenteredRect(_formatIndividualButton.GetComponent<RectTransform>(), new Vector2(-160f, -256f), new Vector2(280f, 56f));
            LocalizedText.Bind(_formatIndividualButton.GetComponentInChildren<Text>(), "ui.play.format_individual");

            _formatTeamButton = _ui.CreateButton(
                panel.transform, "FormatTeam", string.Empty,
                UiPalette.Secondary, UiPalette.SecondaryHighlight, () => SetMatchFormat(MatchFormat.Team));
            UiFactory.SetCenteredRect(_formatTeamButton.GetComponent<RectTransform>(), new Vector2(160f, -256f), new Vector2(280f, 56f));
            LocalizedText.Bind(_formatTeamButton.GetComponentInChildren<Text>(), "ui.play.format_team");

            _lobbyStatus = _ui.CreateText("Status", panel.transform, string.Empty, 22, FontStyle.Normal, UiPalette.MutedText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_lobbyStatus.rectTransform, new Vector2(0f, -304f), new Vector2(660f, 40f));

            var startButton = _ui.CreateAccentButton(
                panel.transform, "StartMatch", string.Empty,
                UiPalette.Success, UiPalette.SuccessHighlight, () => _lobbyPresenter.StartMatch(), 28);
            UiFactory.SetCenteredRect(startButton.GetComponent<RectTransform>(), new Vector2(0f, -358f), new Vector2(400f, 64f));
            LocalizedText.Bind(startButton.GetComponentInChildren<Text>(), "ui.play.start_match");

            var leaveButton = _ui.CreateAccentButton(
                panel.transform, "Leave", string.Empty,
                UiPalette.Danger, UiPalette.DangerHighlight, () =>
                {
                    _lobbyPresenter.LeaveRoom();
                    ReturnToMenu();
                }, 26);
            UiFactory.SetCenteredRect(leaveButton.GetComponent<RectTransform>(), new Vector2(0f, -420f), new Vector2(280f, 56f));
            LocalizedText.Bind(leaveButton.GetComponentInChildren<Text>(), "ui.play.leave");

            _lobbyRoot.SetActive(false);
        }

        /// <summary>
        /// Row of every selectable colour. All of them are on screen at once so
        /// picking one is a single tap, and the swatches themselves are the only
        /// label the row needs.
        /// </summary>
        private void BuildColorPicker(Transform panel)
        {
            var title = _ui.CreateText("ColorTitle", panel, string.Empty, 22, FontStyle.Normal, UiPalette.MutedText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(title.rectTransform, new Vector2(0f, -20f), new Vector2(660f, 32f));
            LocalizedText.Bind(title, "ui.play.your_color");

            var row = UiFactory.CreateRect("Colors", panel);
            UiFactory.SetCenteredRect(row, new Vector2(0f, -66f), new Vector2(ColorRowWidth, ColorRingSize));

            int count = PlayerColorPalette.Count;
            _colorButtons = new Button[count];
            _colorRings = new Image[count];

            // Spread evenly across the row so the spacing adapts if the palette
            // ever grows or shrinks.
            float pitch = ColorRowWidth / count;
            float startX = (-ColorRowWidth + pitch) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var colorId = (byte)i;
                var cell = UiFactory.CreateRect("Color " + i, row);
                UiFactory.SetCenteredRect(cell, new Vector2(startX + pitch * i, 0f), new Vector2(ColorRingSize, ColorRingSize));

                // Ring sits behind the swatch: a dark outline normally, gold
                // when this is the player's colour. Without it the black and
                // grey swatches would disappear into the panel.
                var ring = UiFactory.CreateImage("Ring", cell, UiFactory.CircleSprite, ColorRingIdle);
                UiFactory.Stretch(ring.rectTransform);
                _colorRings[i] = ring;

                _colorButtons[i] = CreateColorSwatch(cell, colorId, () => _lobbyPresenter.RequestColor(colorId));
            }
        }

        /// <summary>
        /// Circular swatch button. The image stays white and the palette colour
        /// is applied through the tint block, so uGUI's hover / disabled states
        /// shade the actual colour instead of overwriting it.
        /// </summary>
        private static Button CreateColorSwatch(Transform parent, byte colorId, UnityEngine.Events.UnityAction onClick)
        {
            Color color = PlayerColorPalette.ColorOf(colorId);

            var buttonObject = new GameObject(
                "Swatch",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            UiFactory.SetCenteredRect(
                (RectTransform)buttonObject.transform,
                Vector2.zero,
                new Vector2(ColorSwatchSize, ColorSwatchSize));

            var image = buttonObject.GetComponent<Image>();
            image.sprite = UiFactory.CircleSprite;
            image.color = Color.white;
            image.raycastTarget = true;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = color,
                highlightedColor = Color.Lerp(color, Color.white, 0.35f),
                pressedColor = Color.Lerp(color, Color.black, 0.20f),
                selectedColor = Color.Lerp(color, Color.white, 0.35f),
                // Taken colours stay recognisable but visibly muted.
                disabledColor = new Color(color.r, color.g, color.b, 0.28f),
                colorMultiplier = 1f,
                fadeDuration = 0.1f
            };
            button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>
        /// Team 1 / Team 2 buttons for the local player in team mode. Hidden
        /// in individual mode and for guests who only need to see badges.
        /// </summary>
        private void BuildTeamPicker(Transform panel)
        {
            _teamPickerRoot = UiFactory.CreateRect("TeamPicker", panel).gameObject;

            var title = _ui.CreateText("TeamTitle", _teamPickerRoot.transform, string.Empty, 22, FontStyle.Normal, UiPalette.MutedText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(title.rectTransform, new Vector2(0f, -110f), new Vector2(660f, 32f));
            LocalizedText.Bind(title, "ui.play.pick_team");

            _team1Button = _ui.CreateButton(
                _teamPickerRoot.transform, "Team1", string.Empty,
                UiPalette.Secondary, UiPalette.SecondaryHighlight, () => _lobbyPresenter.RequestTeam(0));
            UiFactory.SetCenteredRect(_team1Button.GetComponent<RectTransform>(), new Vector2(-160f, -156f), new Vector2(280f, 56f));
            LocalizedText.Bind(_team1Button.GetComponentInChildren<Text>(), "ui.play.team_one");

            _team2Button = _ui.CreateButton(
                _teamPickerRoot.transform, "Team2", string.Empty,
                UiPalette.Secondary, UiPalette.SecondaryHighlight, () => _lobbyPresenter.RequestTeam(1));
            UiFactory.SetCenteredRect(_team2Button.GetComponent<RectTransform>(), new Vector2(160f, -156f), new Vector2(280f, 56f));
            LocalizedText.Bind(_team2Button.GetComponentInChildren<Text>(), "ui.play.team_two");
        }

        private void SetMatchFormat(MatchFormat format)
        {
            // Gated on hosting, not on "can start": team mode is chosen exactly
            // when the roster does not satisfy it yet, and tying the choice to
            // startability made the whole lobby list vanish on every switch.
            if (_lobbyPresenter == null || !_lobbyPresenter.IsHost)
                return;

            _lobbyPresenter.SelectedFormat = format;
            _lobbyPresenter.RefreshMembers();
            RefreshLobby();
        }

        private void RefreshLobby()
        {
            if (_lobbyRoot == null || !_lobbyRoot.activeSelf) return;
            _lobbyCode.text = $"{_text.GetText("ui.play.room_code")}: {_lobbyPresenter.RoomCode}";

            for (int i = _lobbyMembersRoot.childCount - 1; i >= 0; i--)
            {
                // Destroy is deferred to end of frame, so the outgoing rows are
                // detached first: otherwise they would still be parented here
                // while the new roster is laid out at the same offsets and the
                // list would show every member twice for a frame.
                Transform row = _lobbyMembersRoot.GetChild(i);
                row.SetParent(null, false);
                Destroy(row.gameObject);
            }

            float y = 90f;
            var members = _lobbyPresenter.Members
                .Where(static player => player != null)
                .OrderByDescending(static player => player.IsHost)
                .ThenByDescending(static player => player.isLocalPlayer)
                .ToList();
            MatchFormat format = _lobbyPresenter.SelectedFormat;
            for (int index = 0; index < members.Count; index++)
            {
                NetworkPlayer player = members[index];
                var card = UiFactory.CreateGlassPanel(_lobbyMembersRoot, "MemberCard", UiPalette.GlassRow);
                UiFactory.SetCenteredRect(card.rectTransform, new Vector2(0f, y), new Vector2(660f, 56f));

                bool isYou = player.isLocalPlayer;
                bool isHost = player.IsHost;
                string badges = string.Empty;
                if (isHost)
                    badges += $"   <color=#FA9E29>[{_text.GetText("ui.play.host_badge")}]</color>";
                if (isYou)
                    badges += $"   <color=#69C4EA>({_text.GetText("ui.play.you_badge")})</color>";
                if (format == MatchFormat.Team)
                    badges += $"   <color=#8FD694>[{_text.GetText("ui.play.team_badge")} {player.LobbyTeamId + 1}]</color>";

                AddMemberSwatch(card.transform, player.ColorId);

                string displayName = string.IsNullOrEmpty(player.DisplayName) && isYou
                    ? LocalIdentity.DisplayName
                    : player.DisplayName;
                var label = _ui.CreateText("Name", card.transform, displayName + badges, 26, FontStyle.Bold, UiPalette.LightText, TextAnchor.MiddleLeft);
                label.supportRichText = true;
                UiFactory.SetStretchRect(label.rectTransform, 72f, 0f, 24f, 0f);

                y -= 64f;
            }

            RefreshColorPicker();
            RefreshTeamPicker();

            bool weAreHost = _lobbyPresenter.IsHost;
            if (_formatIndividualButton != null)
                _formatIndividualButton.interactable = weAreHost;
            if (_formatTeamButton != null)
                _formatTeamButton.interactable = weAreHost;

            HighlightFormatButton(_formatIndividualButton, format == MatchFormat.Individual);
            HighlightFormatButton(_formatTeamButton, format == MatchFormat.Team);

            // Guests wait; hosts are told exactly what is missing, so a
            // greyed-out start button never looks like a broken one.
            string blockedReason = _lobbyPresenter.StartBlockedReason;
            if (!weAreHost)
                _lobbyStatus.text = _text.GetText("ui.play.waiting_host");
            else if (blockedReason != null)
                _lobbyStatus.text = _text.GetText(blockedReason);
            else
                _lobbyStatus.text = format == MatchFormat.Team
                    ? _text.GetText("ui.play.team_hint")
                    : string.Empty;

            var start = _lobbyRoot.transform.Find("Panel/StartMatch")?.GetComponent<Button>();
            if (start != null)
            {
                start.interactable = blockedReason == null;
                start.gameObject.SetActive(weAreHost);
            }
        }

        /// <summary>Black or white, whichever stays legible on <paramref name="background"/>.</summary>
        private static Color ContrastingTextColor(Color background) =>
            background.r * 0.299f + background.g * 0.587f + background.b * 0.114f > 0.55f
                ? Color.black
                : Color.white;

        /// <summary>Colour dot on a member card, so the roster reads at a glance.</summary>
        private static void AddMemberSwatch(Transform card, byte colorId)
        {
            var ring = UiFactory.CreateImage("ColorRing", card, UiFactory.CircleSprite, ColorRingIdle);
            UiFactory.SetAnchoredRect(
                ring.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(MemberSwatchSize + 8f, MemberSwatchSize + 8f),
                new Vector2(20f, 0f));

            var dot = UiFactory.CreateImage("ColorDot", ring.transform, UiFactory.CircleSprite, (Color)PlayerColorPalette.ColorOf(colorId));
            UiFactory.SetCenteredRect(dot.rectTransform, Vector2.zero, new Vector2(MemberSwatchSize, MemberSwatchSize));
        }

        /// <summary>
        /// Marks the local player's colour and disables the ones other members
        /// hold, so the picker never offers a choice the host would refuse.
        /// </summary>
        private void RefreshColorPicker()
        {
            if (_colorButtons == null) return;

            byte mine = _lobbyPresenter.LocalColorId;

            for (int i = 0; i < _colorButtons.Length; i++)
            {
                var colorId = (byte)i;
                bool isMine = colorId == mine;

                if (_colorButtons[i] != null)
                    _colorButtons[i].interactable = !isMine && !_lobbyPresenter.IsColorTaken(colorId);

                if (_colorRings[i] != null)
                    _colorRings[i].color = isMine ? UiPalette.WinnerGold : ColorRingIdle;
            }
        }

        private void RefreshTeamPicker()
        {
            if (_teamPickerRoot == null) return;

            MatchFormat format = _lobbyPresenter.SelectedFormat;
            bool showPicker = format == MatchFormat.Team;
            _teamPickerRoot.SetActive(showPicker);
            if (!showPicker) return;

            byte mine = _lobbyPresenter.LocalTeamId;
            HighlightFormatButton(_team1Button, mine == 0);
            HighlightFormatButton(_team2Button, mine == 1);
        }

        private static void HighlightFormatButton(Button button, bool selected)
        {
            if (button == null)
                return;

            var colors = button.colors;
            colors.normalColor = selected ? UiPalette.Primary : UiPalette.Secondary;
            colors.highlightedColor = selected ? UiPalette.PrimaryHighlight : UiPalette.SecondaryHighlight;
            button.colors = colors;
        }

        #endregion

        #region Match

        private void BuildMatch()
        {
            _matchRoot = new GameObject("Match", typeof(RectTransform));
            _matchRoot.transform.SetParent(_canvas.transform, false);
            UiFactory.Stretch(_matchRoot.GetComponent<RectTransform>());

            UiFactory.CreateFullScreenBackground(
                _matchRoot.transform,
                "In Game Backgrounds",
                UiPalette.Background);

            _matchStatus = _ui.CreateText(
                "Status", _matchRoot.transform, string.Empty, 26, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter);
            UiFactory.SetAnchoredRect(
                _matchStatus.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(720f, 40f), new Vector2(0f, -28f));
            UiFactory.AddDoubleOutline(_matchStatus.gameObject, new Vector2(3f, -3f), new Vector2(1.5f, -1.5f));

            _matchTimer = _ui.CreateText(
                "Timer", _matchRoot.transform, string.Empty, 36, FontStyle.Bold,
                UiPalette.Primary, TextAnchor.MiddleCenter);
            UiFactory.SetAnchoredRect(
                _matchTimer.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(140f, 44f), new Vector2(0f, -68f));
            UiFactory.AddDoubleOutline(_matchTimer.gameObject, new Vector2(3f, -3f), new Vector2(1.5f, -1.5f));

            var menuButton = _ui.CreateAccentButton(
                _matchRoot.transform, "Menu", string.Empty,
                UiPalette.Secondary, UiPalette.SecondaryHighlight, OpenPause, 20);
            UiFactory.SetAnchoredRect(
                menuButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(120f, 42f), new Vector2(-420f, -34f));
            LocalizedText.Bind(menuButton.GetComponentInChildren<Text>(), "ui.match.menu");

            BuildPlayerSeats();
            BuildBagHud();

            var boardFrame = UiFactory.CreateImage(
                "Board Frame",
                _matchRoot.transform,
                UiPalette.BoardFrame);
            UiFactory.SetCenteredRect(boardFrame.rectTransform, new Vector2(0f, 48f), new Vector2(736f, 736f));
            UiFactory.AddShadow(boardFrame.gameObject, new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, -10f));

            var boardRoot = UiFactory.CreateRect("Board", boardFrame.transform);
            UiFactory.SetCenteredRect(boardRoot, Vector2.zero, new Vector2(700f, 700f));
            _boardView = new MatchBoardView(_ui, boardRoot, OnCellClicked);

            var rackShelf = UiFactory.CreateImage(
                "Rack Shelf",
                _matchRoot.transform,
                UiPalette.RackShelf);
            UiFactory.SetAnchoredRect(
                rackShelf.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(720f, 96f), new Vector2(0f, 118f));
            UiFactory.AddShadow(rackShelf.gameObject, new Color(0f, 0f, 0f, 0.28f), new Vector2(0f, -6f));

            _rackRoot = UiFactory.CreateRect("Rack", rackShelf.transform).transform;
            UiFactory.Stretch((RectTransform)_rackRoot);

            _matchPreview = _ui.CreateText(
                "Preview", _matchRoot.transform, string.Empty, 16, FontStyle.Normal,
                Color.white, TextAnchor.MiddleCenter);
            _matchPreview.horizontalOverflow = HorizontalWrapMode.Wrap;
            _matchPreview.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.SetAnchoredRect(
                _matchPreview.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(640f, 40f), new Vector2(0f, 258f));
            UiFactory.AddOutline(_matchPreview.gameObject, Color.black, new Vector2(1.5f, -1.5f));

            _declareRoot = UiFactory.CreateRect("Declare", _matchRoot.transform).transform;
            UiFactory.SetAnchoredRect(
                (RectTransform)_declareRoot,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(1000f, 100f), new Vector2(0f, 300f));
            _declareRoot.gameObject.SetActive(false);

            float bx = -340f;
            _confirmButton = CreateMatchAction("Confirm", "ui.match.confirm", UiPalette.Success, UiPalette.SuccessHighlight, () => _matchPresenter.ConfirmPlace(), ref bx);
            _clearButton = CreateMatchAction("Clear", "ui.match.clear", UiPalette.Secondary, UiPalette.SecondaryHighlight, () => _matchPresenter.ClearDraft(), ref bx);
            _passButton = CreateMatchAction("Pass", "ui.match.pass", UiPalette.Secondary, UiPalette.SecondaryHighlight, () => _matchPresenter.Pass(), ref bx);
            _exchangeButton = CreateMatchAction("Exchange", "ui.match.exchange", UiPalette.Primary, UiPalette.PrimaryHighlight, ToggleExchangeMode, ref bx);

            _matchRoot.SetActive(false);
        }

        private void BuildPlayerSeats()
        {
            for (int i = 0; i < VisibleSeatCount; i++)
            {
                var seat = new PlayerSeatHud();
                var root = UiFactory.CreateRect($"Seat{i + 1}", _matchRoot.transform);
                Vector2 anchor = SeatAnchors[i];
                UiFactory.SetAnchoredRect(
                    root,
                    anchor, anchor, SeatPivots[i],
                    new Vector2(170f, 170f),
                    SeatPositions[i]);

                bool scoreBelow = anchor.y > 0.5f;
                float avatarY = scoreBelow ? 28f : -28f;
                float scoreY = scoreBelow ? -52f : 52f;

                var ring = UiFactory.CreateImage(
                    "TurnRing", root, UiFactory.CircleSprite, new Color(UiPalette.TurnRing.r, UiPalette.TurnRing.g, UiPalette.TurnRing.b, 0f));
                UiFactory.SetCenteredRect(ring.rectTransform, new Vector2(0f, avatarY), new Vector2(126f, 126f));

                // Constant dark band between the turn ring and the avatar, so a
                // player who picked black or grey still has a visible outline.
                var outline = UiFactory.CreateImage("AvatarOutline", root, UiFactory.CircleSprite, ColorRingIdle);
                UiFactory.SetCenteredRect(outline.rectTransform, new Vector2(0f, avatarY), new Vector2(112f, 112f));

                // Actual colour is applied per player in RefreshPlayerSeats.
                var avatar = UiFactory.CreateImage("Avatar", root, UiFactory.CircleSprite, UiPalette.MutedText);
                UiFactory.SetCenteredRect(avatar.rectTransform, new Vector2(0f, avatarY), new Vector2(104f, 104f));
                UiFactory.AddShadow(avatar.gameObject, new Color(0f, 0f, 0f, 0.30f), new Vector2(0f, -4f));

                var label = _ui.CreateText(
                    "Label", root, $"P{i + 1}", 34, FontStyle.Bold,
                    Color.black, TextAnchor.MiddleCenter);
                UiFactory.SetCenteredRect(label.rectTransform, new Vector2(0f, avatarY), new Vector2(100f, 48f));

                var score = _ui.CreateText(
                    "Score", root, string.Empty, 22, FontStyle.Bold,
                    Color.white, TextAnchor.MiddleCenter);
                UiFactory.SetCenteredRect(score.rectTransform, new Vector2(0f, scoreY), new Vector2(160f, 36f));
                UiFactory.AddDoubleOutline(score.gameObject, new Vector2(2.5f, -2.5f), new Vector2(1.2f, -1.2f));

                seat.Root = root.gameObject;
                seat.Avatar = avatar;
                seat.Label = label;
                seat.Score = score;
                seat.TurnRing = ring;
                seat.Root.SetActive(false);
                _seats[i] = seat;
            }
        }

        private void BuildBagHud()
        {
            var bagRoot = UiFactory.CreateRect("Bag", _matchRoot.transform);
            UiFactory.SetAnchoredRect(
                bagRoot,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(150f, 150f), new Vector2(-36f, 40f));

            Sprite bagSprite = TileIcons.Bag();
            Image bagIcon = bagSprite != null
                ? UiFactory.CreateImage("Icon", bagRoot, bagSprite)
                : UiFactory.CreateImage("Icon", bagRoot, new Color(0.95f, 0.55f, 0.70f, 1f));
            bagIcon.preserveAspect = true;
            UiFactory.SetCenteredRect(bagIcon.rectTransform, new Vector2(0f, 10f), new Vector2(130f, 120f));

            _bagLabel = _ui.CreateText(
                "Count", bagRoot, "0", 28, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_bagLabel.rectTransform, new Vector2(0f, -58f), new Vector2(120f, 36f));
            UiFactory.AddDoubleOutline(_bagLabel.gameObject, new Vector2(2.5f, -2.5f), new Vector2(1.2f, -1.2f));
        }

        private Button CreateMatchAction(
            string name,
            string labelKey,
            Color color,
            Color highlight,
            UnityEngine.Events.UnityAction action,
            ref float x)
        {
            var button = _ui.CreateAccentButton(
                _matchRoot.transform, name, string.Empty, color, highlight, () => action(), 18);
            UiFactory.SetAnchoredRect(
                button.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(160f, 50f), new Vector2(x, 230f));
            LocalizedText.Bind(button.GetComponentInChildren<Text>(), labelKey);
            x += 172f;
            return button;
        }

        private void PulseTurnRings()
        {
            float pulse = 0.72f + 0.28f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.2f));
            for (int i = 0; i < VisibleSeatCount; i++)
            {
                PlayerSeatHud seat = _seats[i];
                if (seat?.TurnRing == null || !seat.Root.activeSelf)
                    continue;

                Color c = seat.TurnRing.color;
                if (c.a < 0.05f)
                    continue;

                c.a = pulse;
                seat.TurnRing.color = c;
            }
        }

        private void ClearExchangeState()
        {
            _exchangeMode = false;
            _exchangeSelection.Clear();
        }

        private void ToggleExchangeMode()
        {
            _exchangeMode = !_exchangeMode;
            if (!_exchangeMode && _exchangeSelection.Count > 0)
            {
                _matchPresenter.ExchangeSelected(_exchangeSelection);
                _exchangeSelection.Clear();
            }

            RefreshMatch();
        }

        private void OnCellClicked(int x, int y)
        {
            if (_matchPresenter == null || !_matchPresenter.IsMyTurnReady) return;

            TurnInputSession input = _matchPresenter.TurnInput;
            if (input == null) return;

            for (int i = 0; i < input.PendingPlacements.Count; i++)
            {
                if (input.PendingPlacements[i].X == x && input.PendingPlacements[i].Y == y)
                {
                    _matchPresenter.RemovePending(x, y);
                    return;
                }
            }

            _matchPresenter.PlaceCell(x, y);
        }

        private void RefreshMatch()
        {
            if (_matchRoot == null || !_matchRoot.activeSelf || _matchPresenter == null) return;

            bool myTurn = _matchPresenter.IsMyTurnReady;

            // Exchange is a this-turn action. Left armed across a turn change or a
            // pause it would fire on the next turn against a stale selection, so
            // losing the turn disarms it.
            if (!myTurn)
                ClearExchangeState();

            string statusText = myTurn
                ? _text.GetText("ui.match.your_turn")
                : _text.GetText("ui.match.wait_turn");
            _matchStatus.text = $"{_text.GetText("ui.match.turn")} {_matchPresenter.TurnNumber}  •  {statusText}";

            // A paused match looks identical to a hung one, so say why the board
            // stopped responding — it outranks both the turn line and any error.
            if (_matchPresenter.IsWaitingForPlayers)
                _matchStatus.text = DescribeWaitingForPlayers();
            else if (!string.IsNullOrEmpty(_matchPresenter.LastError))
                _matchStatus.text = PlacementPreviewFormatter.LocalizeError(_matchPresenter.LastError);

            UpdateTimerAndBag();
            RefreshPlayerSeats();

            // Action buttons only work on your turn; graying them out makes the
            // turn state readable at a glance.
            _confirmButton.interactable = myTurn && !_exchangeMode;
            _clearButton.interactable = myTurn && !_exchangeMode;
            _passButton.interactable = myTurn && !_exchangeMode;
            _exchangeButton.interactable = myTurn;
            HighlightFormatButton(_exchangeButton, _exchangeMode);

            BoardManager boardManager = null;
            NetworkContext.Services?.TryResolve(out boardManager);

            TurnInputSession input = _matchPresenter.TurnInput;
            _boardView?.Refresh(boardManager?.Grid, input);

            // Preview text — equation validity + per-tile A-Math points
            if (input != null && input.PendingPlacements.Count > 0)
            {
                _matchPreview.text = PlacementPreviewFormatter.FormatPreview(
                    input.PreviewValidation,
                    input.PreviewBreakdown);
            }
            else
            {
                _matchPreview.text = _text.GetText(_exchangeMode ? "ui.match.exchange_hint" : "ui.match.idle_hint");
            }

            RefreshRack();
            RefreshDeclareBar();
        }

        /// <summary>
        /// Only the host runs the wait countdown, so clients get the reason
        /// without a timer rather than a misleading one.
        /// </summary>
        private string DescribeWaitingForPlayers()
        {
            float remaining = _matchPresenter.WaitingSecondsRemaining;
            if (remaining <= 0f)
                return _text.GetText("ui.match.waiting_players");

            return string.Format(
                _text.GetText("ui.match.waiting_players_countdown"),
                Mathf.CeilToInt(remaining));
        }

        private void RefreshPlayerSeats()
        {
            IReadOnlyList<PlayerState> players = _matchPresenter.Players?.Players;
            string scorePrefix = _text.GetText("ui.match.score");

            // Local player always sits in the mockup's P1 (bottom-left) seat.
            var ordered = new List<PlayerState>(VisibleSeatCount);
            if (players != null)
            {
                PlayerState local = null;
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].PlayerId == _matchPresenter.LocalPlayerId)
                    {
                        local = players[i];
                        break;
                    }
                }

                if (local != null)
                    ordered.Add(local);

                for (int i = 0; i < players.Count; i++)
                {
                    if (local != null && players[i].PlayerId == local.PlayerId)
                        continue;
                    if (ordered.Count >= VisibleSeatCount)
                        break;
                    ordered.Add(players[i]);
                }
            }

            for (int i = 0; i < VisibleSeatCount; i++)
            {
                PlayerSeatHud seat = _seats[i];
                if (seat == null) continue;

                if (i >= ordered.Count)
                {
                    seat.Root.SetActive(false);
                    continue;
                }

                PlayerState player = ordered[i];
                seat.Root.SetActive(true);

                bool isLocal = player.PlayerId == _matchPresenter.LocalPlayerId;
                bool isCurrent = player.PlayerId == _matchPresenter.CurrentPlayerId;
                string you = isLocal ? $" ({_text.GetText("ui.match.you")})" : string.Empty;

                seat.Label.text = $"P{i + 1}";
                seat.Score.text = $"{scorePrefix} {player.Score}{you}";

                // Keyed off the player in this slot, not the slot index: the
                // local player is always remapped to slot 0, so indexing by slot
                // would show everyone their own colour on the wrong avatar.
                seat.Avatar.color = PlayerColorPalette.ColorOf(player.ColorId);

                // The palette spans white-ish yellow to near-black, so the seat
                // number has to flip rather than stay a fixed colour.
                seat.Label.color = ContrastingTextColor(seat.Avatar.color);

                Color ring = UiPalette.TurnRing;
                ring.a = isCurrent ? 0.95f : 0f;
                seat.TurnRing.color = ring;
            }
        }

        /// <summary>
        /// Updates the rack in place. The rack refreshes on every selection,
        /// draft edit and turn change, so rebuilding the buttons each time was
        /// a steady source of GC churn during play.
        /// </summary>
        private void RefreshRack()
        {
            PlayerState local = _matchPresenter.Players?.GetById(_matchPresenter.LocalPlayerId);
            int tileCount = local?.Rack.Count ?? 0;
            EnsureRackButtons(tileCount);

            bool myTurn = _matchPresenter.IsMyTurnReady;
            float start = -((tileCount - 1) * RackPitch) * 0.5f;
            for (int i = 0; i < _rackButtons.Count; i++)
            {
                Button button = _rackButtons[i];
                if (i >= tileCount)
                {
                    button.gameObject.SetActive(false);
                    continue;
                }

                byte tileId = local.Rack[i];
                bool selected = _matchPresenter.TurnInput?.SelectedRackIndex == i
                               || _exchangeSelection.Contains(i);

                button.gameObject.SetActive(true);
                button.interactable = myTurn;

                Sprite sprite = TileIcons.ForTile(tileId);
                Image image = _rackImages[i];
                Text label = _rackLabels[i];
                if (sprite != null)
                {
                    image.sprite = sprite;
                    image.preserveAspect = true;
                    image.color = selected
                        ? new Color(1f, 0.92f, 0.55f, 1f)
                        : Color.white;
                    label.text = string.Empty;
                }
                else
                {
                    image.sprite = null;
                    image.preserveAspect = false;
                    image.color = Color.white;
                    SetRackButtonColor(button, selected ? UiPalette.Primary : UiPalette.CellOccupied);
                    label.text = $"{SymbolOf(tileId)}\n{PointsOf(tileId)}";
                }

                UiFactory.SetCenteredRect(
                    button.GetComponent<RectTransform>(),
                    new Vector2(start + i * RackPitch, 0f),
                    new Vector2(70f, 84f));
            }
        }

        private void EnsureRackButtons(int required)
        {
            while (_rackButtons.Count < required)
            {
                int index = _rackButtons.Count;
                Button button = UiFactory.CreateIconButton(
                    _rackRoot, $"R{index}", null,
                    UiPalette.CellOccupied,
                    () => OnRackClicked(index));

                var label = _ui.CreateText(
                    "Label", button.transform, string.Empty, 18, FontStyle.Normal,
                    Color.white, TextAnchor.MiddleCenter);
                UiFactory.Stretch(label.rectTransform);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;

                _rackButtons.Add(button);
                _rackLabels.Add(label);
                _rackImages.Add(button.GetComponent<Image>());
            }
        }

        private static void SetRackButtonColor(Button button, Color normal)
        {
            ColorBlock colors = button.colors;
            if (colors.normalColor == normal) return;

            colors.normalColor = normal;
            colors.pressedColor = Color.Lerp(normal, Color.black, 0.16f);
            button.colors = colors;
        }

        private void OnRackClicked(int index)
        {
            if (_exchangeMode)
            {
                if (!_exchangeSelection.Remove(index))
                    _exchangeSelection.Add(index);
                RefreshMatch();
                return;
            }

            _matchPresenter.SelectRack(index);
            RefreshDeclareBar();
        }

        private void RefreshDeclareBar()
        {
            for (int i = _declareRoot.childCount - 1; i >= 0; i--)
                Destroy(_declareRoot.GetChild(i).gameObject);

            byte? selected = _matchPresenter.TurnInput?.SelectedTileId;
            if (!selected.HasValue || !RequiresDeclaration(selected.Value))
            {
                _declareRoot.gameObject.SetActive(false);
                return;
            }

            _declareRoot.gameObject.SetActive(true);
            var title = _ui.CreateText("DeclareTitle", _declareRoot, _text.GetText("ui.match.declare"), 20, FontStyle.Bold, UiPalette.MutedText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(title.rectTransform, new Vector2(0f, 34f), new Vector2(500f, 34f));

            byte tileId = selected.Value;
            float x = -420f;
            void AddOption(byte declaredAs)
            {
                byte value = declaredAs;
                var button = _ui.CreateButton(_declareRoot, $"D{value}", SymbolOf(value), UiPalette.Card, UiPalette.PrimaryHighlight, () =>
                {
                    _matchPresenter.SetDeclaration(value);
                }, 22);
                UiFactory.SetCenteredRect(button.GetComponent<RectTransform>(), new Vector2(x, -16f), new Vector2(52f, 48f));
                x += 58f;
            }

            if (tileId == PlusOrMinus)
            {
                AddOption(Plus);
                AddOption(Minus);
            }
            else if (tileId == TimesOrDivide)
            {
                AddOption(Times);
                AddOption(Divide);
            }
            else if (tileId == Blank)
            {
                for (byte n = 0; n <= 9; n++)
                    AddOption(n);
                AddOption(EqualsSign);
                AddOption(Plus);
                AddOption(Minus);
                AddOption(Times);
                AddOption(Divide);
            }
        }

        #endregion

        #region Pause

        private void BuildPause()
        {
            _pauseRoot = new GameObject("Pause", typeof(RectTransform));
            _pauseRoot.transform.SetParent(_canvas.transform, false);
            UiFactory.Stretch(_pauseRoot.GetComponent<RectTransform>());

            var overlay = UiFactory.CreateImage("Overlay", _pauseRoot.transform, UiPalette.Overlay);
            UiFactory.Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;

            var title = _ui.CreateOutlinedTitle(_pauseRoot.transform, "Title", string.Empty, 64);
            UiFactory.SetCenteredRect(title.rectTransform, new Vector2(0f, 180f), new Vector2(520f, 80f));
            LocalizedText.Bind(title, "ui.pause.title");

            _pauseResumeButton = _ui.CreateTextMenuButton(_pauseRoot.transform, "Resume", string.Empty, 42, ClosePause);
            LocalizedText.Bind(_pauseResumeButton.GetComponentInChildren<Text>(), "ui.pause.resume");

            // Host-only escape hatch from a match that is waiting on players who
            // dropped out; hidden the rest of the time.
            _pausePlayOnButton = _ui.CreateTextMenuButton(_pauseRoot.transform, "PlayOn", string.Empty, 36, PlayOnWithoutMissingPlayers);
            LocalizedText.Bind(_pausePlayOnButton.GetComponentInChildren<Text>(), "ui.pause.play_on");

            _pauseSettingsButton = _ui.CreateTextMenuButton(_pauseRoot.transform, "Settings", string.Empty, 42, OpenMatchSettings);
            LocalizedText.Bind(_pauseSettingsButton.GetComponentInChildren<Text>(), "ui.pause.settings");

            _pauseLeaveButton = _ui.CreateTextMenuButton(_pauseRoot.transform, "Leave", string.Empty, 42, () =>
            {
                OverlayFade.Ensure(_pauseRoot)?.HideInstant();
                _matchPresenter.LeaveRoom();
                ReturnToMenu();
            });
            LocalizedText.Bind(_pauseLeaveButton.GetComponentInChildren<Text>(), "ui.pause.leave");

            OverlayFade.Ensure(_pauseRoot);
            _pauseRoot.SetActive(false);
        }

        private void OpenPause()
        {
            if (_screen != ScreenId.Match) return;
            OverlayFade.Ensure(_pauseRoot).FadeIn();
            LayoutPauseButtons();
            UiFactory.Select(_pauseResumeButton);
        }

        /// <summary>
        /// Places and links only the entries this pause menu is currently
        /// offering, so hiding the host action leaves neither a gap in the card
        /// nor a dead stop in keyboard navigation.
        /// </summary>
        private void LayoutPauseButtons()
        {
            const float buttonPitch = 78f;
            const float firstButtonY = 70f;

            bool canPlayOn = _matchPresenter != null
                && _matchPresenter.IsWaitingForPlayers
                && _matchPresenter.WaitingSecondsRemaining > 0f;
            _pausePlayOnButton.gameObject.SetActive(canPlayOn);

            _visiblePauseButtons.Clear();
            _visiblePauseButtons.Add(_pauseResumeButton);
            if (canPlayOn) _visiblePauseButtons.Add(_pausePlayOnButton);
            _visiblePauseButtons.Add(_pauseSettingsButton);
            _visiblePauseButtons.Add(_pauseLeaveButton);

            for (int i = 0; i < _visiblePauseButtons.Count; i++)
            {
                UiFactory.SetCenteredRect(
                    _visiblePauseButtons[i].GetComponent<RectTransform>(),
                    new Vector2(0f, firstButtonY - i * buttonPitch),
                    new Vector2(520f, 64f));
            }

            for (int i = 0; i < _visiblePauseButtons.Count; i++)
            {
                int previous = (i - 1 + _visiblePauseButtons.Count) % _visiblePauseButtons.Count;
                int next = (i + 1) % _visiblePauseButtons.Count;
                UiFactory.SetVerticalNavigation(
                    _visiblePauseButtons[i],
                    _visiblePauseButtons[previous],
                    _visiblePauseButtons[next]);
            }
        }

        private void PlayOnWithoutMissingPlayers()
        {
            _matchPresenter.ResumeWithoutMissingPlayers();
            ClosePause();
        }

        private void ClosePause()
        {
            OverlayFade.Ensure(_pauseRoot).FadeOut();
        }

        private void OpenMatchSettings()
        {
            OverlayFade.Ensure(_pauseRoot)?.HideInstant();
            if (_matchSettings == null)
            {
                _matchSettings = SettingsMenuController.Create(transform, _ui.Font);
                _matchSettings.Closed += OnMatchSettingsClosed;
            }

            _matchSettings.Open();
        }

        private void OnMatchSettingsClosed()
        {
            _settingsClosedFrame = Time.frameCount;
            if (_screen == ScreenId.Match)
            {
                RefreshMatch();
                OpenPause();
            }
        }

        #endregion

        #region Result / Recovery

        private void BuildResult()
        {
            _resultRoot = new GameObject("Result", typeof(RectTransform));
            _resultRoot.transform.SetParent(_canvas.transform, false);
            UiFactory.Stretch(_resultRoot.GetComponent<RectTransform>());

            var overlay = UiFactory.CreateImage("Overlay", _resultRoot.transform, UiPalette.Overlay);
            UiFactory.Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;

            var winnerLabel = _ui.CreateOutlinedTitle(
                _resultRoot.transform, "WinnerLabel", string.Empty, 56);
            winnerLabel.color = UiPalette.WinnerGold;
            UiFactory.SetCenteredRect(winnerLabel.rectTransform, new Vector2(0f, 160f), new Vector2(720f, 72f));
            LocalizedText.Bind(winnerLabel, "ui.result.winner");

            _resultWinner = _ui.CreateOutlinedTitle(
                _resultRoot.transform, "Winner", string.Empty, 48);
            UiFactory.SetCenteredRect(_resultWinner.rectTransform, new Vector2(0f, 80f), new Vector2(720f, 64f));

            _resultMeta = _ui.CreateText(
                "Meta", _resultRoot.transform, string.Empty, 28, FontStyle.Normal,
                Color.white, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_resultMeta.rectTransform, new Vector2(0f, 20f), new Vector2(720f, 48f));
            UiFactory.AddDoubleOutline(_resultMeta.gameObject, new Vector2(2.5f, -2.5f), new Vector2(1.2f, -1.2f));

            _resultBody = _ui.CreateText(
                "Body", _resultRoot.transform, string.Empty, 26, FontStyle.Normal,
                UiPalette.LightText, TextAnchor.UpperCenter);
            _resultBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _resultBody.verticalOverflow = VerticalWrapMode.Overflow;
            _resultBody.supportRichText = true;
            UiFactory.SetCenteredRect(_resultBody.rectTransform, new Vector2(0f, -120f), new Vector2(640f, 220f));

            var rematchButton = _ui.CreateAccentButton(
                _resultRoot.transform, "Rematch", string.Empty,
                UiPalette.Success, UiPalette.SuccessHighlight, () =>
                {
                    if (_resultPresenter.CanRematch)
                        _resultPresenter.Rematch();
                }, 26);
            UiFactory.SetCenteredRect(rematchButton.GetComponent<RectTransform>(), new Vector2(0f, -280f), new Vector2(360f, 60f));
            LocalizedText.Bind(rematchButton.GetComponentInChildren<Text>(), "ui.result.rematch");

            var leaveButton = _ui.CreateAccentButton(
                _resultRoot.transform, "Leave", string.Empty,
                UiPalette.Secondary, UiPalette.SecondaryHighlight, () =>
                {
                    _resultPresenter.Leave();
                    ReturnToMenu();
                }, 26);
            UiFactory.SetCenteredRect(leaveButton.GetComponent<RectTransform>(), new Vector2(0f, -360f), new Vector2(320f, 56f));
            LocalizedText.Bind(leaveButton.GetComponentInChildren<Text>(), "ui.result.leave");

            OverlayFade.Ensure(_resultRoot);
            _resultRoot.SetActive(false);
        }

        private void RefreshResult()
        {
            MatchResult result = _resultPresenter.Result;
            if (result == null)
            {
                _resultWinner.text = string.Empty;
                _resultMeta.text = string.Empty;
                _resultBody.text = string.Empty;
                return;
            }

            PlayerResult winner = null;
            foreach (PlayerResult row in result.Standings)
            {
                if (row.PlayerId == result.WinnerPlayerId)
                {
                    winner = row;
                    break;
                }
            }

            if (result.Format == MatchFormat.Team && result.WinnerTeamId >= 0)
            {
                _resultWinner.text = string.Format(
                    _text.GetText("ui.result.team_winner"),
                    result.WinnerTeamId + 1);
            }
            else
            {
                _resultWinner.text = winner != null ? winner.DisplayName : $"#{result.WinnerPlayerId}";
            }

            string formatLabel = result.Format == MatchFormat.Team
                ? _text.GetText("ui.play.format_team")
                : _text.GetText("ui.play.format_individual");
            int minutes = result.DurationSeconds / 60;
            int seconds = result.DurationSeconds % 60;
            string duration = minutes > 0
                ? string.Format(_text.GetText("ui.result.duration_min"), minutes, seconds)
                : string.Format(_text.GetText("ui.result.duration_sec"), seconds);
            _resultMeta.text = $"{formatLabel}  •  {duration}";

            var sb = new System.Text.StringBuilder();
            if (result.Format == MatchFormat.Team && result.TeamStandings.Count > 0)
            {
                sb.AppendLine(_text.GetText("ui.result.team_standings"));
                sb.AppendLine();
                foreach (TeamResult team in result.TeamStandings)
                {
                    string line = $"{_text.GetText("ui.play.team_badge")} {team.TeamId + 1}: {team.TotalScore}";
                    if (team.TeamId == result.WinnerTeamId)
                        line = $"<color=#FDA733><b>{line}</b></color>";
                    sb.AppendLine(line);
                }

                sb.AppendLine();
            }

            sb.AppendLine(_text.GetText("ui.result.standings"));
            sb.AppendLine();
            int rank = 1;
            foreach (PlayerResult row in result.Standings.OrderByDescending(r => r.FinalScore))
            {
                string teamSuffix = row.TeamId >= 0 ? $" ({_text.GetText("ui.play.team_badge")} {row.TeamId + 1})" : string.Empty;
                string line = $"{rank}.  {row.DisplayName}{teamSuffix}   {row.FinalScore}";
                if (row.PlayerId == result.WinnerPlayerId)
                    line = $"<color=#FDA733><b>{line}</b></color>";
                sb.AppendLine(line);
                rank++;
            }

            _resultBody.text = sb.ToString();

            var rematch = _resultRoot.transform.Find("Rematch")?.GetComponent<Button>();
            if (rematch != null) rematch.interactable = _resultPresenter.CanRematch;
        }

        private void BuildRecovery()
        {
            var shell = UiFactory.CreateOverlayShell(
                _canvas.transform, "Recovery", includeGlassCard: true,
                cardSize: new Vector2(640f, 420f), glassColor: UiPalette.GlassStrong);
            _recoveryRoot = shell.Root;
            var card = shell.Card.transform;

            var title = _ui.CreateOutlinedTitle(card, "Title", string.Empty, 40);
            UiFactory.SetCenteredRect(title.rectTransform, new Vector2(0f, 130f), new Vector2(560f, 60f));
            LocalizedText.Bind(title, "ui.recovery.title");

            _recoveryStatus = _ui.CreateText("Status", card, string.Empty, 24, FontStyle.Normal, UiPalette.MutedText, TextAnchor.MiddleCenter);
            _recoveryStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.SetCenteredRect(_recoveryStatus.rectTransform, new Vector2(0f, 20f), new Vector2(560f, 120f));

            var leaveButton = _ui.CreateAccentButton(card, "Leave", string.Empty, UiPalette.Secondary, UiPalette.SecondaryHighlight, () =>
            {
                _recoveryPresenter.LeaveRoom();
                SetBrowserNotice(_text.GetText("ui.play.connection_lost"));
                ShowBrowser();
            }, 24);
            UiFactory.SetCenteredRect(leaveButton.GetComponent<RectTransform>(), new Vector2(0f, -90f), new Vector2(420f, 64f));
            LocalizedText.Bind(leaveButton.GetComponentInChildren<Text>(), "ui.recovery.leave");

            _recoveryEndButton = _ui.CreateAccentButton(card, "End", string.Empty, UiPalette.Danger, UiPalette.DangerHighlight, () =>
            {
                // Without a running match there is nothing to settle, and
                // "ending" one would publish an empty result that reads as a
                // defeat. Leaving is the only sensible action from a lobby.
                if (!_recoveryPresenter.MatchWasRunning)
                {
                    _recoveryPresenter.LeaveRoom();
                    SetBrowserNotice(_text.GetText("ui.play.connection_lost"));
                    ShowBrowser();
                    return;
                }

                _recoveryPresenter.EndMatchNow();
                ShowResult();
            }, 24);
            UiFactory.SetCenteredRect(_recoveryEndButton.GetComponent<RectTransform>(), new Vector2(0f, -170f), new Vector2(420f, 64f));
            LocalizedText.Bind(_recoveryEndButton.GetComponentInChildren<Text>(), "ui.recovery.end");
        }

        private void OnRecoveryStatus(RecoveryPhase phase, string _, float elapsed)
        {
            bool show = phase != RecoveryPhase.Idle && phase != RecoveryPhase.Recovered;
            if (show)
                OverlayFade.Ensure(_recoveryRoot).FadeIn();
            else
                OverlayFade.Ensure(_recoveryRoot).HideInstant();

            string key = phase switch
            {
                RecoveryPhase.GraceWait => "ui.recovery.grace",
                RecoveryPhase.Searching => "ui.recovery.search",
                RecoveryPhase.Reconnecting => "ui.recovery.reconnect",
                RecoveryPhase.Recovered => "ui.recovery.recovered",
                _ => "ui.recovery.grace"
            };
            _recoveryStatus.text = $"{_text.GetText(key)}\n({elapsed:0}s)";

            if (_recoveryEndButton != null)
                _recoveryEndButton.gameObject.SetActive(_recoveryPresenter.MatchWasRunning);
        }

        #endregion

        private GameObject ForestScreen(string name)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(_canvas.transform, false);
            UiFactory.Stretch(root.GetComponent<RectTransform>());
            UiFactory.CreateFullScreenBackground(root.transform, "Main Menu Backgrounds", UiPalette.Background);
            root.SetActive(false);
            return root;
        }
    }
}
