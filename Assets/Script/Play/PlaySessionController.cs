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
    /// Code-driven multiplayer play flow: room browser → lobby → match HUD →
    /// results, plus pause and connection-lost overlays. Bootstraps
    /// <see cref="NetworkedGameContext"/> when the scene has none.
    /// </summary>
    public sealed class PlaySessionController : MonoBehaviour
    {
        private enum ScreenId
        {
            None,
            Browser,
            Lobby,
            Match,
            Result
        }

        private const float TurnWarningSeconds = 10f;

        /// <summary>Horizontal distance between rack tiles.</summary>
        private const float RackPitch = 70f;

        private static readonly Color TimerWarning = new(0.95f, 0.35f, 0.30f, 1f);

        private UiFactory _ui;
        private ILocalizedTextProvider _text;
        private Canvas _canvas;

        private GameObject _browserRoot;
        private GameObject _lobbyRoot;
        private GameObject _matchRoot;
        private GameObject _resultRoot;
        private GameObject _recoveryRoot;
        private GameObject _pauseRoot;

        private RoomBrowserPresenter _browserPresenter;
        private LobbyPresenter _lobbyPresenter;
        private MatchHudPresenter _matchPresenter;
        private MatchResultPresenter _resultPresenter;
        private ConnectionLostPresenter _recoveryPresenter;
        private SettingsMenuController _matchSettings;

        private Text _browserStatus;
        private Text _lobbyStatus;
        private Text _lobbyCode;
        private Transform _lobbyMembersRoot;
        private Transform _roomListRoot;
        private InputField _roomNameField;
        private InputField _joinCodeField;

        private Text _matchStatus;
        private Text _matchTimer;
        private Text _bagLabel;
        private Text _matchPreview;
        private Text _scoreboard;
        private Transform _rackRoot;
        private Button _confirmButton;
        private Button _clearButton;
        private Button _passButton;
        private Button _exchangeButton;
        private MatchBoardView _boardView;
        private readonly List<int> _exchangeSelection = new();
        private readonly List<Button> _rackButtons = new(GameRules.RackSize);
        private readonly List<Text> _rackLabels = new(GameRules.RackSize);
        private bool _exchangeMode;
        private Transform _declareRoot;

        private Text _resultWinner;
        private Text _resultBody;
        private Text _recoveryStatus;

        private ScreenId _screen = ScreenId.None;
        private IEventBus _eventBus;
        private MainMenuController _mainMenu;
        private InputAction _cancelAction;
        private int _settingsClosedFrame = -1;

        // Stored delegates so OnDestroy can unsubscribe exactly what was registered.
        private Action<HostStartedEvent> _onHostStarted;
        private Action<ClientConnectedEvent> _onClientConnected;
        private Action<MatchStartedEvent> _onMatchStarted;
        private Action<HostStoppedEvent> _onHostStopped;
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
            ShowBrowser();
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

            BuildBrowser();
            BuildLobby();
            BuildMatch();
            BuildResult();
            BuildRecovery();
            BuildPause();

            _onBrowserError = msg => _browserStatus.text = _text.GetText(msg);
            _onMembersChanged = _ => RefreshLobby();
            _onMatchError = msg =>
            {
                if (_matchStatus != null)
                    _matchStatus.text = PlacementPreviewFormatter.LocalizeError(msg);
            };
            _onResultChanged = _ => ShowResult();
            _onRecoveryFinished = ok =>
            {
                if (ok) _recoveryRoot.SetActive(false);
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
                _onClientConnected = _ => ShowLobby();
                _onMatchStarted = _ => ShowMatch();
                _onHostStopped = _ => ReturnToMenu();
                _eventBus.Subscribe(_onHostStarted);
                _eventBus.Subscribe(_onClientConnected);
                _eventBus.Subscribe(_onMatchStarted);
                _eventBus.Subscribe(_onHostStopped);
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
                _eventBus.Unsubscribe(_onHostStopped);
            }
        }

        private void Update()
        {
            if (_screen == ScreenId.Match && _matchRoot != null && _matchRoot.activeSelf && _matchPresenter != null)
            {
                UpdateTimerAndBag();
                HandlePauseInput();
            }
        }

        private void UpdateTimerAndBag()
        {
            if (_matchTimer == null) return;

            float remaining = _matchPresenter.RemainingTurnSeconds;
            _matchTimer.text = $"{Mathf.CeilToInt(remaining)}s";
            _matchTimer.color = remaining <= TurnWarningSeconds && remaining > 0f
                ? TimerWarning
                : UiPalette.Primary;

            if (_bagLabel != null)
                _bagLabel.text = $"{_text.GetText("ui.match.bag")}  {_matchPresenter.BagCount}";
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
                        _browserStatus.text = _text.GetText(_browserPresenter.NoRoomsFound ? "ui.play.no_rooms" : "ui.play.searching");
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

        private void ShowBrowser()
        {
            _screen = ScreenId.Browser;
            SetActiveScreens(browser: true);
            _browserPresenter.StartSearching();
            _browserStatus.text = _text.GetText("ui.play.searching");
        }

        private void ShowLobby()
        {
            _screen = ScreenId.Lobby;
            _browserPresenter.StopSearching();
            SetActiveScreens(lobby: true);
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
            SetActiveScreens(result: true);
            _pauseRoot.SetActive(false);
            RefreshResult();
        }

        private void SetActiveScreens(bool browser = false, bool lobby = false, bool match = false, bool result = false)
        {
            _browserRoot.SetActive(browser);
            _lobbyRoot.SetActive(lobby);
            _matchRoot.SetActive(match);
            _resultRoot.SetActive(result);
        }

        private void ReturnToMenu()
        {
            _browserPresenter?.StopSearching();
            SetActiveScreens();
            _recoveryRoot.SetActive(false);
            _pauseRoot.SetActive(false);
            _screen = ScreenId.None;
            if (_mainMenu != null)
            {
                _mainMenu.gameObject.SetActive(true);
                Destroy(gameObject);
            }
        }

        #endregion

        #region Browser

        private void BuildBrowser()
        {
            _browserRoot = Panel("Browser", "ui.play.browser_title");
            var panel = _browserRoot.transform.Find("Panel");

            _roomNameField = _ui.CreateInputField(panel, "RoomName", "A-Math Room", string.Empty);
            UiFactory.SetCenteredRect(_roomNameField.GetComponent<RectTransform>(), new Vector2(0f, 220f), new Vector2(520f, 64f));
            if (_roomNameField.placeholder is Text roomNameHint)
                LocalizedText.Bind(roomNameHint, "ui.play.room_name");

            var createButton = _ui.CreateButton(panel, "Create", string.Empty, UiPalette.Primary, UiPalette.PrimaryHighlight, () =>
            {
                _browserStatus.text = _text.GetText("ui.play.connecting");
                _browserPresenter.CreateRoom(_roomNameField.text, GameRules.MaxPlayers);
            });
            UiFactory.SetCenteredRect(createButton.GetComponent<RectTransform>(), new Vector2(0f, 140f), new Vector2(520f, 70f));
            LocalizedText.Bind(createButton.GetComponentInChildren<Text>(), "ui.play.create");

            _joinCodeField = _ui.CreateInputField(panel, "JoinCode", string.Empty, string.Empty);
            UiFactory.SetCenteredRect(_joinCodeField.GetComponent<RectTransform>(), new Vector2(0f, 50f), new Vector2(520f, 64f));
            if (_joinCodeField.placeholder is Text joinCodeHint)
                LocalizedText.Bind(joinCodeHint, "ui.play.room_code");

            var joinButton = _ui.CreateButton(panel, "JoinCodeBtn", string.Empty, UiPalette.Secondary, UiPalette.SecondaryHighlight, () =>
            {
                _browserStatus.text = _text.GetText("ui.play.connecting");
                _browserPresenter.JoinByCode(_joinCodeField.text);
            });
            UiFactory.SetCenteredRect(joinButton.GetComponent<RectTransform>(), new Vector2(0f, -30f), new Vector2(520f, 64f));
            LocalizedText.Bind(joinButton.GetComponentInChildren<Text>(), "ui.play.join_code");

            _roomListRoot = UiFactory.CreateRect("RoomList", panel).transform;
            UiFactory.SetCenteredRect((RectTransform)_roomListRoot, new Vector2(0f, -180f), new Vector2(560f, 220f));

            _browserStatus = _ui.CreateText("Status", panel, string.Empty, 22, FontStyle.Normal, UiPalette.MutedText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_browserStatus.rectTransform, new Vector2(0f, -320f), new Vector2(560f, 40f));

            var backButton = _ui.CreateButton(panel, "Back", string.Empty, UiPalette.Quit, UiPalette.QuitHighlight, () =>
            {
                _browserPresenter.StopSearching();
                ReturnToMenu();
            });
            UiFactory.SetCenteredRect(backButton.GetComponent<RectTransform>(), new Vector2(0f, -380f), new Vector2(320f, 60f));
            LocalizedText.Bind(backButton.GetComponentInChildren<Text>(), "ui.play.back");
        }

        private void RefreshRoomList(IReadOnlyList<RoomInfo> rooms)
        {
            for (int i = _roomListRoot.childCount - 1; i >= 0; i--)
                Destroy(_roomListRoot.GetChild(i).gameObject);

            if (rooms == null || rooms.Count == 0)
            {
                _browserStatus.text = _text.GetText(_browserPresenter.NoRoomsFound ? "ui.play.no_rooms" : "ui.play.searching");
                return;
            }

            _browserStatus.text = string.Empty;
            float y = 80f;
            foreach (RoomInfo room in rooms)
            {
                RoomInfo captured = room;
                string label = $"{room.Advertisement.RoomName}  [{room.Advertisement.RoomCode}]  {room.Advertisement.CurrentPlayers}/{room.Advertisement.MaxPlayers}";
                var button = _ui.CreateButton(_roomListRoot, "Room", label, UiPalette.Secondary, UiPalette.SecondaryHighlight, () =>
                {
                    _browserStatus.text = _text.GetText("ui.play.connecting");
                    _browserPresenter.JoinRoom(captured);
                }, fontSize: 22);
                UiFactory.SetCenteredRect(button.GetComponent<RectTransform>(), new Vector2(0f, y), new Vector2(540f, 52f));
                y -= 58f;
            }
        }

        #endregion

        #region Lobby

        private void BuildLobby()
        {
            _lobbyRoot = Panel("Lobby", "ui.play.lobby_title");
            var panel = _lobbyRoot.transform.Find("Panel");

            _lobbyCode = _ui.CreateText("Code", panel, string.Empty, 42, FontStyle.Bold, UiPalette.Primary, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_lobbyCode.rectTransform, new Vector2(0f, 250f), new Vector2(640f, 60f));

            var membersTitle = _ui.CreateText("MembersTitle", panel, string.Empty, 24, FontStyle.Bold, UiPalette.MutedText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(membersTitle.rectTransform, new Vector2(0f, 185f), new Vector2(560f, 40f));
            LocalizedText.Bind(membersTitle, "ui.play.members");

            _lobbyMembersRoot = UiFactory.CreateRect("Members", panel).transform;
            UiFactory.SetCenteredRect((RectTransform)_lobbyMembersRoot, new Vector2(0f, 20f), new Vector2(560f, 280f));

            _lobbyStatus = _ui.CreateText("Status", panel, string.Empty, 22, FontStyle.Normal, UiPalette.MutedText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_lobbyStatus.rectTransform, new Vector2(0f, -200f), new Vector2(560f, 40f));

            var startButton = _ui.CreateButton(panel, "StartMatch", string.Empty, UiPalette.Primary, UiPalette.PrimaryHighlight, () =>
            {
                _lobbyPresenter.StartMatch();
            });
            UiFactory.SetCenteredRect(startButton.GetComponent<RectTransform>(), new Vector2(0f, -270f), new Vector2(420f, 70f));
            LocalizedText.Bind(startButton.GetComponentInChildren<Text>(), "ui.play.start_match");

            var leaveButton = _ui.CreateButton(panel, "Leave", string.Empty, UiPalette.Quit, UiPalette.QuitHighlight, () =>
            {
                _lobbyPresenter.LeaveRoom();
                ReturnToMenu();
            });
            UiFactory.SetCenteredRect(leaveButton.GetComponent<RectTransform>(), new Vector2(0f, -360f), new Vector2(320f, 60f));
            LocalizedText.Bind(leaveButton.GetComponentInChildren<Text>(), "ui.play.leave");
        }

        private void RefreshLobby()
        {
            if (_lobbyRoot == null || !_lobbyRoot.activeSelf) return;
            _lobbyCode.text = $"{_text.GetText("ui.play.room_code")}: {_lobbyPresenter.RoomCode}";

            for (int i = _lobbyMembersRoot.childCount - 1; i >= 0; i--)
                Destroy(_lobbyMembersRoot.GetChild(i).gameObject);

            float y = 110f;
            foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
            {
                var card = UiFactory.CreateImage("MemberCard", _lobbyMembersRoot, UiPalette.Card);
                UiFactory.SetCenteredRect(card.rectTransform, new Vector2(0f, y), new Vector2(540f, 56f));

                bool isYou = player.isLocalPlayer;
                bool isHost = player.IsHost;
                string badges = string.Empty;
                if (isHost)
                    badges += $"   <color=#FA9E29>[{_text.GetText("ui.play.host_badge")}]</color>";
                if (isYou)
                    badges += $"   <color=#69C4EA>({_text.GetText("ui.play.you_badge")})</color>";

                var label = _ui.CreateText("Name", card.transform, player.DisplayName + badges, 26, FontStyle.Bold, UiPalette.LightText, TextAnchor.MiddleLeft);
                UiFactory.SetStretchRect(label.rectTransform, 24f, 0f, 24f, 0f);

                y -= 64f;
            }

            _lobbyStatus.text = _lobbyPresenter.CanStartMatch
                ? string.Empty
                : _text.GetText("ui.play.waiting_host");
            var start = _lobbyRoot.transform.Find("Panel/StartMatch")?.GetComponent<Button>();
            if (start != null)
            {
                start.interactable = _lobbyPresenter.CanStartMatch;
                start.gameObject.SetActive(_lobbyPresenter.CanStartMatch || !NetworkClient.active || NetworkServer.active);
            }
        }

        #endregion

        #region Match

        private void BuildMatch()
        {
            _matchRoot = new GameObject("Match", typeof(RectTransform));
            _matchRoot.transform.SetParent(_canvas.transform, false);
            UiFactory.Stretch(_matchRoot.GetComponent<RectTransform>());

            var background = UiFactory.CreateImage("Background", _matchRoot.transform, UiPalette.Background);
            UiFactory.Stretch(background.rectTransform);

            _matchStatus = _ui.CreateText("Status", _matchRoot.transform, string.Empty, 24, FontStyle.Bold, UiPalette.LightText, TextAnchor.MiddleCenter);
            UiFactory.SetAnchoredRect(_matchStatus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 40f), new Vector2(0f, -36f));

            _matchTimer = _ui.CreateText("Timer", _matchRoot.transform, string.Empty, 34, FontStyle.Bold, UiPalette.Primary, TextAnchor.MiddleRight);
            UiFactory.SetAnchoredRect(_matchTimer.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(180f, 44f), new Vector2(-40f, -34f));

            _bagLabel = _ui.CreateText("Bag", _matchRoot.transform, string.Empty, 22, FontStyle.Bold, UiPalette.MutedText, TextAnchor.MiddleRight);
            UiFactory.SetAnchoredRect(_bagLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(180f, 34f), new Vector2(-40f, -78f));

            var menuButton = _ui.CreateButton(_matchRoot.transform, "Menu", string.Empty, UiPalette.Secondary, UiPalette.SecondaryHighlight, OpenPause, 20);
            UiFactory.SetAnchoredRect(menuButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(130f, 46f), new Vector2(24f, -24f));
            LocalizedText.Bind(menuButton.GetComponentInChildren<Text>(), "ui.match.menu");

            _scoreboard = _ui.CreateText("Scores", _matchRoot.transform, string.Empty, 22, FontStyle.Normal, UiPalette.MutedText, TextAnchor.UpperLeft);
            _scoreboard.horizontalOverflow = HorizontalWrapMode.Wrap;
            _scoreboard.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.SetAnchoredRect(_scoreboard.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(300f, 220f), new Vector2(24f, -96f));

            var legend = _ui.CreateText("Legend", _matchRoot.transform, string.Empty, 18, FontStyle.Normal, UiPalette.MutedText, TextAnchor.LowerLeft);
            UiFactory.SetAnchoredRect(legend.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(420f, 60f), new Vector2(24f, 24f));
            legend.horizontalOverflow = HorizontalWrapMode.Wrap;
            LocalizedText.Bind(legend, "ui.match.premium_legend");

            var boardRoot = UiFactory.CreateRect("Board", _matchRoot.transform);
            UiFactory.SetCenteredRect(boardRoot, new Vector2(0f, 40f), new Vector2(720f, 720f));
            _boardView = new MatchBoardView(_ui, boardRoot, OnCellClicked);

            _rackRoot = UiFactory.CreateRect("Rack", _matchRoot.transform).transform;
            UiFactory.SetAnchoredRect((RectTransform)_rackRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(900f, 90f), new Vector2(0f, 120f));

            _matchPreview = _ui.CreateText("Preview", _matchRoot.transform, string.Empty, 18, FontStyle.Normal, UiPalette.MutedText, TextAnchor.UpperLeft);
            _matchPreview.horizontalOverflow = HorizontalWrapMode.Wrap;
            _matchPreview.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.SetAnchoredRect(_matchPreview.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(300f, 420f), new Vector2(-24f, 40f));

            _declareRoot = UiFactory.CreateRect("Declare", _matchRoot.transform).transform;
            UiFactory.SetAnchoredRect((RectTransform)_declareRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1000f, 110f), new Vector2(0f, 300f));
            _declareRoot.gameObject.SetActive(false);

            float bx = -360f;
            _confirmButton = CreateMatchAction("Confirm", "ui.match.confirm", UiPalette.Primary, () => _matchPresenter.ConfirmPlace(), ref bx);
            _clearButton = CreateMatchAction("Clear", "ui.match.clear", UiPalette.Secondary, () => _matchPresenter.ClearDraft(), ref bx);
            _passButton = CreateMatchAction("Pass", "ui.match.pass", UiPalette.Secondary, () => _matchPresenter.Pass(), ref bx);
            _exchangeButton = CreateMatchAction("Exchange", "ui.match.exchange", UiPalette.Secondary, ToggleExchangeMode, ref bx);
            CreateMatchAction("Leave", "ui.play.leave", UiPalette.Quit, () =>
            {
                _matchPresenter.LeaveRoom();
                ReturnToMenu();
            }, ref bx);
            CreateMatchAction("AskAi", "ui.match.ask_ai", UiPalette.Secondary, OpenAiChat, ref bx);

            _matchRoot.SetActive(false);
        }

        private Button CreateMatchAction(string name, string labelKey, Color color, UnityEngine.Events.UnityAction action, ref float x)
        {
            var button = _ui.CreateButton(_matchRoot.transform, name, string.Empty, color, Color.Lerp(color, Color.white, 0.25f), () => action(), 20);
            UiFactory.SetAnchoredRect(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(160f, 54f), new Vector2(x, 40f));
            LocalizedText.Bind(button.GetComponentInChildren<Text>(), labelKey);
            x += 175f;
            return button;
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

        private static void OpenAiChat()
        {
            if (NetworkContext.Services != null
                && NetworkContext.Services.TryResolve(out IAiEntryPoint assistant))
            {
                assistant.OpenChat();
            }
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

            string statusText = myTurn
                ? _text.GetText("ui.match.your_turn")
                : _text.GetText("ui.match.wait_turn");
            _matchStatus.text = $"{_text.GetText("ui.match.turn")} {_matchPresenter.TurnNumber}  •  {statusText}";
            if (!string.IsNullOrEmpty(_matchPresenter.LastError))
                _matchStatus.text = PlacementPreviewFormatter.LocalizeError(_matchPresenter.LastError);

            UpdateTimerAndBag();

            // Action buttons only work on your turn; graying them out makes the
            // turn state readable at a glance.
            _confirmButton.interactable = myTurn && !_exchangeMode;
            _clearButton.interactable = myTurn && !_exchangeMode;
            _passButton.interactable = myTurn && !_exchangeMode;
            _exchangeButton.interactable = myTurn;

            var scores = new System.Text.StringBuilder();
            if (_matchPresenter.Players != null)
            {
                foreach (PlayerState player in _matchPresenter.Players.Players)
                {
                    string mark = player.PlayerId == _matchPresenter.LocalPlayerId ? $" ({_text.GetText("ui.match.you")})" : string.Empty;
                    string ai = player.IsAi ? " [AI]" : string.Empty;
                    string line = $"{player.DisplayName}{ai}{mark}: {player.Score}";
                    if (player.PlayerId == _matchPresenter.CurrentPlayerId)
                        line = $"<color=#FDA733><b>▶ {line}</b></color>";
                    scores.AppendLine(line);
                }
            }

            _scoreboard.text = scores.ToString();

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
                SetRackButtonColor(button, selected ? UiPalette.Primary : UiPalette.Secondary);
                _rackLabels[i].text = $"{SymbolOf(tileId)}\n{PointsOf(tileId)}";
                UiFactory.SetCenteredRect(
                    button.GetComponent<RectTransform>(),
                    new Vector2(start + i * RackPitch, 0f),
                    new Vector2(64f, 72f));
            }
        }

        private void EnsureRackButtons(int required)
        {
            while (_rackButtons.Count < required)
            {
                int index = _rackButtons.Count;
                var button = _ui.CreateButton(
                    _rackRoot, $"R{index}", string.Empty,
                    UiPalette.Secondary, UiPalette.PrimaryHighlight,
                    () => OnRackClicked(index), 20);

                Text label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    label.verticalOverflow = VerticalWrapMode.Overflow;
                    label.alignment = TextAnchor.MiddleCenter;
                }

                _rackButtons.Add(button);
                _rackLabels.Add(label);
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

            var card = UiFactory.CreateImage("Card", _pauseRoot.transform, UiPalette.Card);
            UiFactory.SetCenteredRect(card.rectTransform, Vector2.zero, new Vector2(520f, 480f));
            UiFactory.AddShadow(card.gameObject, new Color(0f, 0f, 0f, 0.38f), new Vector2(0f, -12f));

            var title = _ui.CreateText("Title", card.transform, string.Empty, 44, FontStyle.Bold, UiPalette.LightText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(title.rectTransform, new Vector2(0f, 160f), new Vector2(460f, 64f));
            LocalizedText.Bind(title, "ui.pause.title");

            var resumeButton = _ui.CreateButton(card.transform, "Resume", string.Empty, UiPalette.Primary, UiPalette.PrimaryHighlight, ClosePause);
            UiFactory.SetCenteredRect(resumeButton.GetComponent<RectTransform>(), new Vector2(0f, 60f), new Vector2(400f, 72f));
            LocalizedText.Bind(resumeButton.GetComponentInChildren<Text>(), "ui.pause.resume");

            var settingsButton = _ui.CreateButton(card.transform, "Settings", string.Empty, UiPalette.Secondary, UiPalette.SecondaryHighlight, OpenMatchSettings);
            UiFactory.SetCenteredRect(settingsButton.GetComponent<RectTransform>(), new Vector2(0f, -32f), new Vector2(400f, 72f));
            LocalizedText.Bind(settingsButton.GetComponentInChildren<Text>(), "ui.pause.settings");

            var leaveButton = _ui.CreateButton(card.transform, "Leave", string.Empty, UiPalette.Quit, UiPalette.QuitHighlight, () =>
            {
                _pauseRoot.SetActive(false);
                _matchPresenter.LeaveRoom();
                ReturnToMenu();
            });
            UiFactory.SetCenteredRect(leaveButton.GetComponent<RectTransform>(), new Vector2(0f, -124f), new Vector2(400f, 72f));
            LocalizedText.Bind(leaveButton.GetComponentInChildren<Text>(), "ui.pause.leave");

            UiFactory.SetVerticalNavigation(resumeButton, leaveButton, settingsButton);
            UiFactory.SetVerticalNavigation(settingsButton, resumeButton, leaveButton);
            UiFactory.SetVerticalNavigation(leaveButton, settingsButton, resumeButton);

            _pauseRoot.SetActive(false);
        }

        private void OpenPause()
        {
            if (_screen != ScreenId.Match) return;
            _pauseRoot.SetActive(true);
            var resume = _pauseRoot.transform.Find("Card/Resume")?.GetComponent<Button>();
            UiFactory.Select(resume);
        }

        private void ClosePause()
        {
            _pauseRoot.SetActive(false);
        }

        private void OpenMatchSettings()
        {
            _pauseRoot.SetActive(false);
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
            _resultRoot = Panel("Result", "ui.result.title");
            var panel = _resultRoot.transform.Find("Panel");

            _resultWinner = _ui.CreateText("Winner", panel, string.Empty, 40, FontStyle.Bold, UiPalette.Primary, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_resultWinner.rectTransform, new Vector2(0f, 250f), new Vector2(640f, 60f));

            _resultBody = _ui.CreateText("Body", panel, string.Empty, 28, FontStyle.Normal, UiPalette.LightText, TextAnchor.UpperCenter);
            _resultBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _resultBody.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.SetCenteredRect(_resultBody.rectTransform, new Vector2(0f, 40f), new Vector2(560f, 340f));

            var rematchButton = _ui.CreateButton(panel, "Rematch", string.Empty, UiPalette.Primary, UiPalette.PrimaryHighlight, () =>
            {
                if (_resultPresenter.CanRematch)
                    _resultPresenter.Rematch();
            });
            UiFactory.SetCenteredRect(rematchButton.GetComponent<RectTransform>(), new Vector2(0f, -250f), new Vector2(400f, 70f));
            LocalizedText.Bind(rematchButton.GetComponentInChildren<Text>(), "ui.result.rematch");

            var leaveButton = _ui.CreateButton(panel, "Leave", string.Empty, UiPalette.Quit, UiPalette.QuitHighlight, () =>
            {
                _resultPresenter.Leave();
                ReturnToMenu();
            });
            UiFactory.SetCenteredRect(leaveButton.GetComponent<RectTransform>(), new Vector2(0f, -340f), new Vector2(320f, 60f));
            LocalizedText.Bind(leaveButton.GetComponentInChildren<Text>(), "ui.result.leave");
        }

        private void RefreshResult()
        {
            MatchResult result = _resultPresenter.Result;
            if (result == null)
            {
                _resultWinner.text = string.Empty;
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

            string winnerName = winner != null ? winner.DisplayName : $"#{result.WinnerPlayerId}";
            _resultWinner.text = $"{_text.GetText("ui.result.winner")}: {winnerName}";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(_text.GetText("ui.result.standings"));
            sb.AppendLine();
            int rank = 1;
            foreach (PlayerResult row in result.Standings.OrderByDescending(r => r.FinalScore))
            {
                string line = $"{rank}.  {row.DisplayName}   {row.FinalScore}";
                if (row.PlayerId == result.WinnerPlayerId)
                    line = $"<color=#FDA733><b>{line}</b></color>";
                sb.AppendLine(line);
                rank++;
            }

            _resultBody.text = sb.ToString();

            var rematch = _resultRoot.transform.Find("Panel/Rematch")?.GetComponent<Button>();
            if (rematch != null) rematch.interactable = _resultPresenter.CanRematch;
        }

        private void BuildRecovery()
        {
            _recoveryRoot = new GameObject("Recovery", typeof(RectTransform));
            _recoveryRoot.transform.SetParent(_canvas.transform, false);
            UiFactory.Stretch(_recoveryRoot.GetComponent<RectTransform>());
            var overlay = UiFactory.CreateImage("Overlay", _recoveryRoot.transform, UiPalette.Overlay);
            UiFactory.Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;

            var card = UiFactory.CreateImage("Card", _recoveryRoot.transform, UiPalette.Card);
            UiFactory.SetCenteredRect(card.rectTransform, Vector2.zero, new Vector2(640f, 420f));

            var title = _ui.CreateText("Title", card.transform, string.Empty, 40, FontStyle.Bold, UiPalette.LightText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(title.rectTransform, new Vector2(0f, 130f), new Vector2(560f, 60f));
            LocalizedText.Bind(title, "ui.recovery.title");

            _recoveryStatus = _ui.CreateText("Status", card.transform, string.Empty, 24, FontStyle.Normal, UiPalette.MutedText, TextAnchor.MiddleCenter);
            _recoveryStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.SetCenteredRect(_recoveryStatus.rectTransform, new Vector2(0f, 20f), new Vector2(560f, 120f));

            var waitButton = _ui.CreateButton(card.transform, "Wait", string.Empty, UiPalette.Secondary, UiPalette.SecondaryHighlight, () => { });
            UiFactory.SetCenteredRect(waitButton.GetComponent<RectTransform>(), new Vector2(0f, -90f), new Vector2(420f, 64f));
            LocalizedText.Bind(waitButton.GetComponentInChildren<Text>(), "ui.recovery.wait");

            var endButton = _ui.CreateButton(card.transform, "End", string.Empty, UiPalette.Quit, UiPalette.QuitHighlight, () =>
            {
                _recoveryPresenter.EndMatchNow();
                ShowResult();
            });
            UiFactory.SetCenteredRect(endButton.GetComponent<RectTransform>(), new Vector2(0f, -170f), new Vector2(420f, 64f));
            LocalizedText.Bind(endButton.GetComponentInChildren<Text>(), "ui.recovery.end");

            _recoveryRoot.SetActive(false);
        }

        private void OnRecoveryStatus(RecoveryPhase phase, string _, float elapsed)
        {
            _recoveryRoot.SetActive(phase != RecoveryPhase.Idle && phase != RecoveryPhase.Recovered);
            string key = phase switch
            {
                RecoveryPhase.GraceWait => "ui.recovery.grace",
                RecoveryPhase.Searching => "ui.recovery.search",
                RecoveryPhase.Promoting => "ui.recovery.promote",
                RecoveryPhase.Reconnecting => "ui.recovery.reconnect",
                RecoveryPhase.Recovered => "ui.recovery.recovered",
                _ => "ui.recovery.grace"
            };
            _recoveryStatus.text = $"{_text.GetText(key)}\n({elapsed:0}s)";
        }

        #endregion

        private GameObject Panel(string name, string titleKey)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(_canvas.transform, false);
            UiFactory.Stretch(root.GetComponent<RectTransform>());

            var background = UiFactory.CreateImage("Background", root.transform, UiPalette.Background);
            UiFactory.Stretch(background.rectTransform);

            var panel = UiFactory.CreateImage("Panel", root.transform, UiPalette.Panel);
            UiFactory.SetCenteredRect(panel.rectTransform, Vector2.zero, new Vector2(720f, 920f));

            var heading = _ui.CreateText("Title", panel.transform, string.Empty, 48, FontStyle.Bold, UiPalette.LightText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(heading.rectTransform, new Vector2(0f, 380f), new Vector2(640f, 70f));
            LocalizedText.Bind(heading, titleKey);

            root.SetActive(false);
            return root;
        }
    }
}
