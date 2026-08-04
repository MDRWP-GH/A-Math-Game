using AMath.Core;
using AMath.Core.Assistance;
using AMath.Core.Events;
using AMath.Tutorial;
using AMath.Tutorial.Assistance;
using AMath.Tutorial.Bootstrap;
using AMath.Tutorial.Definitions;
using AMath.Tutorial.Interfaces;
using AMath.Tutorial.Localization;
using AMath.Tutorial.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AMath.UI.Tutorial
{
    /// <summary>
    /// Composition root for the standalone tutorial bootstrap scene. Builds
    /// the HUD at runtime, wires <see cref="TutorialManager"/>, and starts
    /// the intro sequence.
    /// </summary>
    public sealed class TutorialSceneController : MonoBehaviour
    {
        private static readonly string[] RequiredUiActions = { "Point", "Click", "Navigate", "Submit", "Cancel" };

        private ServiceRegistry _services;
        private TutorialHudPresenter _presenter;
        private TutorialManager _tutorialManager;
        private UiFactory _ui;

        private void Awake()
        {
            EnsureEventSystem();
            _ui = new UiFactory(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            _presenter = BuildHud();
            WireServices(_presenter);
        }

        private void Start()
        {
            IEventBus eventBus = _services.Resolve<IEventBus>();
            ILocalizedTextProvider textProvider = _services.Resolve<ILocalizedTextProvider>();

            eventBus.Publish(new BoardLoadedEvent());
            _tutorialManager.Start(new IntroTutorialSequence(textProvider), resumeProgress: true);
        }

        private void Update()
        {
            _services?.TickAll(Time.deltaTime);
            _presenter?.Tick();
        }

        private void OnDestroy()
        {
            _presenter?.Dispose();
            _services?.Dispose();
        }

        private void WireServices(TutorialHudPresenter presenter)
        {
            _services = new ServiceRegistry();

            IEventBus eventBus = _services.Register<IEventBus>(new EventBus());
            var readers = _services.Register(new TutorialAssistanceReaders());
            ILocalizedTextProvider textProvider = _services.Register<ILocalizedTextProvider>(
                new TutorialLocalizationProvider());
            ITutorialSaveStore saveStore = _services.Register<ITutorialSaveStore>(
                new TutorialProgressSaveStore());

            var runtimeContext = new TutorialRuntimeContext(
                eventBus,
                readers,
                readers,
                readers,
                presenter,
                presenter,
                presenter);

            _tutorialManager = _services.Register(
                new TutorialManager(eventBus, runtimeContext, textProvider, saveStore));

            presenter.Configure(_tutorialManager, eventBus);
        }

        private TutorialHudPresenter BuildHud()
        {
            var canvas = _ui.CreateCanvas(transform, "Tutorial Canvas", 100);

            var background = UiFactory.CreateImage("Background", canvas.transform, UiPalette.Background);
            UiFactory.Stretch(background.rectTransform);

            var title = _ui.CreateText(
                "Title",
                canvas.transform,
                "A-MATH Tutorial",
                56,
                FontStyle.Bold,
                UiPalette.LightText,
                TextAnchor.MiddleCenter);
            UiFactory.SetAnchoredRect(
                title.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(700f, 80f),
                new Vector2(0f, -48f));

            var boardPanel = UiFactory.CreateImage("Demo Board Panel", canvas.transform, UiPalette.Panel);
            UiFactory.SetCenteredRect(boardPanel.rectTransform, new Vector2(0f, 70f), new Vector2(620f, 420f));
            UiFactory.AddShadow(boardPanel.gameObject, UiPalette.Shadow, new Vector2(0f, -10f));

            var boardLabel = _ui.CreateText(
                "Board Label",
                boardPanel.transform,
                "พื้นที่กระดานตัวอย่าง",
                34,
                FontStyle.Bold,
                UiPalette.MutedText,
                TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(boardLabel.rectTransform, Vector2.zero, new Vector2(560f, 64f));

            var boardHighlight = UiFactory.CreateImage(
                "Demo Board Highlight",
                boardPanel.transform,
                new Color(0.98f, 0.84f, 0.18f, 0.35f));
            UiFactory.Stretch(boardHighlight.rectTransform);

            var hudPanel = UiFactory.CreateImage("Tutorial HUD", canvas.transform, UiPalette.Card);
            UiFactory.SetAnchoredRect(
                hudPanel.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(900f, 320f),
                new Vector2(0f, 170f));
            UiFactory.AddShadow(hudPanel.gameObject, UiPalette.Shadow, new Vector2(0f, -8f));

            var objectiveText = _ui.CreateText(
                "Objective",
                hudPanel.transform,
                string.Empty,
                28,
                FontStyle.Bold,
                UiPalette.LightText,
                TextAnchor.UpperLeft);
            UiFactory.SetAnchoredRect(
                objectiveText.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(-48f, 56f),
                new Vector2(24f, -20f));

            var progressText = _ui.CreateText(
                "Progress",
                hudPanel.transform,
                "0/0",
                22,
                FontStyle.Bold,
                UiPalette.MutedText,
                TextAnchor.UpperRight);
            UiFactory.SetAnchoredRect(
                progressText.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(120f, 40f),
                new Vector2(-24f, -24f));

            var dialoguePanel = UiFactory.CreateImage(
                "Dialogue Panel",
                hudPanel.transform,
                UiPalette.PanelTranslucent);
            UiFactory.SetCenteredRect(dialoguePanel.rectTransform, new Vector2(0f, 24f), new Vector2(820f, 120f));

            var dialogueText = _ui.CreateText(
                "Dialogue",
                dialoguePanel.transform,
                string.Empty,
                24,
                FontStyle.Normal,
                UiPalette.LightText,
                TextAnchor.MiddleLeft);
            UiFactory.SetAnchoredRect(
                dialogueText.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-32f, -16f),
                Vector2.zero);
            dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            dialogueText.verticalOverflow = VerticalWrapMode.Overflow;
            dialoguePanel.gameObject.SetActive(false);

            var hintText = _ui.CreateText(
                "Hint",
                hudPanel.transform,
                string.Empty,
                20,
                FontStyle.Italic,
                UiPalette.HintText,
                TextAnchor.UpperLeft);
            UiFactory.SetAnchoredRect(
                hintText.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 0f),
                new Vector2(-48f, 48f),
                new Vector2(24f, 92f));

            var continueButton = _ui.CreateButton(
                hudPanel.transform,
                "Continue Button",
                "ดำเนินการต่อ",
                UiPalette.Primary,
                UiPalette.PrimaryHighlight,
                () => { });
            UiFactory.SetCenteredRect(continueButton.GetComponent<RectTransform>(), new Vector2(-170f, -112f), new Vector2(240f, 68f));

            var replayButton = _ui.CreateButton(
                hudPanel.transform,
                "Replay Step Button",
                "เล่นขั้นนี้ใหม่",
                UiPalette.Secondary,
                UiPalette.SecondaryHighlight,
                () => { });
            UiFactory.SetCenteredRect(replayButton.GetComponent<RectTransform>(), new Vector2(90f, -112f), new Vector2(240f, 68f));

            var skipButton = _ui.CreateButton(
                hudPanel.transform,
                "Skip Tutorial Button",
                "ข้ามบทฝึก",
                UiPalette.Quit,
                UiPalette.QuitHighlight,
                () => { });
            UiFactory.SetCenteredRect(skipButton.GetComponent<RectTransform>(), new Vector2(350f, -112f), new Vector2(220f, 68f));

            var presenter = new TutorialHudPresenter(
                hudPanel.gameObject,
                objectiveText,
                progressText,
                hintText,
                dialoguePanel.gameObject,
                dialogueText,
                continueButton,
                skipButton,
                replayButton);

            presenter.RegisterHighlight(IntroTutorialSequence.DemoBoardTargetId, boardHighlight.gameObject);
            hudPanel.gameObject.SetActive(false);
            return presenter;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            ConfigureInputModule(eventSystemObject.GetComponent<InputSystemUIInputModule>());
        }

        private static void ConfigureInputModule(InputSystemUIInputModule inputModule)
        {
            var actionAsset = InputSystem.actions;
            var uiMap = actionAsset == null ? null : actionAsset.FindActionMap("UI", false);

            if (uiMap != null && !HasRequiredActions(uiMap))
            {
                Debug.LogWarning(
                    "A-Math Tutorial: UI action map is incomplete; using built-in UI actions.");
                uiMap = null;
            }

            if (uiMap == null)
            {
                if (inputModule.actionsAsset == null)
                    inputModule.AssignDefaultActions();
                return;
            }

            inputModule.enabled = false;
            inputModule.actionsAsset = actionAsset;
            inputModule.move = CreateActionReference(uiMap, "Navigate");
            inputModule.submit = CreateActionReference(uiMap, "Submit");
            inputModule.cancel = CreateActionReference(uiMap, "Cancel");
            inputModule.point = CreateActionReference(uiMap, "Point");
            inputModule.leftClick = CreateActionReference(uiMap, "Click");
            inputModule.scrollWheel = CreateActionReference(uiMap, "ScrollWheel");
            inputModule.enabled = true;
        }

        private static InputActionReference CreateActionReference(InputActionMap map, string actionName)
        {
            InputAction action = map.FindAction(actionName, false);
            return action == null ? null : InputActionReference.Create(action);
        }

        private static bool HasRequiredActions(InputActionMap uiMap)
        {
            foreach (string actionName in RequiredUiActions)
            {
                if (uiMap.FindAction(actionName, false) == null)
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Spawns <see cref="TutorialSceneController"/> when the tutorial scene loads.
    /// </summary>
    public static class TutorialSceneBootstrap
    {
        public const string SceneName = "TutorialScene";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateTutorialBootstrapForScene()
        {
            if (SceneManager.GetActiveScene().name != SceneName ||
                Object.FindFirstObjectByType<TutorialSceneController>() != null)
            {
                return;
            }

            var root = new GameObject("Tutorial Bootstrap");
            root.AddComponent<TutorialSceneController>();
        }
    }
}
