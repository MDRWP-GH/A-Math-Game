using System.Collections;
using AMath.Art;
using AMath.Accounts;
using AMath.Bootstrap;
using AMath.Settings;
using AMath.UI.Localization;
using AMath.UI.Tutorial;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>
    /// Builds and owns the first A-Math menu at runtime. Keeping it code-driven makes the
    /// starter scene safe to reuse while the game screens are still being developed.
    /// Layout matches the main-menu mockup: background art, title, and the text buttons,
    /// which fade in one after another whenever the menu is shown.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        /// <summary>
        /// Without every one of these the menu cannot be driven by mouse, keyboard or gamepad, so
        /// a project-wide action map that lacks any of them is not worth using.
        /// </summary>
        private static readonly string[] RequiredUiActions = { "Point", "Click", "Navigate", "Submit", "Cancel" };

        private UiFactory _ui;
        private GameObject _menuCanvas;
        private CanvasGroup _menuInputGroup;
        private HowToPlayOverlay _helpOverlay;
        private Button _startButton;
        private Button _historyButton;
        private Button _tutorialButton;
        private Button _helpButton;
        private Button _settingsButton;
        private Button _quitButton;
        private SettingsMenuController _settingsMenu;
        private MatchHistoryOverlay _historyOverlay;
        private InputAction _cancelAction;
        private MenuEntranceAnimator _entrance;
        private LocalAccountService _accounts;
        private AccountGateView _accountGate;
        private Button _logoutButton;

        private void Awake()
        {
            EnsureEventSystem();
            SceneTransitionController.SetStartupStatus("ui.loading.account");
            _ui = new UiFactory(GameFonts.Jersey25);
            _accounts = new LocalAccountService();
            AccountError autoLoginError = AccountError.None;
            bool authenticated = AccountSession.IsAuthenticated || _accounts.TryAutoLogin(out autoLoginError);
            if (authenticated)
                GameSettings.UsePlayerProfile(AccountSession.AccountId);
            else
                GameSettings.ClearPlayerProfile();
            BuildMenu();
            _accountGate = new AccountGateView(_ui, transform, _accounts);
            _accountGate.Authenticated += OnAuthenticated;
            if (authenticated)
                ShowAuthenticatedMenu();
            else
                ShowAccountGate(autoLoginError);
            SceneTransitionController.DestinationRevealStarted += OnDestinationRevealStarted;
            _cancelAction = UiFactory.FindCancelAction();
        }

        private void OnDestroy()
        {
            SceneTransitionController.DestinationRevealStarted -= OnDestinationRevealStarted;
        }

        private void OnDestinationRevealStarted()
        {
            if (!isActiveAndEnabled)
                return;
            if (_accountGate != null && _accountGate.IsOpen)
                _accountGate.ReplayEntrance();
            else
                PlayEntranceAnimation();
        }

        private void Start()
        {
            // The login gate and its first selectable are ready now. During a real scene
            // transition the transition controller owns dismissal after activation instead.
            if (!SceneTransitionController.IsTransitioning)
                SceneTransitionController.NotifyStartupReady();
        }

        private void OnEnable()
        {
            if (_accountGate == null || !_accountGate.IsOpen)
            {
                PlayEntranceAnimation();
                UiFactory.SelectFirstInteractable(_startButton);
            }
        }

        private void Update()
        {
            bool cancelPressed = WasCancelPressed();

            if (_accountGate != null && _accountGate.IsOpen)
            {
                if (cancelPressed)
                    _accountGate.HandleCancel();
                return;
            }

            // Cancel doubles as "skip the intro" so the menu never feels like it
            // is holding the player up.
            if (cancelPressed && _entrance != null && _entrance.IsPlaying)
            {
                _entrance.Skip();
                return;
            }

            if (!cancelPressed)
                return;

            // Topmost first, one layer per press: the replay reader sits above the
            // history list, so Cancel steps back through them instead of closing
            // the list out from under a reader that stays on screen.
            if (_historyOverlay != null && _historyOverlay.IsReplayOpen)
                _historyOverlay.CloseReplay();
            else if (_historyOverlay != null && _historyOverlay.IsOpen)
                _historyOverlay.Close();
            else if (_helpOverlay != null && _helpOverlay.IsOpen)
                _helpOverlay.Close();
        }

        private void BuildMenu()
        {
            var canvas = _ui.CreateCanvas(transform, "Canvas", 100);
            _menuCanvas = canvas.gameObject;
            _menuInputGroup = _menuCanvas.AddComponent<CanvasGroup>();

            UiFactory.CreateFullScreenBackground(
                canvas.transform,
                "Main Menu Backgrounds",
                UiPalette.Background);

            var title = _ui.CreateOutlinedTitle(
                canvas.transform,
                "Title",
                string.Empty,
                78);
            title.font = GameFonts.JainiPurva;
            UiFactory.SetAnchoredRect(
                title.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(980f, 110f),
                new Vector2(0f, -72f));
            LocalizedText.Bind(title, "ui.menu.title");

            const float buttonWidth = 520f;
            const float buttonHeight = 64f;
            const float buttonPitch = 70f;
            const float firstButtonY = 110f;
            const int buttonFontSize = 44;

            _startButton = PlaceMenuButton(canvas.transform, "Start Button", firstButtonY, buttonWidth, buttonHeight, buttonFontSize, StartGame, "ui.menu.start");
            _historyButton = PlaceMenuButton(canvas.transform, "History Button", firstButtonY - buttonPitch, buttonWidth, buttonHeight, buttonFontSize, OpenHistory, "ui.menu.history");
            _tutorialButton = PlaceMenuButton(canvas.transform, "Tutorial Button", firstButtonY - buttonPitch * 2f, buttonWidth, buttonHeight, buttonFontSize, OpenTutorial, null);
            ConfigureTutorialButton();

            _helpButton = PlaceMenuButton(canvas.transform, "How To Play Button", firstButtonY - buttonPitch * 3f, buttonWidth, buttonHeight, buttonFontSize, OpenHelp, "ui.menu.help");
            _settingsButton = PlaceMenuButton(canvas.transform, "Settings Button", firstButtonY - buttonPitch * 4f, buttonWidth, buttonHeight, buttonFontSize, OpenSettings, "ui.menu.settings");
            _quitButton = PlaceMenuButton(canvas.transform, "Exit Button", firstButtonY - buttonPitch * 5f, buttonWidth, buttonHeight, buttonFontSize, QuitGame, "ui.menu.quit");

            BuildAccountFooter(canvas.transform);

            ConfigureMenuNavigation();
            BuildEntranceAnimation(title);
            BuildHelpOverlay(canvas.transform);
            BuildHistoryOverlay(canvas.transform);
            BuildSettingsMenu();
        }

        private void StartGame()
        {
            PlaySessionController.EnsureExists().OpenFromMainMenu(this);
        }


        /// <summary>
        /// A tutorial button that loads nothing is worse than no button, so
        /// when the scene is missing from Build Settings the entry is disabled
        /// and says so instead of failing on click.
        /// </summary>
        private void ConfigureTutorialButton()
        {
            var label = _tutorialButton.GetComponentInChildren<Text>();
            if (Application.CanStreamedLevelBeLoaded(TutorialSceneBootstrap.SceneName))
            {
                LocalizedText.Bind(label, "ui.menu.tutorial");
                return;
            }

            LocalizedText.Bind(label, "ui.menu.no_tutorial");
            _tutorialButton.interactable = false;
            Debug.LogError(
                $"A-Math: scene '{TutorialSceneBootstrap.SceneName}' is missing from Build Settings.");
        }

        private void OpenTutorial()
        {
            SceneTransitionController.LoadScene(TutorialSceneBootstrap.SceneName, "ui.loading.tutorial");
        }

        private void OpenHelp()
        {
            _helpOverlay.Open();
        }

        private void OpenHistory()
        {
            _historyOverlay.Open();
        }

        private void CloseHelp()
        {
            UiFactory.Select(_helpButton);
        }

        private void OpenSettings()
        {
            // Keep the painted menu behind the settings fade so no camera-colour
            // frame appears, while removing its controls from pointer/navigation input.
            _menuInputGroup.interactable = false;
            _menuInputGroup.blocksRaycasts = false;
            _settingsMenu.Open();
        }

        private void CloseSettings()
        {
            _menuInputGroup.interactable = true;
            _menuInputGroup.blocksRaycasts = true;
            UiFactory.Select(_settingsButton);
            // Returning from settings should feel like the menu arriving again,
            // not a hard cut back onto a static list.
            if (_entrance != null)
            {
                PlayEntranceAnimation();
            }
        }

        private Button PlaceMenuButton(
            Transform parent,
            string name,
            float y,
            float width,
            float height,
            int fontSize,
            System.Action onClick,
            string localizationKey)
        {
            var button = _ui.CreateTextMenuButton(parent, name, string.Empty, fontSize, onClick);
            UiFactory.SetCenteredRect(button.GetComponent<RectTransform>(), new Vector2(0f, y), new Vector2(width, height));
            if (localizationKey != null)
                LocalizedText.Bind(button.GetComponentInChildren<Text>(), localizationKey);
            return button;
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            Debug.Log("A-Math: Quit was requested. Application.Quit() runs in a built player.");
#else
            Application.Quit();
#endif
        }

        private void BuildAccountFooter(Transform parent)
        {
            _logoutButton = _ui.CreateAccentButton(parent, "Logout Button",
                UiLocalizationProvider.Shared.GetText("ui.account.logout"), UiPalette.Quit,
                UiPalette.QuitHighlight, Logout, 20);
            UiFactory.SetAnchoredRect(_logoutButton.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero,
                Vector2.zero, new Vector2(88f, 88f), new Vector2(28f, 28f));

            Text account = _ui.CreateText("Signed In Account", parent,
                string.Format(UiLocalizationProvider.Shared.GetText("ui.account.signed_in"), AccountSession.Username),
                22, FontStyle.Normal, UiPalette.LightText, TextAnchor.LowerLeft);
            UiFactory.SetAnchoredRect(account.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(420f, 42f), new Vector2(132f, 30f));

            Text version = _ui.CreateText("Game Version", parent,
                string.Format(UiLocalizationProvider.Shared.GetText("ui.account.version"), Application.version),
                22, FontStyle.Normal, new Color(1f, 1f, 1f, 0.78f), TextAnchor.LowerRight);
            UiFactory.SetAnchoredRect(version.rectTransform, Vector2.one, Vector2.one, Vector2.one,
                new Vector2(260f, 40f), new Vector2(-24f, 18f));
        }

        private void OnAuthenticated()
        {
            // Recreate the scene so every save/history presenter is constructed
            // after the profile becomes active and can never retain another
            // account's directory in a readonly field.
            SceneTransitionController.LoadScene(SceneManager.GetActiveScene().buildIndex, "ui.loading.account");
        }

        private void ShowAuthenticatedMenu()
        {
            _accountGate.Close();
            _menuCanvas.SetActive(true);
        }

        private void ShowAccountGate(AccountError initialError)
        {
            _menuCanvas.SetActive(false);
            _accountGate.Open(initialError == AccountError.AutoLoginExpired ? AccountError.None : initialError);
        }

        private void Logout()
        {
            if (_logoutButton != null)
                _logoutButton.interactable = false;
            StartCoroutine(LogoutAndReload());
        }

        private IEnumerator LogoutAndReload()
        {
            GameSettings.ClearPlayerProfile();
            _accounts.Logout();

            // These objects survive scene loads and own save services bound to
            // the old profile. Tear them down before another account can enter.
            if (NetworkedGameContext.Instance != null)
                Destroy(NetworkedGameContext.Instance.gameObject);
            PlaySessionController playSession = FindFirstObjectByType<PlaySessionController>();
            if (playSession != null)
                Destroy(playSession.gameObject);

            yield return null;
            SceneTransitionController.LoadScene(SceneManager.GetActiveScene().buildIndex, "ui.loading.menu");
        }

        private void BuildSettingsMenu()
        {
            _settingsMenu = SettingsMenuController.Create(transform, _ui.Font);
            _settingsMenu.Closed += CloseSettings;
        }

        private void BuildHelpOverlay(Transform canvasTransform)
        {
            _helpOverlay = new HowToPlayOverlay(_ui, canvasTransform);
            _helpOverlay.Closed += CloseHelp;
        }

        /// <summary>
        /// The title and the menu entries fade up in reading order, so entering
        /// the screen reads as the menu arriving rather than a hard cut.
        /// </summary>
        private void BuildEntranceAnimation(Text title)
        {
            _entrance = gameObject.AddComponent<MenuEntranceAnimator>();
            _entrance.SetTargets(
                title,
                _startButton,
                _historyButton,
                _tutorialButton,
                _helpButton,
                _settingsButton,
                _quitButton);
            _entrance.Configure(0.22f, 0.025f, 0f);
        }

        private void PlayEntranceAnimation()
        {
            if (_entrance != null)
                _entrance.Play();
        }

        private void BuildHistoryOverlay(Transform canvasTransform)
        {
            _historyOverlay = new MatchHistoryOverlay(_ui, canvasTransform, UiLocalizationProvider.Shared);
            _historyOverlay.Closed += () => UiFactory.Select(_historyButton);
        }

        private void ConfigureMenuNavigation()
        {
            UiFactory.SetVerticalNavigation(_startButton, _quitButton, _historyButton);
            UiFactory.SetVerticalNavigation(_historyButton, _startButton, _tutorialButton);
            UiFactory.SetVerticalNavigation(_tutorialButton, _historyButton, _helpButton);
            UiFactory.SetVerticalNavigation(_helpButton, _tutorialButton, _settingsButton);
            UiFactory.SetVerticalNavigation(_settingsButton, _helpButton, _quitButton);
            UiFactory.SetVerticalNavigation(_quitButton, _settingsButton, _startButton);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var inputModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
            ConfigureInputModule(inputModule);
        }

        private static void ConfigureInputModule(InputSystemUIInputModule inputModule)
        {
            var actionAsset = InputSystem.actions;
            var uiMap = actionAsset == null ? null : actionAsset.FindActionMap("UI", false);

            if (uiMap != null && !HasRequiredActions(uiMap))
            {
                Debug.LogWarning(
                    "A-Math: the project-wide UI action map is missing one of " +
                    string.Join(", ", RequiredUiActions) +
                    ". Falling back to the built-in UI actions.");
                uiMap = null;
            }

            if (uiMap == null)
            {
                UseDefaultActions(inputModule);
                return;
            }

            // The template action map calls its actions Navigate and Click rather than the
            // module defaults Move and LeftClick, so connect every reference explicitly.
            inputModule.enabled = false;
            inputModule.actionsAsset = actionAsset;
            inputModule.move = CreateActionReference(uiMap, "Navigate");
            inputModule.submit = CreateActionReference(uiMap, "Submit");
            inputModule.cancel = CreateActionReference(uiMap, "Cancel");
            inputModule.point = CreateActionReference(uiMap, "Point");
            inputModule.leftClick = CreateActionReference(uiMap, "Click");
            inputModule.rightClick = CreateActionReference(uiMap, "RightClick");
            inputModule.middleClick = CreateActionReference(uiMap, "MiddleClick");
            inputModule.scrollWheel = CreateActionReference(uiMap, "ScrollWheel");
            inputModule.trackedDevicePosition = CreateActionReference(uiMap, "TrackedDevicePosition");
            inputModule.trackedDeviceOrientation = CreateActionReference(uiMap, "TrackedDeviceOrientation");
            inputModule.enabled = true;
        }

        private static InputActionReference CreateActionReference(InputActionMap map, string actionName)
        {
            var action = map.FindAction(actionName, false);
            return action == null ? null : InputActionReference.Create(action);
        }

        private static bool HasRequiredActions(InputActionMap map)
        {
            foreach (var actionName in RequiredUiActions)
            {
                if (map.FindAction(actionName, false) == null)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Leaves the module on the actions it ships with, so pointing, clicking and navigating
        /// keep working even in a project that has no usable UI action map of its own.
        /// </summary>
        private static void UseDefaultActions(InputSystemUIInputModule inputModule)
        {
            // Adding the module from code normally assigns those defaults during OnEnable already,
            // and assigning them twice would leak the first set.
            if (inputModule.actionsAsset == null)
            {
                inputModule.AssignDefaultActions();
            }
        }

        private bool WasCancelPressed()
        {
            return UiFactory.WasCancelPressed(_cancelAction);
        }
    }
}
