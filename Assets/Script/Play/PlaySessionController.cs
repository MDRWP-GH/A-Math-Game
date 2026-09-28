using System;
using System.Collections.Generic;
using System.Linq;
using AMath.Art;
using AMath.AI.UI;
using AMath.Bootstrap;
using AMath.Core;
using AMath.Core.Assistance;
using AMath.Core.Events;
using AMath.Core.Identity;
using AMath.Core.StateMachines;
using AMath.Gameplay.Board;
using AMath.Gameplay.Interaction;
using AMath.Gameplay.Players;
using AMath.Managers;
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
using UnityEngine.EventSystems;
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
        private const float CommandDrawerAnimationSeconds = 0.30f;

        /// <summary>Horizontal distance between rack tiles.</summary>
        private const float RackPitch = MatchHudLayout.RackPitch;

        private const int VisibleSeatCount = MatchHudLayout.VisibleSeatCount;

        /// <summary>Diameter of a colour swatch in the lobby picker.</summary>
        private const float ColorSwatchSize = 38f;

        /// <summary>Outer ring around a swatch; doubles as the "selected" marker.</summary>
        private const float ColorRingSize = 50f;

        /// <summary>Width the 13 swatches are spread across.</summary>
        private const float ColorRowWidth = 650f;

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
        private static readonly Color ColorRingHover = new(1f, 1f, 1f, 0.95f);


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
        private ColorSwatchRingFeedback[] _colorRingFeedback;
        private GameObject _teamPickerRoot;
        private Button _team1Button;
        private Button _team2Button;

        private Text _browserStatus;
        private Text _hostStatus;
        private Text _hostPlayerNameError;
        private Text _browserPlayerNameError;
        private Text _lobbyStatus;
        private Text _lobbyCode;
        private Transform _lobbyMembersRoot;
        private Button _formatIndividualButton;
        private Button _formatTeamButton;
        private Button _turnRushButton;
        private Button _turnShortButton;
        private Button _turnNormalButton;
        private Button _turnLongButton;
        private Button _lobbyStartButton;
        private Button _lobbyLeaveButton;
        private Transform _roomListRoot;
        private InputField _roomNameField;
        private InputField _hostPlayerNameField;
        private InputField _browserPlayerNameField;
        private InputField _joinCodeField;
        private Button _hostCreateButton;
        private Button _browserJoinCodeButton;
        private readonly List<Button> _browserRoomButtons = new();
        private bool _connectionAttemptPending;

        private Text _matchStatus;
        private Image _matchTimerPanel;
        private Text _matchTimer;
        private Image _matchTimerFill;
        private Text _bagLabel;
        private Text _matchPreview;
        private Transform _rackRoot;
        private MatchCommandDrawerAnimator _commandDrawer;
        private Button _commandDrawerToggle;
        private Text _commandDrawerToggleLabel;
        private Button _confirmButton;
        private Button _clearButton;
        private Button _passButton;
        private Button _exchangeButton;
        private MatchBoardView _boardView;
        private TilePreviewPanel _tilePreviewPanel;
        private TilePlacementInput _tilePlacementInput;
        private readonly List<int> _exchangeSelection = new();
        private readonly List<Button> _rackButtons = new(GameRules.RackSize);
        private readonly List<Text> _rackLabels = new(GameRules.RackSize);
        private readonly List<Image> _rackImages = new(GameRules.RackSize);
        private readonly MatchHudElements.Seat[] _seats = new MatchHudElements.Seat[VisibleSeatCount];
        private bool _exchangeMode;
        private Transform _declareRoot;

        private Button _pauseResumeButton;
        private Button _pausePlayOnButton;
        private Button _pauseSettingsButton;
        private Button _pauseLeaveButton;
        private Button _resultRematchButton;
        private Button _resultLeaveButton;
        private readonly List<Button> _visiblePauseButtons = new(4);

        private Text _resultWinner;
        private Text _resultWinnerCaption;
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
        private Action<RoomOperationFailedEvent> _onRoomOperationFailed;
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
            PlacementPreviewFormatter.ThaiSelector = () => GameSettings.LanguageIndex == 1;

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
                SetConnectionPending(false);
                string localized = _text.GetText(msg);
                if (_screen == ScreenId.HostSetup && _hostStatus != null)
                    _hostStatus.text = localized;
                else
                    SetBrowserNotice(localized);
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
                if (_screen == ScreenId.Browser)
                {
                    _browserPresenter?.StartSearching();
                    UpdateBrowserStatus(_text.GetText(GetBrowserStatusLocalizationKey()));
                    return;
                }

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

                // The host explains failures the client cannot deduce. Known
                // reasons are localization keys; legacy prose remains readable
                // because the provider returns unknown keys unchanged.
                _onConnectionRejected = evt =>
                {
                    SetConnectionPending(false);
                    SetBrowserNotice(string.IsNullOrEmpty(evt.Reason)
                        ? _text.GetText("ui.play.err_join")
                        : _text.GetText(evt.Reason));
                    ShowBrowser();
                };
                _onRoomOperationFailed = evt =>
                {
                    SetConnectionPending(false);
                    string message = _text.GetText(RoomOperationErrorText.LocalizationKey(evt.Error));
                    if (_screen == ScreenId.HostSetup && _hostStatus != null)
                        _hostStatus.text = message;
                    else
                    {
                        SetBrowserNotice(message);
                        if (_screen == ScreenId.Browser)
                            UpdateBrowserStatus(message);
                        else
                            ShowBrowser();
                    }
                };

                _eventBus.Subscribe(_onHostStarted);
                _eventBus.Subscribe(_onClientConnected);
                _eventBus.Subscribe(_onMatchStarted);
                _eventBus.Subscribe(_onMatchRestored);
                _eventBus.Subscribe(_onHostStopped);
                _eventBus.Subscribe(_onConnectionRejected);
                _eventBus.Subscribe(_onRoomOperationFailed);
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
                _eventBus.Unsubscribe(_onRoomOperationFailed);
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

            if (_screen == ScreenId.Browser
                && _browserRoot != null
                && _browserRoot.activeSelf
                && _browserPresenter != null
                && _browserStatus != null)
            {
                UpdateBrowserStatus(_text.GetText(GetBrowserStatusLocalizationKey()));
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
            if (_matchTimer == null || _matchPresenter == null) return;

            float remaining = _matchPresenter.RemainingTurnSeconds;
            float progress = NormalizeTurnClock(remaining, _matchPresenter.TurnDurationSeconds);
            bool warning = _matchPresenter.Phase == MatchPhase.Playing
                && remaining <= TurnWarningSeconds
                && remaining > 0f;

            _matchTimer.text = FormatTurnClock(remaining);
            _matchTimer.color = warning ? UiPalette.TimerWarning : UiPalette.Primary;

            if (_matchTimerFill != null)
            {
                RectTransform fillRect = _matchTimerFill.rectTransform;
                fillRect.anchorMax = new Vector2(progress, 1f);
                _matchTimerFill.color = warning ? UiPalette.TimerWarning : UiPalette.Primary;
            }

            if (_matchTimerPanel != null)
            {
                float pulse = warning
                    ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)
                    : 0f;
                _matchTimerPanel.rectTransform.localScale = Vector3.one * (1f + pulse * 0.025f);
                _matchTimerPanel.color = warning
                    ? Color.Lerp(
                        UiPalette.GlassStrong,
                        new Color(UiPalette.TimerWarning.r, UiPalette.TimerWarning.g, UiPalette.TimerWarning.b, 0.72f),
                        0.22f + pulse * 0.18f)
                    : UiPalette.GlassStrong;
            }

            if (_bagLabel != null)
                _bagLabel.text = _matchPresenter.BagCount.ToString();
        }

        private static string FormatTurnClock(float remainingSeconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"{minutes:00}:{seconds:00}";
        }

        private static float NormalizeTurnClock(float remainingSeconds, float durationSeconds)
        {
            if (durationSeconds <= 0f)
                return 0f;

            return Mathf.Clamp01(remainingSeconds / durationSeconds);
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
            _tilePreviewPanel?.RefreshLanguage();
            _hostPlayerNameField?.SetTextWithoutNotify(GameSettings.PlayerName);
            _browserPlayerNameField?.SetTextWithoutNotify(GameSettings.PlayerName);

            // Language may have changed: re-render every dynamic string on the
            // active screen (static labels update through LocalizedText).
            switch (_screen)
            {
                case ScreenId.Browser:
                    if (_browserRoot.activeSelf && _browserPresenter != null)
                        UpdateBrowserStatus(_text.GetText(GetBrowserStatusLocalizationKey()));
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
            SetActiveScreens(start: true, focus: _startHostButton);
            if (_startEntrance != null)
                _startEntrance.Play();
        }

        private void ShowHostSetup()
        {
            _screen = ScreenId.HostSetup;
            HideRecovery();
            _browserPresenter?.StopSearching();
            SetConnectionPending(false);
            if (_hostStatus != null)
                _hostStatus.text = string.Empty;
            _hostPlayerNameField?.SetTextWithoutNotify(GameSettings.PlayerName);
            SetActiveScreens(host: true, focus: _hostPlayerNameField);
            _hostRoot.GetComponent<MenuEntranceAnimator>()?.Play();
        }

        private void ShowBrowser()
        {
            _screen = ScreenId.Browser;
            HideRecovery();
            SetConnectionPending(false);
            _browserPlayerNameField?.SetTextWithoutNotify(GameSettings.PlayerName);
            SetActiveScreens(browser: true, focus: _browserPlayerNameField);
            _browserRoot.GetComponent<MenuEntranceAnimator>()?.Play();
            _browserPresenter.StartSearching();
            UpdateBrowserStatus(_text.GetText(GetBrowserStatusLocalizationKey()));
        }

        private string GetBrowserStatusLocalizationKey()
        {
            if (_browserPresenter == null)
                return "ui.play.searching";

            if (_browserPresenter.DiscoveryFailed)
                return "ui.play.discovery_failed";

            return _browserPresenter.NoRoomsFound ? "ui.play.no_rooms" : "ui.play.searching";
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
            if (_screen == ScreenId.HostSetup && _hostStatus != null)
                _hostStatus.text = _text.GetText("ui.play.connecting");
            else if (_browserStatus != null)
                _browserStatus.text = _text.GetText("ui.play.connecting");
        }

        private bool BeginConnectionAttempt()
        {
            if (_connectionAttemptPending)
                return false;

            SetConnectionPending(true);
            ShowConnectingStatus();
            return true;
        }

        private bool TryCommitPlayerName(InputField field, bool hostScreen)
        {
            string raw = field != null ? field.text : string.Empty;
            if (!GameSettings.TrySetPlayerName(raw, out PlayerNameValidationError error))
            {
                string message = _text.GetText(PlayerNameValidationUi.LocalizationKey(error));
                Text inlineError = hostScreen ? _hostPlayerNameError : _browserPlayerNameError;
                if (inlineError != null)
                    inlineError.text = message;
                if (hostScreen && _hostStatus != null)
                    _hostStatus.text = message;
                else
                {
                    SetBrowserNotice(message);
                    UpdateBrowserStatus(message);
                }

                if (field != null)
                {
                    field.Select();
                    UiFactory.Select(field);
                }
                return false;
            }

            string normalized = GameSettings.PlayerName;
            if (_hostPlayerNameError != null)
                _hostPlayerNameError.text = string.Empty;
            if (_browserPlayerNameError != null)
                _browserPlayerNameError.text = string.Empty;
            if (hostScreen && _hostStatus != null)
                _hostStatus.text = string.Empty;
            field?.SetTextWithoutNotify(normalized);
            _hostPlayerNameField?.SetTextWithoutNotify(normalized);
            _browserPlayerNameField?.SetTextWithoutNotify(normalized);
            LocalIdentity.DisplayName = normalized;
            return true;
        }

        private void SetConnectionPending(bool pending)
        {
            _connectionAttemptPending = pending;

            if (_hostCreateButton != null)
                _hostCreateButton.interactable = !pending;
            if (_browserJoinCodeButton != null)
                _browserJoinCodeButton.interactable = !pending;

            for (int i = 0; i < _browserRoomButtons.Count; i++)
            {
                if (_browserRoomButtons[i] != null)
                    _browserRoomButtons[i].interactable = !pending;
            }
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
            SetConnectionPending(false);
            _browserPresenter.StopSearching();
            SetActiveScreens(lobby: true);
            _lobbyRoot.GetComponent<MenuEntranceAnimator>()?.Play();
            _lobbyPresenter?.RefreshMembers();
            RefreshLobby();
            UiFactory.SelectFirstInteractable(
                _lobbyStartButton,
                _formatIndividualButton,
                _formatTeamButton,
                _team1Button,
                _team2Button,
                _lobbyLeaveButton);
        }

        private void ShowMatch()
        {
            _screen = ScreenId.Match;
            SetActiveScreens(match: true);
            _matchPresenter?.ClearDraft();
            _tilePreviewPanel?.Hide();
            _commandDrawer?.SetExpandedInstant(false);
            UpdateCommandDrawerToggleLabel();
            _eventBus?.Publish(new BoardLoadedEvent());
            RefreshMatch();
            UiFactory.SelectFirstInteractable(_commandDrawerToggle);
        }

        private void ShowResult()
        {
            _screen = ScreenId.Result;
            _tilePreviewPanel?.Hide();
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
            _resultRoot.GetComponent<MenuEntranceAnimator>()?.Play();
            OverlayFade.Ensure(_pauseRoot)?.HideInstant();
            RefreshResult();
            UiFactory.SelectFirstInteractable(_resultRematchButton, _resultLeaveButton);
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
            bool result = false,
            Selectable focus = null)
        {
            if (!match)
                _tilePreviewPanel?.Hide();
            if (_startRoot != null) _startRoot.SetActive(start);
            if (_hostRoot != null) _hostRoot.SetActive(host);
            _browserRoot.SetActive(browser);
            _lobbyRoot.SetActive(lobby);
            _matchRoot.SetActive(match);
            _resultRoot.SetActive(result);
            UiFactory.SelectFirstInteractable(focus);
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
            var title = _ui.CreateOutlinedTitle(_startRoot.transform, "Title", string.Empty, 68);
            title.font = GameFonts.JainiPurva;
            UiFactory.SetAnchoredRect(
                title.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(980f, 88f), new Vector2(0f, -54f));
            LocalizedText.Bind(title, "ui.menu.title");

            var subtitle = _ui.CreateText(
                "Subtitle", _startRoot.transform, string.Empty, 30, FontStyle.Normal,
                UiPalette.LightText, TextAnchor.MiddleCenter);
            UiFactory.SetAnchoredRect(
                subtitle.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(1100f, 48f), new Vector2(0f, -126f));
            LocalizedText.Bind(subtitle, "ui.play.subtitle");

            var panel = UiFactory.CreateImage("Panel", _startRoot.transform, Color.clear);
            panel.raycastTarget = false;
            UiFactory.SetCenteredRect(panel.rectTransform, new Vector2(0f, -30f), new Vector2(1220f, 650f));

            var hostCard = UiFactory.CreateGlassPanel(panel.transform, "Host Card", UiPalette.GlassStrong);
            UiFactory.SetCenteredRect(hostCard.rectTransform, new Vector2(-290f, 15f), new Vector2(520f, 430f));
            var hostHeading = _ui.CreateText(
                "Heading", hostCard.transform, string.Empty, 42, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(hostHeading.rectTransform, new Vector2(0f, 118f), new Vector2(440f, 58f));
            LocalizedText.Bind(hostHeading, "ui.play.create");
            var hostDescription = _ui.CreateText(
                "Description", hostCard.transform, string.Empty, 27, FontStyle.Normal,
                UiPalette.LightText, TextAnchor.UpperCenter);
            hostDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.SetCenteredRect(hostDescription.rectTransform, new Vector2(0f, 28f), new Vector2(420f, 116f));
            LocalizedText.Bind(hostDescription, "ui.play.host_description");

            _startHostButton = _ui.CreateAccentButton(
                hostCard.transform, "Host", string.Empty,
                UiPalette.Success, UiPalette.SuccessHighlight, ShowHostSetup, 30);
            UiFactory.SetCenteredRect(_startHostButton.GetComponent<RectTransform>(), new Vector2(0f, -132f), new Vector2(360f, 68f));
            LocalizedText.Bind(_startHostButton.GetComponentInChildren<Text>(), "ui.play.host");

            var joinCard = UiFactory.CreateGlassPanel(panel.transform, "Join Card", UiPalette.GlassStrong);
            UiFactory.SetCenteredRect(joinCard.rectTransform, new Vector2(290f, 15f), new Vector2(520f, 430f));
            var joinHeading = _ui.CreateText(
                "Heading", joinCard.transform, string.Empty, 42, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(joinHeading.rectTransform, new Vector2(0f, 118f), new Vector2(440f, 58f));
            LocalizedText.Bind(joinHeading, "ui.play.join");
            var joinDescription = _ui.CreateText(
                "Description", joinCard.transform, string.Empty, 27, FontStyle.Normal,
                UiPalette.LightText, TextAnchor.UpperCenter);
            joinDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.SetCenteredRect(joinDescription.rectTransform, new Vector2(0f, 28f), new Vector2(420f, 116f));
            LocalizedText.Bind(joinDescription, "ui.play.join_description");

            _startJoinButton = _ui.CreateAccentButton(
                joinCard.transform, "Join", string.Empty,
                UiPalette.Primary, UiPalette.PrimaryHighlight, ShowBrowser, 30);
            UiFactory.SetCenteredRect(_startJoinButton.GetComponent<RectTransform>(), new Vector2(0f, -132f), new Vector2(360f, 68f));
            LocalizedText.Bind(_startJoinButton.GetComponentInChildren<Text>(), "ui.play.join");

            _startBackButton = _ui.CreateAccentButton(panel.transform, "Back", string.Empty,
                UiPalette.Danger, UiPalette.DangerHighlight, ReturnToMenu, 34);
            UiFactory.SetCenteredRect(_startBackButton.GetComponent<RectTransform>(), new Vector2(0f, -270f), new Vector2(320f, 58f));
            LocalizedText.Bind(_startBackButton.GetComponentInChildren<Text>(), "ui.play.back");

            SetNavigation(_startHostButton, null, _startBackButton, null, _startJoinButton);
            SetNavigation(_startJoinButton, null, _startBackButton, _startHostButton, null);
            SetNavigation(_startBackButton, _startHostButton, null, _startHostButton, _startJoinButton);

            _startEntrance = _startRoot.AddComponent<MenuEntranceAnimator>();
            _startEntrance.SetTargets(title, subtitle, hostCard, joinCard, _startBackButton);
            _startEntrance.Configure(0.26f, 0.03f, 0f);
            _startRoot.SetActive(false);
        }

        private void BuildHostSetup()
        {
            _hostRoot = ForestScreen("Host Setup");

            var title = _ui.CreateOutlinedTitle(_hostRoot.transform, "Title", string.Empty, 64);
            UiFactory.SetAnchoredRect(
                title.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(900f, 82f), new Vector2(0f, -58f));
            LocalizedText.Bind(title, "ui.play.host_title");

            var subtitle = _ui.CreateText(
                "Subtitle", _hostRoot.transform, string.Empty, 29, FontStyle.Normal,
                UiPalette.LightText, TextAnchor.MiddleCenter);
            UiFactory.SetAnchoredRect(
                subtitle.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(1000f, 44f), new Vector2(0f, -126f));
            LocalizedText.Bind(subtitle, "ui.play.host_setup_subtitle");

            var panel = UiFactory.CreateGlassPanel(_hostRoot.transform, "Panel", UiPalette.GlassStrong);
            UiFactory.SetCenteredRect(panel.rectTransform, new Vector2(0f, -30f), new Vector2(920f, 650f));

            var playerNameLabel = _ui.CreateText(
                "PlayerNameLabel", panel.transform, string.Empty, 29, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft);
            UiFactory.SetCenteredRect(playerNameLabel.rectTransform, new Vector2(0f, 250f), new Vector2(720f, 42f));
            LocalizedText.Bind(playerNameLabel, "ui.play.player_name");

            _hostPlayerNameField = _ui.CreateInputField(panel.transform, "PlayerName", GameSettings.PlayerName,
                _text.GetText("ui.settings.name_placeholder"), 30);
            _hostPlayerNameField.textComponent.font = GameFonts.K2D;
            if (_hostPlayerNameField.placeholder is Text hostNamePlaceholder)
                hostNamePlaceholder.font = GameFonts.K2D;
            _hostPlayerNameField.characterLimit = 64;
            UiFactory.SetCenteredRect(_hostPlayerNameField.GetComponent<RectTransform>(), new Vector2(0f, 202f), new Vector2(720f, 58f));
            _hostPlayerNameField.onEndEdit.AddListener(_ => TryCommitPlayerName(_hostPlayerNameField, true));

            _hostPlayerNameError = _ui.CreateText(
                "PlayerNameError", panel.transform, string.Empty, 21, FontStyle.Normal,
                UiPalette.TimerWarning, TextAnchor.MiddleLeft);
            UiFactory.SetCenteredRect(_hostPlayerNameError.rectTransform, new Vector2(0f, 158f), new Vector2(720f, 28f));

            var nameLabel = _ui.CreateText(
                "RoomLabel", panel.transform, string.Empty, 32, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft);
            UiFactory.SetCenteredRect(nameLabel.rectTransform, new Vector2(0f, 118f), new Vector2(720f, 42f));
            UiFactory.AddDoubleOutline(nameLabel.gameObject, new Vector2(2.5f, -2.5f), new Vector2(1.2f, -1.2f));
            LocalizedText.Bind(nameLabel, "ui.play.room_name");

            // Left empty on purpose: the placeholder shows the name the room
            // gets if the host just presses play, so naming it is optional
            // rather than something they have to clear first.
            _roomNameField = _ui.CreateInputField(panel.transform, "RoomName", string.Empty, RoomSession.DefaultRoomName);
            UiFactory.SetCenteredRect(_roomNameField.GetComponent<RectTransform>(), new Vector2(0f, 68f), new Vector2(720f, 58f));

            var hint = _ui.CreateText(
                "RoomHint", panel.transform, string.Empty, 25, FontStyle.Normal,
                UiPalette.MutedText, TextAnchor.UpperLeft);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.SetCenteredRect(hint.rectTransform, new Vector2(0f, 4f), new Vector2(720f, 48f));
            LocalizedText.Bind(hint, "ui.play.room_name_hint");

            var capacity = _ui.CreateText(
                "Capacity", panel.transform, string.Empty, 26, FontStyle.Normal,
                UiPalette.LightText, TextAnchor.MiddleLeft);
            UiFactory.SetCenteredRect(capacity.rectTransform, new Vector2(0f, -48f), new Vector2(720f, 42f));
            LocalizedText.Bind(capacity, "ui.play.room_capacity");

            _hostStatus = _ui.CreateText(
                "Status", panel.transform, string.Empty, 24, FontStyle.Normal,
                UiPalette.Primary, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_hostStatus.rectTransform, new Vector2(0f, -103f), new Vector2(720f, 48f));

            var back = _ui.CreateAccentButton(
                panel.transform, "Back", string.Empty,
                UiPalette.Danger, UiPalette.DangerHighlight, ShowStartChoice, 28);
            UiFactory.SetCenteredRect(back.GetComponent<RectTransform>(), new Vector2(-190f, -218f), new Vector2(300f, 64f));
            LocalizedText.Bind(back.GetComponentInChildren<Text>(), "ui.play.back");

            _hostCreateButton = _ui.CreateAccentButton(
                panel.transform, "Create Room", string.Empty,
                UiPalette.Success, UiPalette.SuccessHighlight, () =>
                {
                    if (!TryCommitPlayerName(_hostPlayerNameField, true)) return;
                    if (!BeginConnectionAttempt()) return;
                    _browserPresenter.CreateRoom(
                        _roomNameField != null ? _roomNameField.text : string.Empty,
                        GameRules.MaxPlayers);
                }, 28);
            UiFactory.SetCenteredRect(_hostCreateButton.GetComponent<RectTransform>(), new Vector2(190f, -218f), new Vector2(360f, 64f));
            LocalizedText.Bind(_hostCreateButton.GetComponentInChildren<Text>(), "ui.play.create");

            SetNavigation(_hostPlayerNameField, null, _roomNameField, null, null);
            SetNavigation(_roomNameField, _hostPlayerNameField, _hostCreateButton, null, null);
            SetNavigation(back, _roomNameField, null, null, _hostCreateButton);
            SetNavigation(_hostCreateButton, _roomNameField, null, back, null);

            ConfigureScreenEntrance(_hostRoot, title, subtitle, panel);
            _hostRoot.SetActive(false);
        }

        private void BuildBrowser()
        {
            _browserRoot = ForestScreen("Join");

            var title = _ui.CreateOutlinedTitle(_browserRoot.transform, "Title", string.Empty, 64);
            UiFactory.SetAnchoredRect(
                title.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(900f, 82f), new Vector2(0f, -52f));
            LocalizedText.Bind(title, "ui.play.browser_title");

            var subtitle = _ui.CreateText(
                "Subtitle", _browserRoot.transform, string.Empty, 29, FontStyle.Normal,
                UiPalette.LightText, TextAnchor.MiddleCenter);
            UiFactory.SetAnchoredRect(
                subtitle.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(1100f, 44f), new Vector2(0f, -116f));
            LocalizedText.Bind(subtitle, "ui.play.browser_subtitle");

            var panel = UiFactory.CreateImage("Panel", _browserRoot.transform, Color.clear);
            panel.raycastTarget = false;
            UiFactory.SetCenteredRect(panel.rectTransform, new Vector2(0f, -32f), new Vector2(1280f, 760f));

            _browserStatus = _ui.CreateText(
                "Status", panel.transform, string.Empty, 28, FontStyle.Normal,
                Color.white, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_browserStatus.rectTransform, new Vector2(0f, 320f), new Vector2(1120f, 48f));
            UiFactory.AddDoubleOutline(_browserStatus.gameObject, new Vector2(2.5f, -2.5f), new Vector2(1.2f, -1.2f));

            var roomsTitle = _ui.CreateText(
                "RoomsTitle", panel.transform, string.Empty, 27, FontStyle.Bold,
                UiPalette.LightText, TextAnchor.MiddleLeft);
            UiFactory.SetCenteredRect(roomsTitle.rectTransform, new Vector2(0f, 270f), new Vector2(1140f, 40f));
            LocalizedText.Bind(roomsTitle, "ui.play.available_rooms");

            var viewport = UiFactory.CreateImage("Viewport", panel.transform, UiPalette.GlassRow);
            viewport.raycastTarget = true;
            UiFactory.SetCenteredRect(viewport.rectTransform, new Vector2(0f, 62f), new Vector2(1160f, 350f));
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = UiFactory.CreateRect("List", viewport.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            UiFactory.AddVerticalLayout(content.gameObject, 10f, new RectOffset(10, 10, 10, 10));
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
            scroll.scrollSensitivity = 34f;

            var manualCard = UiFactory.CreateGlassPanel(panel.transform, "Manual Code", UiPalette.GlassStrong);
            UiFactory.SetCenteredRect(manualCard.rectTransform, new Vector2(0f, -248f), new Vector2(1160f, 220f));

            var playerNameTitle = _ui.CreateText(
                "Player Name Title", manualCard.transform, string.Empty, 24, FontStyle.Bold,
                UiPalette.LightText, TextAnchor.MiddleLeft);
            UiFactory.SetCenteredRect(playerNameTitle.rectTransform, new Vector2(-430f, 67f), new Vector2(190f, 34f));
            LocalizedText.Bind(playerNameTitle, "ui.play.player_name");

            _browserPlayerNameField = _ui.CreateInputField(
                manualCard.transform, "PlayerName", GameSettings.PlayerName,
                _text.GetText("ui.settings.name_placeholder"), 26);
            _browserPlayerNameField.textComponent.font = GameFonts.K2D;
            if (_browserPlayerNameField.placeholder is Text browserNamePlaceholder)
                browserNamePlaceholder.font = GameFonts.K2D;
            _browserPlayerNameField.characterLimit = 64;
            UiFactory.SetCenteredRect(_browserPlayerNameField.GetComponent<RectTransform>(),
                new Vector2(105f, 67f), new Vector2(850f, 52f));
            _browserPlayerNameField.onEndEdit.AddListener(_ => TryCommitPlayerName(_browserPlayerNameField, false));

            _browserPlayerNameError = _ui.CreateText(
                "PlayerNameError", manualCard.transform, string.Empty, 20, FontStyle.Normal,
                UiPalette.TimerWarning, TextAnchor.MiddleLeft);
            UiFactory.SetCenteredRect(_browserPlayerNameError.rectTransform,
                new Vector2(105f, 31f), new Vector2(850f, 24f));

            var manualTitle = _ui.CreateText(
                "Title", manualCard.transform, string.Empty, 24, FontStyle.Bold,
                UiPalette.LightText, TextAnchor.MiddleLeft);
            UiFactory.SetCenteredRect(manualTitle.rectTransform, new Vector2(-430f, -50f), new Vector2(190f, 34f));
            LocalizedText.Bind(manualTitle, "ui.play.room_code");

            _joinCodeField = _ui.CreateInputField(manualCard.transform, "JoinCode", string.Empty, string.Empty, 28);
            UiFactory.SetCenteredRect(_joinCodeField.GetComponent<RectTransform>(), new Vector2(-70f, -50f), new Vector2(500f, 52f));
            if (_joinCodeField.placeholder is Text joinCodeHint)
                LocalizedText.Bind(joinCodeHint, "ui.play.room_code");

            _browserJoinCodeButton = _ui.CreateAccentButton(
                manualCard.transform, "JoinCodeBtn", string.Empty,
                UiPalette.Success, UiPalette.SuccessHighlight, () =>
                {
                    if (!TryCommitPlayerName(_browserPlayerNameField, false)) return;
                    if (!BeginConnectionAttempt()) return;
                    _browserPresenter.JoinByCode(_joinCodeField.text);
                }, 24);
            UiFactory.SetCenteredRect(_browserJoinCodeButton.GetComponent<RectTransform>(), new Vector2(390f, -50f), new Vector2(240f, 52f));
            LocalizedText.Bind(_browserJoinCodeButton.GetComponentInChildren<Text>(), "ui.play.join_code");

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
                new Vector2(220f, 60f), new Vector2(72f, 30f));
            LocalizedText.Bind(back.GetComponentInChildren<Text>(), "ui.play.back");

            SetNavigation(_browserPlayerNameField, null, _joinCodeField, null, null);
            SetNavigation(_joinCodeField, _browserPlayerNameField, _browserJoinCodeButton, null, _browserJoinCodeButton);
            SetNavigation(_browserJoinCodeButton, _joinCodeField, back, _joinCodeField, null);
            SetNavigation(back, _browserJoinCodeButton, null, null, null);

            ConfigureScreenEntrance(_browserRoot, title, subtitle, panel, back);
            _browserRoot.SetActive(false);
        }

        private void RefreshRoomList(IReadOnlyList<RoomInfo> rooms)
        {
            _browserRoomButtons.Clear();
            for (int i = _roomListRoot.childCount - 1; i >= 0; i--)
                Destroy(_roomListRoot.GetChild(i).gameObject);

            if (rooms == null || rooms.Count == 0)
            {
                SetNavigation(_joinCodeField, null, _browserJoinCodeButton, null, _browserJoinCodeButton);
                UpdateBrowserStatus(_text.GetText(GetBrowserStatusLocalizationKey()));
                return;
            }

            UpdateBrowserStatus(string.Empty);
            foreach (RoomInfo room in rooms)
            {
                RoomInfo captured = room;
                var row = UiFactory.CreateGlassPanel(_roomListRoot, "Room", UiPalette.GlassRow);
                UiFactory.SetLayoutSize(row.gameObject, 0f, 88f, 1f);

                var roomName = _ui.CreateText(
                    "Room Name", row.transform, room.Advertisement.RoomName, 28,
                    FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
                UiFactory.SetAnchoredRect(
                    roomName.rectTransform,
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                    new Vector2(-240f, 38f), new Vector2(24f, -10f));

                var roomMeta = _ui.CreateText(
                    "Room Meta", row.transform,
                    string.Format(
                        _text.GetText("ui.play.room_players"),
                        room.Advertisement.RoomCode,
                        room.Advertisement.CurrentPlayers,
                        room.Advertisement.MaxPlayers),
                    22, FontStyle.Normal, UiPalette.MutedText, TextAnchor.MiddleLeft);
                UiFactory.SetAnchoredRect(
                    roomMeta.rectTransform,
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                    new Vector2(-240f, 34f), new Vector2(24f, 10f));

                var button = _ui.CreateAccentButton(
                    row.transform, "Join", string.Empty,
                    UiPalette.Primary, UiPalette.PrimaryHighlight, () =>
                {
                    if (!TryCommitPlayerName(_browserPlayerNameField, false)) return;
                    if (!BeginConnectionAttempt()) return;
                    _browserPresenter.JoinRoom(captured);
                }, 24);
                UiFactory.SetAnchoredRect(
                    button.GetComponent<RectTransform>(),
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(190f, 54f), new Vector2(-20f, 0f));
                LocalizedText.Bind(button.GetComponentInChildren<Text>(), "ui.play.join");
                button.interactable = !_connectionAttemptPending;
                _browserRoomButtons.Add(button);
            }

            for (int i = 0; i < _browserRoomButtons.Count; i++)
            {
                Selectable up = i > 0 ? _browserRoomButtons[i - 1] : null;
                Selectable down = i + 1 < _browserRoomButtons.Count
                    ? _browserRoomButtons[i + 1]
                    : _joinCodeField;
                SetNavigation(_browserRoomButtons[i], up, down, null, null);
            }

            Selectable lastRoom = _browserRoomButtons.Count > 0
                ? _browserRoomButtons[_browserRoomButtons.Count - 1]
                : null;
            SetNavigation(_browserPlayerNameField, lastRoom, _joinCodeField, null, null);
            SetNavigation(_joinCodeField, _browserPlayerNameField, _browserJoinCodeButton, null, _browserJoinCodeButton);
        }

        #endregion

        #region Lobby

        private void BuildLobby()
        {
            _lobbyRoot = ForestScreen("Lobby");

            var title = _ui.CreateOutlinedTitle(_lobbyRoot.transform, "Title", string.Empty, 62);
            UiFactory.SetAnchoredRect(
                title.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(900f, 76f), new Vector2(0f, -42f));
            LocalizedText.Bind(title, "ui.play.lobby_title");

            var subtitle = _ui.CreateText(
                "Subtitle", _lobbyRoot.transform, string.Empty, 27, FontStyle.Normal,
                UiPalette.LightText, TextAnchor.MiddleCenter);
            UiFactory.SetAnchoredRect(
                subtitle.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(1100f, 42f), new Vector2(0f, -106f));
            LocalizedText.Bind(subtitle, "ui.play.lobby_subtitle");

            var panel = UiFactory.CreateImage("Panel", _lobbyRoot.transform, Color.clear);
            panel.raycastTarget = false;
            UiFactory.SetCenteredRect(panel.rectTransform, new Vector2(0f, -42f), new Vector2(1400f, 820f));

            var membersCard = UiFactory.CreateGlassPanel(panel.transform, "Members Card", UiPalette.Glass);
            UiFactory.SetCenteredRect(membersCard.rectTransform, new Vector2(-365f, 0f), new Vector2(600f, 700f));

            var setupCard = UiFactory.CreateGlassPanel(panel.transform, "Setup Card", UiPalette.Glass);
            UiFactory.SetCenteredRect(setupCard.rectTransform, new Vector2(335f, 0f), new Vector2(700f, 700f));

            _lobbyCode = _ui.CreateText("Code", membersCard.transform, string.Empty, 38, FontStyle.Bold, UiPalette.Primary, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_lobbyCode.rectTransform, new Vector2(0f, 286f), new Vector2(520f, 56f));
            UiFactory.AddDoubleOutline(_lobbyCode.gameObject, new Vector2(2.5f, -2.5f), new Vector2(1.2f, -1.2f));

            var membersTitle = _ui.CreateText("MembersTitle", membersCard.transform, string.Empty, 27, FontStyle.Bold, UiPalette.LightText, TextAnchor.MiddleLeft);
            UiFactory.SetCenteredRect(membersTitle.rectTransform, new Vector2(0f, 218f), new Vector2(520f, 40f));
            LocalizedText.Bind(membersTitle, "ui.play.members");

            _lobbyMembersRoot = UiFactory.CreateRect("Members", membersCard.transform).transform;
            UiFactory.SetCenteredRect((RectTransform)_lobbyMembersRoot, new Vector2(0f, -8f), new Vector2(540f, 410f));

            var setupTitle = _ui.CreateText("SetupTitle", setupCard.transform, string.Empty, 30, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(setupTitle.rectTransform, new Vector2(0f, 306f), new Vector2(620f, 44f));
            LocalizedText.Bind(setupTitle, "ui.play.match_setup");

            BuildColorPicker(setupCard.transform);
            BuildTeamPicker(setupCard.transform);

            var formatTitle = _ui.CreateText("FormatTitle", setupCard.transform, string.Empty, 24, FontStyle.Bold, UiPalette.LightText, TextAnchor.MiddleLeft);
            UiFactory.SetCenteredRect(formatTitle.rectTransform, new Vector2(0f, 142f), new Vector2(620f, 34f));
            LocalizedText.Bind(formatTitle, "ui.play.format");

            _formatIndividualButton = _ui.CreateButton(
                setupCard.transform, "FormatIndividual", string.Empty,
                UiPalette.Secondary, UiPalette.SecondaryHighlight, () => SetMatchFormat(MatchFormat.Individual));
            UiFactory.SetCenteredRect(_formatIndividualButton.GetComponent<RectTransform>(), new Vector2(-160f, 92f), new Vector2(290f, 56f));
            LocalizedText.Bind(_formatIndividualButton.GetComponentInChildren<Text>(), "ui.play.format_individual");

            _formatTeamButton = _ui.CreateButton(
                setupCard.transform, "FormatTeam", string.Empty,
                UiPalette.Secondary, UiPalette.SecondaryHighlight, () => SetMatchFormat(MatchFormat.Team));
            UiFactory.SetCenteredRect(_formatTeamButton.GetComponent<RectTransform>(), new Vector2(160f, 92f), new Vector2(290f, 56f));
            LocalizedText.Bind(_formatTeamButton.GetComponentInChildren<Text>(), "ui.play.format_team");

            var turnTimeTitle = _ui.CreateText(
                "TurnTimeTitle", setupCard.transform, string.Empty, 24, FontStyle.Bold,
                UiPalette.LightText, TextAnchor.MiddleLeft);
            UiFactory.SetCenteredRect(turnTimeTitle.rectTransform, new Vector2(0f, 48f), new Vector2(620f, 34f));
            LocalizedText.Bind(turnTimeTitle, "ui.play.turn_time");

            _turnRushButton = CreateTurnTimeButton(
                setupCard.transform, "TurnRush", TurnTimePreset.Rush, "ui.play.turn_rush", new Vector2(-232f, 4f));
            _turnShortButton = CreateTurnTimeButton(
                setupCard.transform, "TurnShort", TurnTimePreset.Short, "ui.play.turn_short", new Vector2(-78f, 4f));
            _turnNormalButton = CreateTurnTimeButton(
                setupCard.transform, "TurnNormal", TurnTimePreset.Normal, "ui.play.turn_normal", new Vector2(76f, 4f));
            _turnLongButton = CreateTurnTimeButton(
                setupCard.transform, "TurnLong", TurnTimePreset.Long, "ui.play.turn_long", new Vector2(230f, 4f));

            _lobbyStatus = _ui.CreateText("Status", setupCard.transform, string.Empty, 22, FontStyle.Normal, UiPalette.MutedText, TextAnchor.MiddleCenter);
            _lobbyStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.SetCenteredRect(_lobbyStatus.rectTransform, new Vector2(0f, -148f), new Vector2(620f, 66f));

            _lobbyStartButton = _ui.CreateAccentButton(
                setupCard.transform, "StartMatch", string.Empty,
                UiPalette.Success, UiPalette.SuccessHighlight, StartLobbyMatch, 28);
            UiFactory.SetCenteredRect(_lobbyStartButton.GetComponent<RectTransform>(), new Vector2(0f, -244f), new Vector2(480f, 66f));
            LocalizedText.Bind(_lobbyStartButton.GetComponentInChildren<Text>(), "ui.play.start_match");

            _lobbyLeaveButton = _ui.CreateAccentButton(
                setupCard.transform, "Leave", string.Empty,
                UiPalette.Danger, UiPalette.DangerHighlight, () =>
                {
                    _lobbyPresenter.LeaveRoom();
                    ReturnToMenu();
                }, 26);
            UiFactory.SetCenteredRect(_lobbyLeaveButton.GetComponent<RectTransform>(), new Vector2(0f, -322f), new Vector2(360f, 56f));
            LocalizedText.Bind(_lobbyLeaveButton.GetComponentInChildren<Text>(), "ui.play.leave");

            SetNavigation(_formatIndividualButton, null, _turnRushButton, null, _formatTeamButton);
            SetNavigation(_formatTeamButton, null, _turnLongButton, _formatIndividualButton, null);
            SetNavigation(_turnRushButton, _formatIndividualButton, _turnShortButton, null, _turnNormalButton);
            SetNavigation(_turnShortButton, _turnRushButton, _turnNormalButton, null, _turnLongButton);
            SetNavigation(_turnNormalButton, _turnShortButton, _turnLongButton, _turnRushButton, null);
            SetNavigation(_turnLongButton, _turnNormalButton, _team1Button, _turnShortButton, null);
            SetNavigation(_team1Button, _turnLongButton, _lobbyStartButton, null, _team2Button);
            SetNavigation(_team2Button, _turnLongButton, _lobbyStartButton, _team1Button, null);
            SetNavigation(_lobbyStartButton, _team1Button, _lobbyLeaveButton, null, null);
            SetNavigation(_lobbyLeaveButton, _lobbyStartButton, null, null, null);

            ConfigureScreenEntrance(_lobbyRoot, title, subtitle, panel);
            _lobbyRoot.SetActive(false);
        }

        private void StartLobbyMatch()
        {
            _lobbyPresenter.StartMatch();
        }

        /// <summary>
        /// Row of every selectable colour. All of them are on screen at once so
        /// picking one is a single tap, and the swatches themselves are the only
        /// label the row needs.
        /// </summary>
        private void BuildColorPicker(Transform panel)
        {
            var title = _ui.CreateText("ColorTitle", panel, string.Empty, 24, FontStyle.Bold, UiPalette.LightText, TextAnchor.MiddleLeft);
            UiFactory.SetCenteredRect(title.rectTransform, new Vector2(0f, 246f), new Vector2(620f, 34f));
            LocalizedText.Bind(title, "ui.play.your_color");

            var row = UiFactory.CreateRect("Colors", panel);
            UiFactory.SetCenteredRect(row, new Vector2(0f, 198f), new Vector2(ColorRowWidth, ColorRingSize));

            int count = PlayerColorPalette.Count;
            _colorButtons = new Button[count];
            _colorRingFeedback = new ColorSwatchRingFeedback[count];

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

                _colorButtons[i] = CreateColorSwatch(
                    cell,
                    colorId,
                    ring,
                    () => _lobbyPresenter.RequestColor(colorId));
                _colorRingFeedback[i] = _colorButtons[i].GetComponent<ColorSwatchRingFeedback>();
            }
        }

        /// <summary>
        /// Circular swatch button. The fill stays the palette colour in every
        /// interactable state; hover and ownership are drawn on the ring so the
        /// colour itself stays readable.
        /// </summary>
        private static Button CreateColorSwatch(
            Transform parent,
            byte colorId,
            Image ring,
            UnityEngine.Events.UnityAction onClick)
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
                highlightedColor = color,
                pressedColor = color,
                selectedColor = color,
                // Taken colours stay recognisable but visibly muted.
                disabledColor = new Color(color.r, color.g, color.b, 0.28f),
                colorMultiplier = 1f,
                fadeDuration = 0.1f
            };
            button.onClick.AddListener(onClick);

            var feedback = buttonObject.AddComponent<ColorSwatchRingFeedback>();
            feedback.Configure(ring, ColorRingIdle, ColorRingHover, UiPalette.WinnerGold);
            return button;
        }

        /// <summary>
        /// Team 1 / Team 2 buttons for the local player in team mode. Hidden
        /// in individual mode; every machine can pick a team once the host
        /// switches the lobby to team format.
        /// </summary>
        private void BuildTeamPicker(Transform panel)
        {
            _teamPickerRoot = UiFactory.CreateRect("TeamPicker", panel).gameObject;
            UiFactory.Stretch(_teamPickerRoot.GetComponent<RectTransform>());

            var title = _ui.CreateText("TeamTitle", _teamPickerRoot.transform, string.Empty, 24, FontStyle.Bold, UiPalette.LightText, TextAnchor.MiddleLeft);
            UiFactory.SetCenteredRect(title.rectTransform, new Vector2(0f, -44f), new Vector2(620f, 34f));
            LocalizedText.Bind(title, "ui.play.pick_team");

            _team1Button = _ui.CreateButton(
                _teamPickerRoot.transform, "Team1", string.Empty,
                UiPalette.Secondary, UiPalette.SecondaryHighlight, () => _lobbyPresenter.RequestTeam(0));
            UiFactory.SetCenteredRect(_team1Button.GetComponent<RectTransform>(), new Vector2(-160f, -94f), new Vector2(290f, 56f));
            LocalizedText.Bind(_team1Button.GetComponentInChildren<Text>(), "ui.play.team_one");

            _team2Button = _ui.CreateButton(
                _teamPickerRoot.transform, "Team2", string.Empty,
                UiPalette.Secondary, UiPalette.SecondaryHighlight, () => _lobbyPresenter.RequestTeam(1));
            UiFactory.SetCenteredRect(_team2Button.GetComponent<RectTransform>(), new Vector2(160f, -94f), new Vector2(290f, 56f));
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

        private Button CreateTurnTimeButton(
            Transform parent,
            string name,
            TurnTimePreset preset,
            string localizationKey,
            Vector2 position)
        {
            var button = _ui.CreateButton(
                parent,
                name,
                string.Empty,
                UiPalette.Secondary,
                UiPalette.SecondaryHighlight,
                () => SetTurnTimePreset(preset));
            UiFactory.SetCenteredRect(button.GetComponent<RectTransform>(), position, new Vector2(148f, 48f));
            LocalizedText.Bind(button.GetComponentInChildren<Text>(), localizationKey);
            return button;
        }

        private void SetTurnTimePreset(TurnTimePreset preset)
        {
            if (_lobbyPresenter == null || !_lobbyPresenter.IsHost)
                return;

            _lobbyPresenter.SelectedTurnTimePreset = preset;
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

            float y = 150f;
            var members = _lobbyPresenter.Members
                .Where(player => player != null)
                .OrderByDescending(player => player.IsHost)
                .ThenByDescending(player => player.isLocalPlayer)
                .ToList();
            MatchFormat format = _lobbyPresenter.SelectedFormat;
            for (int index = 0; index < members.Count; index++)
            {
                NetworkPlayer player = members[index];
                var card = UiFactory.CreateGlassPanel(_lobbyMembersRoot, "MemberCard", UiPalette.GlassRow);
                UiFactory.SetCenteredRect(card.rectTransform, new Vector2(0f, y), new Vector2(520f, 68f));

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
                var label = _ui.CreateText("Name", card.transform, displayName + badges, 24, FontStyle.Bold, UiPalette.LightText, TextAnchor.MiddleLeft);
                label.supportRichText = true;
                UiFactory.SetStretchRect(label.rectTransform, 72f, 0f, 24f, 0f);

                y -= 78f;
            }

            for (int index = members.Count; index < GameRules.MaxPlayers; index++)
            {
                var card = UiFactory.CreateGlassPanel(_lobbyMembersRoot, "EmptyMemberCard", new Color(1f, 1f, 1f, 0.035f));
                UiFactory.SetCenteredRect(card.rectTransform, new Vector2(0f, y), new Vector2(520f, 68f));
                var label = _ui.CreateText(
                    "Waiting", card.transform, string.Empty, 23, FontStyle.Normal,
                    UiPalette.MutedText, TextAnchor.MiddleCenter);
                UiFactory.Stretch(label.rectTransform);
                LocalizedText.Bind(label, "ui.play.waiting_player_slot");
                y -= 78f;
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

            TurnTimePreset turnPreset = _lobbyPresenter.SelectedTurnTimePreset;
            if (_turnRushButton != null)
                _turnRushButton.interactable = weAreHost;
            if (_turnShortButton != null)
                _turnShortButton.interactable = weAreHost;
            if (_turnNormalButton != null)
                _turnNormalButton.interactable = weAreHost;
            if (_turnLongButton != null)
                _turnLongButton.interactable = weAreHost;

            HighlightFormatButton(_turnRushButton, turnPreset == TurnTimePreset.Rush);
            HighlightFormatButton(_turnShortButton, turnPreset == TurnTimePreset.Short);
            HighlightFormatButton(_turnNormalButton, turnPreset == TurnTimePreset.Normal);
            HighlightFormatButton(_turnLongButton, turnPreset == TurnTimePreset.Long);

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

            if (_lobbyStartButton != null)
            {
                _lobbyStartButton.interactable = weAreHost && blockedReason == null;
                _lobbyStartButton.gameObject.SetActive(weAreHost);
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
                    _colorButtons[i].interactable = !_lobbyPresenter.IsColorTaken(colorId);

                if (_colorRingFeedback[i] != null)
                    _colorRingFeedback[i].SetOwned(isMine);
            }
        }

        private void RefreshTeamPicker()
        {
            if (_teamPickerRoot == null) return;

            MatchFormat format = _lobbyPresenter.SelectedFormat;
            bool showPicker = format == MatchFormat.Team;
            _teamPickerRoot.SetActive(showPicker);
            if (!showPicker)
            {
                SetNavigation(_turnLongButton, _turnNormalButton, _lobbyStartButton, _turnShortButton, null);
                SetNavigation(_lobbyStartButton, _turnLongButton, _lobbyLeaveButton, null, null);
                return;
            }

            byte mine = _lobbyPresenter.LocalTeamId;
            HighlightFormatButton(_team1Button, mine == 0);
            HighlightFormatButton(_team2Button, mine == 1);
            SetNavigation(_turnLongButton, _turnNormalButton, _team1Button, _turnShortButton, null);
            SetNavigation(_lobbyStartButton, _team1Button, _lobbyLeaveButton, null, null);
        }

        private static void HighlightFormatButton(Button button, bool selected)
        {
            if (button == null)
                return;

            var colors = button.colors;
            colors.normalColor = UiPalette.Secondary;
            colors.highlightedColor = UiPalette.SecondaryHighlight;
            colors.pressedColor = Color.Lerp(UiPalette.Secondary, Color.black, 0.16f);
            colors.selectedColor = UiPalette.SecondaryHighlight;
            button.colors = colors;
            UiFactory.SetChoiceSelected(button, selected);
        }

        #endregion

        #region Match

        private void BuildMatch()
        {
            _matchRoot = new GameObject("Match", typeof(RectTransform));
            _matchRoot.transform.SetParent(_canvas.transform, false);
            UiFactory.Stretch(_matchRoot.GetComponent<RectTransform>());

            Image matchBackground = UiFactory.CreateFullScreenBackground(
                _matchRoot.transform,
                "In Game Backgrounds",
                UiPalette.Background);
            matchBackground.raycastTarget = true;
            matchBackground.gameObject.AddComponent<TilePreviewBackground>()
                .Configure(() => _tilePreviewPanel?.Hide());

            _matchStatus = _ui.CreateText(
                "Status", _matchRoot.transform, string.Empty, 26, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter);
            MatchHudLayout.Standard.Status.Apply(_matchStatus.rectTransform);
            UiFactory.AddDoubleOutline(_matchStatus.gameObject, new Vector2(3f, -3f), new Vector2(1.5f, -1.5f));

            MatchHudElements.Timer timer = MatchHudElements.CreateTimer(
                _ui, _matchRoot.transform, string.Empty, "01:00");
            _matchTimerPanel = timer.Panel;
            _matchTimer = timer.Value;
            _matchTimerFill = timer.Fill;
            LocalizedText.Bind(timer.Caption, "ui.match.time");

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
            _tilePreviewPanel = new TilePreviewPanel(
                _ui, _text, _matchRoot.transform, MatchHudLayout.Standard.TilePreview);

            var boardFrame = MatchHudElements.CreateBoardFrame(_matchRoot.transform);

            var boardRoot = UiFactory.CreateRect("Board", boardFrame.transform);
            UiFactory.SetCenteredRect(boardRoot, Vector2.zero, MatchHudLayout.Standard.BoardSize);
            _boardView = new MatchBoardView(
                _ui,
                boardRoot,
                OnCellClicked,
                MatchHudLayout.Standard.BoardCellSize,
                onTileClicked: tileId => _tilePreviewPanel.Show(tileId),
                onEmptyCellClicked: () => _tilePreviewPanel.Hide());
            _tilePlacementInput = new TilePlacementInput(
                _boardView,
                CanPlaceTiles,
                () => _matchPresenter?.TurnInput?.SelectedRackIndex,
                PlaceFromRack,
                RefreshBoardVisual);
            _boardView.ConfigureCells(_tilePlacementInput.AttachBoardCell);

            var rackShelf = MatchHudElements.CreateRackShelf(_matchRoot.transform);

            _rackRoot = UiFactory.CreateRect("Rack", rackShelf.transform).transform;
            UiFactory.Stretch((RectTransform)_rackRoot);

            _matchPreview = _ui.CreateText(
                "Preview", _matchRoot.transform, string.Empty, 16, FontStyle.Normal,
                Color.white, TextAnchor.MiddleCenter);
            _matchPreview.horizontalOverflow = HorizontalWrapMode.Wrap;
            _matchPreview.verticalOverflow = VerticalWrapMode.Overflow;
            MatchHudLayout.Standard.Preview.Apply(_matchPreview.rectTransform);
            UiFactory.AddOutline(_matchPreview.gameObject, Color.black, new Vector2(1.5f, -1.5f));

            _declareRoot = UiFactory.CreateRect("Declare", _matchRoot.transform).transform;
            MatchHudLayout.Standard.Declare.Apply((RectTransform)_declareRoot);
            _declareRoot.gameObject.SetActive(false);

            RectTransform commandDrawerRoot = UiFactory.CreateRect("Command Drawer", _matchRoot.transform);
            Vector2 drawerExpandedPosition = MatchHudLayout.Standard.CommandDrawer.Position;
            MatchHudLayout.Standard.CommandDrawer.Apply(commandDrawerRoot);

            Image commandPanel = UiFactory.CreateGlassPanel(
                commandDrawerRoot,
                "Panel",
                new Color(UiPalette.GlassStrong.r, UiPalette.GlassStrong.g, UiPalette.GlassStrong.b, 0.94f));
            UiFactory.Stretch(commandPanel.rectTransform);
            UiFactory.AddOutline(commandPanel.gameObject, new Color(1f, 1f, 1f, 0.16f), new Vector2(1.5f, -1.5f));

            int actionIndex = 0;
            _confirmButton = CreateMatchAction(commandDrawerRoot, "Confirm", "ui.match.confirm", UiPalette.Success, UiPalette.SuccessHighlight, () => _matchPresenter.ConfirmPlace(), actionIndex++);
            _clearButton = CreateMatchAction(commandDrawerRoot, "Clear", "ui.match.clear", UiPalette.Secondary, UiPalette.SecondaryHighlight, () => _matchPresenter.ClearDraft(), actionIndex++);
            _passButton = CreateMatchAction(commandDrawerRoot, "Pass", "ui.match.pass", UiPalette.Secondary, UiPalette.SecondaryHighlight, () => _matchPresenter.Pass(), actionIndex++);
            _exchangeButton = CreateMatchAction(commandDrawerRoot, "Exchange", "ui.match.exchange", UiPalette.Primary, UiPalette.PrimaryHighlight, ToggleExchangeMode, actionIndex);

            _commandDrawer = commandDrawerRoot.gameObject.AddComponent<MatchCommandDrawerAnimator>();
            _commandDrawer.Configure(
                drawerExpandedPosition,
                MatchHudLayout.Standard.CommandDrawerCollapsedPosition,
                CommandDrawerAnimationSeconds);
            _commandDrawer.SetExpandedInstant(false);

            _commandDrawerToggle = _ui.CreateAccentButton(
                _matchRoot.transform,
                "Command Drawer Toggle",
                string.Empty,
                UiPalette.Primary,
                UiPalette.PrimaryHighlight,
                ToggleCommandDrawer,
                18);
            MatchHudLayout.Standard.CommandToggle.Apply(
                _commandDrawerToggle.GetComponent<RectTransform>());
            _commandDrawerToggleLabel = _commandDrawerToggle.GetComponentInChildren<Text>();
            UpdateCommandDrawerToggleLabel();

            _matchRoot.SetActive(false);
        }

        private void BuildPlayerSeats()
        {
            for (int i = 0; i < VisibleSeatCount; i++)
                _seats[i] = MatchHudElements.CreateSeat(_ui, _matchRoot.transform, i);
        }

        private void BuildBagHud()
        {
            _bagLabel = MatchHudElements.CreateBag(_ui, _matchRoot.transform);
        }

        private Button CreateMatchAction(
            Transform parent,
            string name,
            string labelKey,
            Color color,
            Color highlight,
            UnityEngine.Events.UnityAction action,
            int index)
        {
            var button = MatchHudElements.CreateAction(
                _ui, parent, name, string.Empty, color, highlight, () => action(), index);
            LocalizedText.Bind(button.GetComponentInChildren<Text>(), labelKey);
            return button;
        }

        private void ToggleCommandDrawer()
        {
            if (_commandDrawer == null)
                return;

            _commandDrawer.Toggle();
            UpdateCommandDrawerToggleLabel();

            if (_commandDrawer.IsExpanded)
                UiFactory.SelectFirstInteractable(_confirmButton, _clearButton, _passButton, _exchangeButton);
            else
                UiFactory.SelectFirstInteractable(_commandDrawerToggle);
        }

        private void UpdateCommandDrawerToggleLabel()
        {
            if (_commandDrawerToggleLabel == null || _text == null)
                return;

            string key = _commandDrawer != null && _commandDrawer.IsExpanded
                ? "ui.match.commands_hide"
                : "ui.match.commands_open";
            _commandDrawerToggleLabel.text = _text.GetText(key);
        }

        private void PulseTurnRings()
        {
            float pulse = 0.72f + 0.28f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.2f));
            for (int i = 0; i < VisibleSeatCount; i++)
            {
                MatchHudElements.Seat seat = _seats[i];
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

        private bool CanPlaceTiles() =>
            _matchPresenter != null && _matchPresenter.IsMyTurnReady && !_exchangeMode;

        private void PlaceFromRack(int rackIndex, int x, int y) =>
            _matchPresenter?.PlaceFromRack(rackIndex, x, y);

        private void RefreshBoardVisual()
        {
            BoardManager boardManager = null;
            NetworkContext.Services?.TryResolve(out boardManager);
            TurnInputSession input = _matchPresenter?.TurnInput;
            _boardView?.Refresh(boardManager?.Grid, input);
        }

        private void OnCellClicked(int x, int y)
        {
            if (!CanPlaceTiles()) return;

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

            BoardManager boardManager = null;
            NetworkContext.Services?.TryResolve(out boardManager);
            if (boardManager?.Grid != null && boardManager.Grid.IsOccupied(x, y))
                return;

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
            UpdateCommandDrawerToggleLabel();

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
                MatchHudElements.Seat seat = _seats[i];
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
                seat.Label.color = MatchHudElements.ContrastingTextColor(seat.Avatar.color);

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
                    image.color = selected ? UiPalette.CellSelected : Color.white;
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

                var rackRect = button.GetComponent<RectTransform>();
                UiFactory.SetCenteredRect(
                    rackRect,
                    new Vector2(start + i * RackPitch, 0f),
                    new Vector2(70f, 84f));
                rackRect.localScale = Vector3.one;

                int rackIndex = i;
                _tilePlacementInput?.AttachRackButton(
                    button,
                    rackIndex,
                    CaptureRackSpriteProvider(rackIndex, index =>
                    {
                        PlayerState rackOwner = _matchPresenter.Players?.GetById(_matchPresenter.LocalPlayerId);
                        if (rackOwner == null || index >= rackOwner.Rack.Count)
                            return null;
                        return TileIcons.ForTile(rackOwner.Rack[index]);
                    }));
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
                button.gameObject.AddComponent<TilePreviewRackTarget>().Configure(
                    () =>
                    {
                        PlayerState owner = _matchPresenter?.Players?.GetById(_matchPresenter.LocalPlayerId);
                        return owner != null && index < owner.Rack.Count
                            ? (byte?)owner.Rack[index]
                            : null;
                    },
                    tileId => _tilePreviewPanel.Show(tileId));
                _tilePlacementInput?.AttachRackButton(
                    button,
                    index,
                    CaptureRackSpriteProvider(index, rackIndex =>
                    {
                        PlayerState rackOwner = _matchPresenter?.Players?.GetById(_matchPresenter.LocalPlayerId);
                        if (rackOwner == null || rackIndex >= rackOwner.Rack.Count)
                            return null;
                        return TileIcons.ForTile(rackOwner.Rack[rackIndex]);
                    }));
            }
        }

        internal static Func<Sprite> CaptureRackSpriteProvider(
            int rackIndex,
            Func<int, Sprite> spriteProvider)
        {
            if (spriteProvider == null) throw new ArgumentNullException(nameof(spriteProvider));
            return () => spriteProvider(rackIndex);
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
            UiFactory.SetCenteredRect(title.rectTransform, new Vector2(0f, 24f), new Vector2(500f, 28f));

            byte tileId = selected.Value;
            float x = -420f;
            void AddOption(byte declaredAs)
            {
                byte value = declaredAs;
                var button = _ui.CreateButton(_declareRoot, $"D{value}", SymbolOf(value), UiPalette.Card, UiPalette.PrimaryHighlight, () =>
                {
                    _matchPresenter.SetDeclaration(value);
                }, 22);
                UiFactory.SetCenteredRect(button.GetComponent<RectTransform>(), new Vector2(x, -14f), new Vector2(52f, 42f));
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

            _pauseLeaveButton = _ui.CreateAccentButton(_pauseRoot.transform, "Leave", string.Empty,
                UiPalette.Danger, UiPalette.DangerHighlight, () =>
            {
                OverlayFade.Ensure(_pauseRoot)?.HideInstant();
                _matchPresenter.LeaveRoom();
                ReturnToMenu();
            }, 42);
            LocalizedText.Bind(_pauseLeaveButton.GetComponentInChildren<Text>(), "ui.pause.leave");

            OverlayFade.Ensure(_pauseRoot);
            var pauseEntrance = _pauseRoot.AddComponent<MenuEntranceAnimator>();
            pauseEntrance.SetTargets(title, _pauseResumeButton, _pausePlayOnButton, _pauseSettingsButton, _pauseLeaveButton);
            pauseEntrance.Configure(0.22f, 0.03f, 0f);
            _pauseRoot.SetActive(false);
        }

        private void OpenPause()
        {
            if (_screen != ScreenId.Match) return;
            OverlayFade.Ensure(_pauseRoot).FadeIn();
            _pauseRoot.GetComponent<MenuEntranceAnimator>()?.Play();
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
                _matchSettings = SettingsMenuController.Create(
                    transform,
                    _ui.Font,
                    SettingsBackdropMode.LiveMatchOverlay);
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

            var overlay = UiFactory.CreateImage("Overlay", _resultRoot.transform,
                new Color(0.01f, 0.015f, 0.025f, 0.86f));
            UiFactory.Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;

            RectTransform resultContent = UiFactory.CreateRect("Result Content", _resultRoot.transform);
            UiFactory.SetCenteredRect(resultContent, new Vector2(0f, 90f), new Vector2(780f, 800f));

            _resultWinnerCaption = _ui.CreateOutlinedTitle(
                resultContent, "WinnerLabel", string.Empty, 56);
            _resultWinnerCaption.color = UiPalette.WinnerGold;
            UiFactory.SetCenteredRect(_resultWinnerCaption.rectTransform, new Vector2(0f, 160f), new Vector2(720f, 72f));
            _resultWinnerCaption.text = _text.GetText("ui.result.winner");

            _resultWinner = _ui.CreateOutlinedTitle(
                resultContent, "Winner", string.Empty, 48);
            UiFactory.SetCenteredRect(_resultWinner.rectTransform, new Vector2(0f, 80f), new Vector2(720f, 64f));

            _resultMeta = _ui.CreateText(
                "Meta", resultContent, string.Empty, 28, FontStyle.Normal,
                Color.white, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_resultMeta.rectTransform, new Vector2(0f, 20f), new Vector2(720f, 48f));
            UiFactory.AddDoubleOutline(_resultMeta.gameObject, new Vector2(2.5f, -2.5f), new Vector2(1.2f, -1.2f));

            _resultBody = _ui.CreateText(
                "Body", resultContent, string.Empty, 26, FontStyle.Normal,
                UiPalette.LightText, TextAnchor.UpperCenter);
            _resultBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _resultBody.verticalOverflow = VerticalWrapMode.Overflow;
            _resultBody.supportRichText = true;
            UiFactory.SetCenteredRect(_resultBody.rectTransform, new Vector2(0f, -120f), new Vector2(640f, 220f));

            _resultRematchButton = _ui.CreateAccentButton(
                resultContent, "Rematch", string.Empty,
                UiPalette.Success, UiPalette.SuccessHighlight, () =>
                {
                    if (_resultPresenter.CanRematch)
                    {
                        _resultPresenter.Rematch();
                    }
                }, 26);
            UiFactory.SetCenteredRect(_resultRematchButton.GetComponent<RectTransform>(), new Vector2(0f, -280f), new Vector2(360f, 60f));
            LocalizedText.Bind(_resultRematchButton.GetComponentInChildren<Text>(), "ui.result.rematch");

            _resultLeaveButton = _ui.CreateAccentButton(
                resultContent, "Leave", string.Empty,
                UiPalette.Danger, UiPalette.DangerHighlight, () =>
                {
                    _resultPresenter.Leave();
                    ReturnToMenu();
                }, 26);
            UiFactory.SetCenteredRect(_resultLeaveButton.GetComponent<RectTransform>(), new Vector2(0f, -360f), new Vector2(320f, 56f));
            LocalizedText.Bind(_resultLeaveButton.GetComponentInChildren<Text>(), "ui.result.leave");

            OverlayFade.Ensure(_resultRoot);
            var resultEntrance = _resultRoot.AddComponent<MenuEntranceAnimator>();
            resultEntrance.SetTargets(_resultWinnerCaption, _resultWinner, _resultMeta,
                _resultBody, _resultRematchButton, _resultLeaveButton);
            resultEntrance.Configure(0.22f, 0.03f, 0f);
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

            _resultWinnerCaption.text = _text.GetText(result.IsDraw ? "ui.result.outcome" : "ui.result.winner");

            PlayerResult winner = null;
            foreach (PlayerResult row in result.Standings)
            {
                if (row.PlayerId == result.WinnerPlayerId)
                {
                    winner = row;
                    break;
                }
            }

            if (result.IsDraw)
            {
                _resultWinner.text = _text.GetText("ui.result.draw");
            }
            else if (result.Format == MatchFormat.Team && result.WinnerTeamId >= 0)
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
                    if (!result.IsDraw && team.TeamId == result.WinnerTeamId)
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
                if (!result.IsDraw && row.PlayerId == result.WinnerPlayerId)
                    line = $"<color=#FDA733><b>{line}</b></color>";
                sb.AppendLine(line);
                rank++;
            }

            _resultBody.text = sb.ToString();

            var rematch = _resultRoot.transform.Find("Result Content/Rematch")?.GetComponent<Button>();
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

            var leaveButton = _ui.CreateAccentButton(card, "Leave", string.Empty, UiPalette.Danger, UiPalette.DangerHighlight, () =>
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

        private static void SetNavigation(
            Selectable selectable,
            Selectable up,
            Selectable down,
            Selectable left,
            Selectable right)
        {
            if (selectable == null)
                return;

            var navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;
            navigation.selectOnLeft = left;
            navigation.selectOnRight = right;
            selectable.navigation = navigation;
        }

        private GameObject ForestScreen(string name)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(_canvas.transform, false);
            UiFactory.Stretch(root.GetComponent<RectTransform>());
            UiFactory.CreateFullScreenBackground(root.transform, "Main Menu Backgrounds", UiPalette.Background);
            root.SetActive(false);
            return root;
        }

        private static void ConfigureScreenEntrance(GameObject root, params Component[] targets)
        {
            var entrance = root.AddComponent<MenuEntranceAnimator>();
            entrance.SetTargets(targets);
            entrance.Configure(0.26f, 0.045f, 0f);
        }
    }

    /// <summary>
    /// Draws colour-swatch hover and ownership on the outer ring only, so the
    /// fill colour never gets washed out.
    /// </summary>
    internal sealed class ColorSwatchRingFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Image _ring;
        private Color _idle;
        private Color _hover;
        private Color _owned;
        private bool _isOwned;
        private bool _hovered;

        public void Configure(Image ring, Color idle, Color hover, Color owned)
        {
            _ring = ring;
            _idle = idle;
            _hover = hover;
            _owned = owned;
            Apply();
        }

        public void SetOwned(bool isOwned)
        {
            _isOwned = isOwned;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Apply();
        }

        private void OnDisable()
        {
            _hovered = false;
            Apply();
        }

        private void Apply()
        {
            if (_ring == null)
                return;

            if (_isOwned)
                _ring.color = _owned;
            else if (_hovered)
                _ring.color = _hover;
            else
                _ring.color = _idle;
        }
    }
}
