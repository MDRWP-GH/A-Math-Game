using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>
    /// Builds and owns the first A-Math menu at runtime. Keeping it code-driven makes the
    /// starter scene safe to reuse while the game screens are still being developed.
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
        private GameObject _helpOverlay;
        private Text _statusLabel;
        private Button _startButton;
        private Button _helpButton;
        private Button _settingsButton;
        private Button _quitButton;
        private Button _closeHelpButton;
        private SettingsMenuController _settingsMenu;

        private void Awake()
        {
            EnsureEventSystem();
            _ui = new UiFactory(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            BuildMenu();
        }

        private void Start()
        {
            UiFactory.Select(_startButton);
        }

        private void BuildMenu()
        {
            var canvas = _ui.CreateCanvas(transform, "Canvas", 100);
            _menuCanvas = canvas.gameObject;

            var background = UiFactory.CreateImage("Background", canvas.transform, UiPalette.Background);
            UiFactory.Stretch(background.rectTransform);

            CreateDecorativeSymbol(canvas.transform, "+", new Vector2(0.12f, 0.79f), 132f, -10f);
            CreateDecorativeSymbol(canvas.transform, "÷", new Vector2(0.84f, 0.81f), 110f, 8f);
            CreateDecorativeSymbol(canvas.transform, "×", new Vector2(0.12f, 0.20f), 128f, 14f);
            CreateDecorativeSymbol(canvas.transform, "=", new Vector2(0.87f, 0.17f), 110f, -10f);

            var menuPanel = UiFactory.CreateImage("Menu Panel", canvas.transform, UiPalette.Panel);
            UiFactory.SetCenteredRect(menuPanel.rectTransform, Vector2.zero, new Vector2(720f, 770f));
            UiFactory.AddShadow(menuPanel.gameObject, new Color(0f, 0f, 0f, 0.32f), new Vector2(0f, -14f));

            var accentLine = UiFactory.CreateImage("Accent Line", menuPanel.transform, UiPalette.Primary);
            UiFactory.SetCenteredRect(accentLine.rectTransform, new Vector2(0f, 205f), new Vector2(150f, 8f));

            var title = _ui.CreateText("Title", menuPanel.transform, "A-MATH", 94, FontStyle.Bold, UiPalette.LightText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(title.rectTransform, new Vector2(0f, 265f), new Vector2(650f, 120f));
            UiFactory.AddShadow(title.gameObject, UiPalette.Shadow, new Vector2(0f, -4f));

            var subtitle = _ui.CreateText("Subtitle", menuPanel.transform, "สนุกกับการคิดเลข ทุกวัน", 30, FontStyle.Normal, UiPalette.MutedText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(subtitle.rectTransform, new Vector2(0f, 150f), new Vector2(620f, 64f));

            var prompt = _ui.CreateText("Prompt", menuPanel.transform, "พร้อมเริ่มฝึกแล้วหรือยัง?", 25, FontStyle.Bold, UiPalette.LightText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(prompt.rectTransform, new Vector2(0f, 82f), new Vector2(600f, 54f));

            _startButton = _ui.CreateButton(menuPanel.transform, "Start Button", "เริ่มเกม", UiPalette.Primary, UiPalette.PrimaryHighlight, StartGame);
            UiFactory.SetCenteredRect(_startButton.GetComponent<RectTransform>(), new Vector2(0f, 16f), new Vector2(520f, 88f));

            _helpButton = _ui.CreateButton(menuPanel.transform, "How To Play Button", "วิธีเล่น", UiPalette.Secondary, UiPalette.SecondaryHighlight, OpenHelp);
            UiFactory.SetCenteredRect(_helpButton.GetComponent<RectTransform>(), new Vector2(0f, -82f), new Vector2(520f, 78f));

            _settingsButton = _ui.CreateButton(menuPanel.transform, "Settings Button", "ตั้งค่า", UiPalette.Secondary, UiPalette.SecondaryHighlight, OpenSettings);
            UiFactory.SetCenteredRect(_settingsButton.GetComponent<RectTransform>(), new Vector2(0f, -174f), new Vector2(520f, 78f));

            _quitButton = _ui.CreateButton(menuPanel.transform, "Quit Button", "ออกจากเกม", UiPalette.Quit, UiPalette.QuitHighlight, QuitGame);
            UiFactory.SetCenteredRect(_quitButton.GetComponent<RectTransform>(), new Vector2(0f, -262f), new Vector2(520f, 68f));

            _statusLabel = _ui.CreateText("Status", menuPanel.transform, "เลือกเมนูเพื่อเริ่มต้น", 20, FontStyle.Normal, UiPalette.MutedText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_statusLabel.rectTransform, new Vector2(0f, -335f), new Vector2(610f, 50f));

            var footer = _ui.CreateText("Footer", canvas.transform, "A-MATH  •  LEARN  •  PLAY  •  GROW", 18, FontStyle.Bold, new Color(0.66f, 0.75f, 0.93f, 0.75f), TextAnchor.MiddleCenter);
            UiFactory.SetAnchoredRect(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(720f, 42f), new Vector2(0f, 35f));

            ConfigureMenuNavigation();
            BuildHelpOverlay(canvas.transform);
            BuildSettingsMenu();
        }

        private void StartGame()
        {
            // There is no gameplay scene in the starter project yet. Keep the menu honest
            // instead of loading a guessed scene name or index.
            _statusLabel.text = "ด่านแรกกำลังอยู่ระหว่างเตรียม...";
        }

        private void OpenHelp()
        {
            _helpOverlay.SetActive(true);
            UiFactory.Select(_closeHelpButton);
        }

        private void CloseHelp()
        {
            _helpOverlay.SetActive(false);
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
            _helpOverlay = UiFactory.CreateImage("How To Play Overlay", canvasTransform, UiPalette.Overlay).gameObject;
            UiFactory.Stretch(_helpOverlay.GetComponent<RectTransform>());

            var card = UiFactory.CreateImage("How To Play Card", _helpOverlay.transform, UiPalette.Card);
            UiFactory.SetCenteredRect(card.rectTransform, Vector2.zero, new Vector2(680f, 560f));
            UiFactory.AddShadow(card.gameObject, new Color(0f, 0f, 0f, 0.38f), new Vector2(0f, -12f));

            var heading = _ui.CreateText("Heading", card.transform, "วิธีเล่น", 52, FontStyle.Bold, UiPalette.LightText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(heading.rectTransform, new Vector2(0f, 177f), new Vector2(560f, 78f));

            var body = _ui.CreateText(
                "Body",
                card.transform,
                "เมนูนี้รองรับเมาส์ สัมผัส คีย์บอร์ด และจอย\n\nกด “เริ่มเกม” เพื่อเข้าสู่ด่านเมื่อฉากเกมพร้อม\n\nระหว่างนี้ คุณสามารถกลับมาที่เมนูนี้ได้ทุกเมื่อ",
                25,
                FontStyle.Normal,
                UiPalette.MutedText,
                TextAnchor.MiddleCenter);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.SetCenteredRect(body.rectTransform, new Vector2(0f, 6f), new Vector2(560f, 245f));

            _closeHelpButton = _ui.CreateButton(card.transform, "Back Button", "กลับ", UiPalette.Primary, UiPalette.PrimaryHighlight, CloseHelp);
            UiFactory.SetCenteredRect(_closeHelpButton.GetComponent<RectTransform>(), new Vector2(0f, -198f), new Vector2(310f, 76f));

            _helpOverlay.SetActive(false);
        }

        private void ConfigureMenuNavigation()
        {
            UiFactory.SetVerticalNavigation(_startButton, _quitButton, _helpButton);
            UiFactory.SetVerticalNavigation(_helpButton, _startButton, _settingsButton);
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

        private void CreateDecorativeSymbol(Transform parent, string symbol, Vector2 anchor, float size, float rotation)
        {
            var text = _ui.CreateText("Decoration " + symbol, parent, symbol, Mathf.RoundToInt(size), FontStyle.Bold, new Color(0.28f, 0.42f, 0.76f, 0.23f), TextAnchor.MiddleCenter);
            UiFactory.SetAnchoredRect(text.rectTransform, anchor, anchor, new Vector2(0.5f, 0.5f), new Vector2(size, size), Vector2.zero);
            text.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotation);
        }
    }
}
