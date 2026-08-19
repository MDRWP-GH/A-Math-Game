using AMath.Art;
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
    /// Layout matches the main-menu mockup: background art, title, and four text buttons.
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
        private HowToPlayOverlay _helpOverlay;
        private Button _startButton;
        private Button _tutorialButton;
        private Button _helpButton;
        private Button _settingsButton;
        private Button _quitButton;
        private SettingsMenuController _settingsMenu;
        private InputAction _cancelAction;

        private void Awake()
        {
            EnsureEventSystem();
            _ui = new UiFactory(GameFonts.Jersey25);
            BuildMenu();
            _cancelAction = UiFactory.FindCancelAction();
        }

        private void Start()
        {
            UiFactory.Select(_startButton);
        }

        private void OnEnable()
        {
            // The play session hides this object and re-activates it on return;
            // restore keyboard/gamepad focus.
            if (_startButton != null)
            {
                UiFactory.Select(_startButton);
            }
        }

        private void Update()
        {
            if (_helpOverlay != null && _helpOverlay.IsOpen && WasCancelPressed())
            {
                _helpOverlay.Close();
            }
        }

        private void BuildMenu()
        {
            var canvas = _ui.CreateCanvas(transform, "Canvas", 100);
            _menuCanvas = canvas.gameObject;

            UiFactory.CreateFullScreenBackground(
                canvas.transform,
                "Main Menu Backgrounds",
                UiPalette.Background);

            var title = _ui.CreateText(
                "Title",
                canvas.transform,
                string.Empty,
                78,
                FontStyle.Normal,
                Color.white,
                TextAnchor.MiddleCenter);
            title.font = GameFonts.JainiPurva;
            UiFactory.SetAnchoredRect(
                title.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(980f, 110f),
                new Vector2(0f, -72f));
            UiFactory.AddDoubleOutline(title.gameObject, new Vector2(4f, -4f), new Vector2(2f, -2f));
            LocalizedText.Bind(title, "ui.menu.title");

            const float buttonWidth = 520f;
            const float buttonHeight = 88f;
            const int buttonFontSize = 52;

            _startButton = _ui.CreateTextMenuButton(
                canvas.transform,
                "Start Button",
                string.Empty,
                buttonFontSize,
                StartGame);
            UiFactory.SetCenteredRect(
                _startButton.GetComponent<RectTransform>(),
                new Vector2(0f, 158f),
                new Vector2(buttonWidth, buttonHeight));
            LocalizedText.Bind(_startButton.GetComponentInChildren<Text>(), "ui.menu.start");

            _tutorialButton = _ui.CreateTextMenuButton(
                canvas.transform,
                "Tutorial Button",
                string.Empty,
                buttonFontSize,
                OpenTutorial);
            UiFactory.SetCenteredRect(
                _tutorialButton.GetComponent<RectTransform>(),
                new Vector2(0f, 62f),
                new Vector2(buttonWidth, buttonHeight));
            ConfigureTutorialButton();

            _helpButton = _ui.CreateTextMenuButton(
                canvas.transform,
                "How To Play Button",
                string.Empty,
                buttonFontSize,
                OpenHelp);
            UiFactory.SetCenteredRect(
                _helpButton.GetComponent<RectTransform>(),
                new Vector2(0f, -34f),
                new Vector2(buttonWidth, buttonHeight));
            LocalizedText.Bind(_helpButton.GetComponentInChildren<Text>(), "ui.menu.help");

            _settingsButton = _ui.CreateTextMenuButton(
                canvas.transform,
                "Settings Button",
                string.Empty,
                buttonFontSize,
                OpenSettings);
            UiFactory.SetCenteredRect(
                _settingsButton.GetComponent<RectTransform>(),
                new Vector2(0f, -130f),
                new Vector2(buttonWidth, buttonHeight));
            LocalizedText.Bind(_settingsButton.GetComponentInChildren<Text>(), "ui.menu.settings");

            _quitButton = _ui.CreateTextMenuButton(
                canvas.transform,
                "Exit Button",
                string.Empty,
                buttonFontSize,
                QuitGame);
            UiFactory.SetCenteredRect(
                _quitButton.GetComponent<RectTransform>(),
                new Vector2(0f, -226f),
                new Vector2(buttonWidth, buttonHeight));
            LocalizedText.Bind(_quitButton.GetComponentInChildren<Text>(), "ui.menu.quit");

            ConfigureMenuNavigation();
            BuildHelpOverlay(canvas.transform);
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
            SceneManager.LoadScene(TutorialSceneBootstrap.SceneName);
        }

        private void OpenHelp()
        {
            _helpOverlay.Open();
        }

        private void CloseHelp()
        {
            UiFactory.Select(_helpButton);
        }

        private void OpenSettings()
        {
            // Hiding the menu keeps its buttons out of reach of keyboard and gamepad navigation
            // while the settings screen is on top.
            _menuCanvas.SetActive(false);
            _settingsMenu.Open();
        }

        private void CloseSettings()
        {
            _menuCanvas.SetActive(true);
            UiFactory.Select(_settingsButton);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            Debug.Log("A-Math: Quit was requested. Application.Quit() runs in a built player.");
#else
            Application.Quit();
#endif
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

        private void ConfigureMenuNavigation()
        {
            UiFactory.SetVerticalNavigation(_startButton, _quitButton, _tutorialButton);
            UiFactory.SetVerticalNavigation(_tutorialButton, _startButton, _helpButton);
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
