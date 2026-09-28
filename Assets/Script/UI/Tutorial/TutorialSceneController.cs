using System;
using System.Collections.Generic;
using AMath.Art;
using AMath.Core;
using AMath.Core.Assistance;
using AMath.Core.Events;
using AMath.Tutorial;
using AMath.Tutorial.Bootstrap;
using AMath.Tutorial.Definitions;
using AMath.Tutorial.Interfaces;
using AMath.Tutorial.Localization;
using AMath.Tutorial.Save;
using AMath.Tutorial.Scripted;
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
        private TutorialMatchHost _matchHost;
        private TutorialMatchView _matchView;
        private UiFactory _ui;
        private TutorialLocalizationProvider _textProvider;
        private IEventBus _eventBus;
        private GameObject _chapterPicker;
        private GameObject _gameplayRoot;
        private Button _chapterBackButton;
        private MenuEntranceAnimator _chapterEntrance;
        private Text _chapterProgressText;
        private Text _chapterNoticeText;
        private readonly List<ChapterButtonBinding> _chapterCards = new();
        private ITutorialSaveStore _saveStore;
        private Canvas _canvas;
        private OverlayShell _confirmationShell;
        private Text _confirmationTitle;
        private Text _confirmationBody;
        private Button _confirmationConfirm;
        private Button _confirmationCancel;
        private Action _pendingConfirmation;
        private Action _pendingCancellation;
        private OverlayShell _completionShell;
        private Text _completionChapter;
        private Text _completionOutcomes;
        private Button _completionPrimary;
        private Button _completionReplay;
        private Button _completionPicker;
        private string _activeTutorialId;
        private TutorialScreenState _screenState;

        private enum TutorialScreenState
        {
            ChapterSelect,
            Playing,
            ExitConfirmation,
            CompletionSummary
        }

        private sealed class ChapterButtonBinding
        {
            public Image Card;
            public Text Heading;
            public Button Button;
            public Text Description;
            public Text Status;
            public Color Accent;
            public Color AccentHighlight;
            public Button RestartButton;
            public TutorialChapterDescriptor Descriptor;
        }

        private void Awake()
        {
            EnsureEventSystem();
            _textProvider = new TutorialLocalizationProvider();
            _ui = new UiFactory(GameFonts.Jersey25);
            WireCoreServices();
            _presenter = BuildHud();
            BuildOverlays();
            WireTutorialManager(_presenter);
            SceneTransitionController.DestinationRevealStarted += OnDestinationRevealStarted;
        }

        private void Start()
        {
            _eventBus.Publish(new BoardLoadedEvent());
            ShowChapterPicker();
            if (!SceneTransitionController.IsTransitioning)
                SceneTransitionController.NotifyStartupReady();
        }

        private void OnDestroy()
        {
            SceneTransitionController.DestinationRevealStarted -= OnDestinationRevealStarted;
            if (_eventBus != null)
                _eventBus.Unsubscribe<TutorialStepChangedEvent>(OnTutorialStepChanged);

            _matchView?.Dispose();
            _presenter?.Dispose();
            _services?.Dispose();
        }

        private void WireCoreServices()
        {
            _services = new ServiceRegistry();
            _eventBus = _services.Register<IEventBus>(new EventBus());
            ILocalizedTextProvider textProvider = _services.Register<ILocalizedTextProvider>(_textProvider);
            _matchHost = _services.Register(
                new TutorialMatchHost(_eventBus, textProvider, ScriptedTutorialMatchScript.Intro()));
            _services.Register(_matchHost.Game);
        }

        private void WireTutorialManager(TutorialHudPresenter presenter)
        {
            ILocalizedTextProvider textProvider = _services.Resolve<ILocalizedTextProvider>();
            ITutorialSaveStore saveStore = _services.Register<ITutorialSaveStore>(
                new TutorialProgressSaveStore());

            var runtimeContext = new TutorialRuntimeContext(
                _eventBus,
                _matchHost.Readers,
                _matchHost.Readers,
                _matchHost.Readers,
                presenter,
                presenter,
                presenter);

            _saveStore = saveStore;
            _tutorialManager = _services.Register(
                new TutorialManager(_eventBus, runtimeContext, textProvider, saveStore));

            presenter.Configure(_tutorialManager, _eventBus);
            _eventBus.Subscribe<TutorialStepChangedEvent>(OnTutorialStepChanged);
        }

        private void Update()
        {
            _services?.TickAll(Time.deltaTime);
            _presenter?.Tick(Time.deltaTime);
        }

        private void OnDestinationRevealStarted()
        {
            if (isActiveAndEnabled && _chapterPicker != null && _chapterPicker.activeInHierarchy)
                _chapterEntrance?.Play();
        }

        private void OnTutorialStepChanged(TutorialStepChangedEvent evt)
        {
            if (evt.IsActive)
            {
                HideChapterPicker();
                _screenState = TutorialScreenState.Playing;
                return;
            }

            if (_tutorialManager.Phase == AMath.Tutorial.StateMachine.TutorialPhase.Completed)
            {
                ShowCompletionSummary(_activeTutorialId);
                return;
            }

            ShowChapterPicker("tutorial.ui.skipped");
        }

        private void StartChapter(string tutorialId, bool resumeProgress = true)
        {
            TutorialChapterDescriptor chapter = TutorialChapterCatalog.Find(tutorialId);
            HideChapterPicker();
            _matchView?.ResetForChapter();
            ILocalizedTextProvider textProvider = _services.Resolve<ILocalizedTextProvider>();
            ITutorialSequenceDefinition sequence = chapter.CreateSequence(textProvider);

            _activeTutorialId = chapter.Id;
            _screenState = TutorialScreenState.Playing;
            _gameplayRoot?.SetActive(true);
            CloseCompletion();
            CloseConfirmation();
            _matchHost.SetScript(chapter.CreateScript());
            _tutorialManager.Start(sequence, resumeProgress);
        }

        private void ShowChapterPicker(string noticeKey = null)
        {
            _matchView?.HideTilePreview();
            if (_chapterPicker == null)
            {
                BuildChapterPicker();
            }

            RefreshChapterPicker();
            _screenState = TutorialScreenState.ChapterSelect;
            _gameplayRoot?.SetActive(false);
            CloseCompletion();
            CloseConfirmation();
            _chapterPicker.SetActive(true);
            _chapterEntrance?.Play();
            if (_chapterNoticeText != null)
                _chapterNoticeText.text = string.IsNullOrWhiteSpace(noticeKey)
                    ? string.Empty
                    : _textProvider.GetText(noticeKey);

            var first = new List<Selectable>();
            for (int i = 0; i < _chapterCards.Count; i++)
                first.Add(_chapterCards[i].Button);
            first.Add(_chapterBackButton);
            UiFactory.SelectFirstInteractable(first.ToArray());
        }

        private void BuildChapterPicker()
        {
            _chapterPicker = UiFactory.CreateRect("Chapter Picker", _canvas.transform).gameObject;
            UiFactory.Stretch(_chapterPicker.GetComponent<RectTransform>());
            UiFactory.CreateFullScreenBackground(
                _chapterPicker.transform,
                "Main Menu Backgrounds",
                UiPalette.Background);

            var title = _ui.CreateOutlinedTitle(
                _chapterPicker.transform,
                "Title",
                _textProvider.GetText("tutorial.ui.title"),
                68);
            title.font = GameFonts.JainiPurva;
            UiFactory.SetAnchoredRect(
                title.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(1100f, 88f),
                new Vector2(0f, -54f));

            var subtitle = _ui.CreateText(
                "Subtitle",
                _chapterPicker.transform,
                $"{_textProvider.GetText("tutorial.chapters.title")} — {_textProvider.GetText("tutorial.chapters.subtitle")}",
                27,
                FontStyle.Normal,
                UiPalette.LightText,
                TextAnchor.MiddleCenter);
            UiFactory.SetAnchoredRect(
                subtitle.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(1500f, 48f),
                new Vector2(0f, -118f));

            _chapterProgressText = _ui.CreateText(
                "Overall Progress",
                _chapterPicker.transform,
                string.Empty,
                24,
                FontStyle.Bold,
                UiPalette.CellGuide,
                TextAnchor.MiddleCenter);
            UiFactory.SetAnchoredRect(
                _chapterProgressText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(700f, 36f),
                new Vector2(0f, -158f));

            var panel = UiFactory.CreateImage("Panel", _chapterPicker.transform, Color.clear);
            panel.raycastTarget = false;
            UiFactory.SetCenteredRect(
                panel.rectTransform,
                new Vector2(0f, -30f),
                new Vector2(1700f, 690f));

            _chapterCards.Clear();
            IReadOnlyList<TutorialChapterDescriptor> chapters = TutorialChapterCatalog.All;
            for (int i = 0; i < chapters.Count; i++)
            {
                float x = (i - (chapters.Count - 1) * 0.5f) * 535f;
                _chapterCards.Add(CreateChapterCard(panel.transform, chapters[i], i + 1, new Vector2(x, 20f)));
            }

            _chapterBackButton = _ui.CreateAccentButton(
                panel.transform,
                "Back",
                _textProvider.GetText("tutorial.ui.back_menu"),
                UiPalette.Danger,
                UiPalette.DangerHighlight,
                OnBackPressed,
                34);
            UiFactory.SetCenteredRect(
                _chapterBackButton.GetComponent<RectTransform>(),
                new Vector2(0f, -290f),
                new Vector2(320f, 58f));

            _chapterNoticeText = _ui.CreateText(
                "Chapter Notice",
                panel.transform,
                string.Empty,
                21,
                FontStyle.Italic,
                UiPalette.HintText,
                TextAnchor.MiddleCenter);
            _chapterNoticeText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.SetCenteredRect(
                _chapterNoticeText.rectTransform,
                new Vector2(0f, -240f),
                new Vector2(1080f, 40f));

            _chapterEntrance = _chapterPicker.AddComponent<MenuEntranceAnimator>();
            _chapterEntrance.SetTargets(
                title,
                subtitle,
                _chapterCards[0].Card,
                _chapterCards[1].Card,
                _chapterCards[2].Card,
                _chapterBackButton);
            _chapterEntrance.Configure(0.22f, 0.03f, 0f);
        }

        private void RefreshChapterPicker()
        {
            int completedCount = 0;
            for (int i = 0; i < _chapterCards.Count; i++)
            {
                ChapterButtonBinding binding = _chapterCards[i];
                TutorialChapterDescriptor chapter = binding.Descriptor;
                TutorialChapterState chapterState = chapter.ResolveState(_saveStore, out TutorialProgressData progress);
                bool unlocked = chapterState != TutorialChapterState.Locked;
                bool hasProgress = chapterState == TutorialChapterState.InProgress
                    || chapterState == TutorialChapterState.Completed;
                bool completed = chapterState == TutorialChapterState.Completed;
                if (completed)
                    completedCount++;

                ITutorialSequenceDefinition sequence = chapter.CreateSequence(_textProvider);
                string status;
                if (!unlocked)
                {
                    status = _textProvider.GetText("tutorial.chapters.status_locked");
                }
                else if (completed)
                {
                    status = _textProvider.GetText("tutorial.chapters.status_completed");
                }
                else if (hasProgress)
                {
                    int stepIndex = Mathf.Clamp(progress.StepIndex, 0, sequence.Steps.Count - 1);
                    int milestone = Mathf.Clamp(sequence.Steps[stepIndex].MilestoneIndex, 0, sequence.MilestoneTitleKeys.Count - 1);
                    status = string.Format(
                        _textProvider.GetText("tutorial.chapters.status_progress"),
                        milestone + 1,
                        sequence.MilestoneTitleKeys.Count);
                }
                else
                {
                    status = _textProvider.GetText("tutorial.chapters.status_new");
                }

                string actionLabel = !unlocked
                    ? _textProvider.GetText("tutorial.chapters.action_locked")
                    : completed
                        ? _textProvider.GetText("tutorial.chapters.action_replay")
                        : hasProgress
                            ? _textProvider.GetText("tutorial.chapters.action_continue")
                            : _textProvider.GetText("tutorial.chapters.action_start");
                string description = _textProvider.GetText(
                    unlocked ? chapter.DescriptionKey : chapter.LockedDescriptionKey);

                ConfigureChapterButton(binding, unlocked, description, status, actionLabel, () =>
                    StartChapter(chapter.Id, resumeProgress: !completed));

                bool showRestart = unlocked && hasProgress && !completed;
                binding.RestartButton.gameObject.SetActive(showRestart);
                binding.RestartButton.onClick.RemoveAllListeners();
                if (showRestart)
                    binding.RestartButton.onClick.AddListener(() => ShowRestartConfirmation(chapter.Id));
            }

            if (_chapterProgressText != null)
            {
                _chapterProgressText.text = string.Format(
                    _textProvider.GetText("tutorial.chapters.progress"),
                    completedCount);
            }

            ConfigureChapterNavigation();
        }

        private void HideChapterPicker()
        {
            if (_chapterPicker != null)
                _chapterPicker.SetActive(false);
        }

        private ChapterButtonBinding CreateChapterCard(
            Transform parent,
            TutorialChapterDescriptor chapter,
            int chapterNumber,
            Vector2 position)
        {
            string cardName = string.IsNullOrEmpty(chapter.Id)
                ? $"Chapter {chapterNumber} Card"
                : $"{char.ToUpperInvariant(chapter.Id[0])}{chapter.Id.Substring(1)} Card";
            Image card = UiFactory.CreateGlassPanel(parent, cardName, UiPalette.GlassStrong);
            UiFactory.SetCenteredRect(card.rectTransform, position, new Vector2(480f, 490f));

            Text number = _ui.CreateText(
                "Number", card.transform, chapterNumber.ToString("00"), 24,
                FontStyle.Bold, chapter.Accent, TextAnchor.MiddleLeft);
            UiFactory.SetCenteredRect(number.rectTransform, new Vector2(-185f, 200f), new Vector2(70f, 34f));

            Text heading = _ui.CreateText(
                "Heading",
                card.transform,
                _textProvider.GetText(chapter.CardTitleKey),
                32,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter);
            heading.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.SetCenteredRect(
                heading.rectTransform,
                new Vector2(0f, 146f),
                new Vector2(400f, 82f));

            Text status = _ui.CreateText(
                "Status", card.transform, string.Empty, 21, FontStyle.Bold,
                chapter.Accent, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(status.rectTransform, new Vector2(0f, 92f), new Vector2(400f, 34f));

            var desc = _ui.CreateText(
                "Description",
                card.transform,
                string.Empty,
                23,
                FontStyle.Normal,
                UiPalette.LightText,
                TextAnchor.UpperCenter);
            UiFactory.SetCenteredRect(
                desc.rectTransform,
                new Vector2(0f, 15f),
                new Vector2(400f, 118f));
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Overflow;

            Button button = _ui.CreateAccentButton(
                card.transform,
                "Action",
                string.Empty,
                chapter.Accent,
                chapter.AccentHighlight,
                () => { },
                27);
            UiFactory.SetCenteredRect(
                button.GetComponent<RectTransform>(),
                new Vector2(0f, -104f),
                new Vector2(360f, 68f));

            Button restart = _ui.CreateTextMenuButton(
                card.transform,
                "Restart",
                _textProvider.GetText("tutorial.chapters.action_restart"),
                24,
                () => { });
            UiFactory.SetCenteredRect(
                restart.GetComponent<RectTransform>(),
                new Vector2(0f, -174f),
                new Vector2(280f, 48f));
            restart.gameObject.SetActive(false);

            return new ChapterButtonBinding
            {
                Card = card,
                Heading = heading,
                Button = button,
                Description = desc,
                Status = status,
                Accent = chapter.Accent,
                AccentHighlight = chapter.AccentHighlight,
                RestartButton = restart,
                Descriptor = chapter
            };
        }

        private static void ConfigureChapterButton(
            ChapterButtonBinding binding,
            bool enabled,
            string description,
            string status,
            string actionLabel,
            System.Action onClick)
        {
            if (binding?.Button == null)
                return;

            binding.Button.onClick.RemoveAllListeners();
            if (enabled && onClick != null)
                binding.Button.onClick.AddListener(onClick.Invoke);

            Color normal = enabled ? binding.Accent : UiPalette.HintText;
            Color highlighted = enabled ? binding.AccentHighlight : UiPalette.HintText;
            ColorBlock colors = binding.Button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.pressedColor = Color.Lerp(normal, Color.black, 0.16f);
            colors.selectedColor = highlighted;
            binding.Button.colors = colors;
            binding.Button.interactable = enabled;

            if (binding.Description != null)
            {
                binding.Description.text = description;
                binding.Description.color = enabled ? UiPalette.LightText : UiPalette.MutedText;
            }
            if (binding.Heading != null)
                binding.Heading.color = enabled ? Color.white : UiPalette.MutedText;
            if (binding.Status != null)
            {
                binding.Status.text = status;
                binding.Status.color = enabled ? binding.Accent : UiPalette.MutedText;
            }
            if (binding.Card != null)
            {
                binding.Card.color = enabled
                    ? UiPalette.GlassStrong
                    : new Color(UiPalette.GlassStrong.r, UiPalette.GlassStrong.g, UiPalette.GlassStrong.b, 0.72f);
            }

            Text buttonLabel = binding.Button.GetComponentInChildren<Text>();
            if (buttonLabel != null)
                buttonLabel.text = actionLabel;
        }

        private void ConfigureChapterNavigation()
        {
            var available = new List<Button>();
            for (int i = 0; i < _chapterCards.Count; i++)
            {
                if (_chapterCards[i].Button.interactable)
                    available.Add(_chapterCards[i].Button);
            }

            for (int i = 0; i < available.Count; i++)
            {
                ChapterButtonBinding binding = _chapterCards.Find(card => card.Button == available[i]);
                Selectable down = binding.RestartButton.gameObject.activeSelf
                    ? binding.RestartButton
                    : _chapterBackButton;
                SetNavigation(
                    available[i],
                    null,
                    down,
                    i > 0 ? available[i - 1] : null,
                    i + 1 < available.Count ? available[i + 1] : null);
                if (binding.RestartButton.gameObject.activeSelf)
                    SetNavigation(binding.RestartButton, available[i], _chapterBackButton, null, null);
            }

            Button last = available.Count > 0 ? available[available.Count - 1] : null;
            SetNavigation(_chapterBackButton, last, null, available.Count > 0 ? available[0] : null, last);
        }

        private static void SetNavigation(
            Selectable selectable,
            Selectable up,
            Selectable down,
            Selectable left,
            Selectable right)
        {
            Navigation navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;
            navigation.selectOnLeft = left;
            navigation.selectOnRight = right;
            selectable.navigation = navigation;
        }

        private void OnBackPressed()
        {
            if (_screenState == TutorialScreenState.CompletionSummary)
            {
                ShowChapterPicker();
                return;
            }

            if (_screenState == TutorialScreenState.Playing
                && _tutorialManager.Phase == AMath.Tutorial.StateMachine.TutorialPhase.Running)
            {
                ShowExitConfirmation();
                return;
            }

            if (_screenState == TutorialScreenState.ExitConfirmation)
            {
                CancelExitConfirmation();
                return;
            }

            ReturnToMainMenu();
        }

        private void ShowExitConfirmation()
        {
            _tutorialManager.Pause();
            _screenState = TutorialScreenState.ExitConfirmation;
            ShowConfirmation(
                "tutorial.ui.exit_title",
                "tutorial.ui.exit_body",
                "tutorial.ui.exit_confirm",
                "tutorial.ui.cancel",
                () =>
                {
                    CloseConfirmation();
                    _tutorialManager.Skip();
                },
                CancelExitConfirmation);
        }

        private void CancelExitConfirmation()
        {
            CloseConfirmation();
            _screenState = TutorialScreenState.Playing;
            _tutorialManager.Resume();
        }

        private void ShowRestartConfirmation(string tutorialId)
        {
            ShowConfirmation(
                "tutorial.ui.restart_title",
                "tutorial.ui.restart_body",
                "tutorial.ui.restart_confirm",
                "tutorial.ui.cancel_action",
                () =>
                {
                    CloseConfirmation();
                    StartChapter(tutorialId, resumeProgress: false);
                },
                CloseConfirmation);
        }

        private void ReturnToMainMenu()
        {
            SceneTransitionController.LoadScene(0, "ui.loading.menu");
        }

        private TutorialHudPresenter BuildHud()
        {
            var canvas = _ui.CreateCanvas(transform, "Tutorial Canvas", 100);
            _canvas = canvas;

            var background = UiFactory.CreateImage("Background", canvas.transform, UiPalette.Background);
            UiFactory.Stretch(background.rectTransform);

            _gameplayRoot = UiFactory.CreateRect("Tutorial Gameplay", canvas.transform).gameObject;
            UiFactory.Stretch(_gameplayRoot.GetComponent<RectTransform>());

            _matchView = new TutorialMatchView(
                _ui,
                _textProvider,
                _matchHost,
                _gameplayRoot.transform,
                message => _presenter?.ShowCorrection(message),
                OnBackPressed);

            var spotlightObject = new GameObject("Tutorial Spotlight", typeof(RectTransform), typeof(TutorialSpotlightOverlay));
            spotlightObject.transform.SetParent(_gameplayRoot.transform, false);
            var spotlight = spotlightObject.GetComponent<TutorialSpotlightOverlay>();
            spotlight.Initialize(canvas);

            var hudPanel = UiFactory.CreateImage("Tutorial Coach", _gameplayRoot.transform, UiPalette.Card);
            UiFactory.SetCenteredRect(hudPanel.rectTransform,
                new Vector2(610f, 0f), MatchHudLayout.TutorialCoachSize);
            UiFactory.AddShadow(hudPanel.gameObject, UiPalette.Shadow, new Vector2(0f, -8f));

            Text milestoneText = _ui.CreateText(
                "Milestone",
                hudPanel.transform,
                string.Empty,
                24,
                FontStyle.Bold,
                UiPalette.CellGuide,
                TextAnchor.MiddleLeft);
            milestoneText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.SetCenteredRect(milestoneText.rectTransform, new Vector2(0f, 177f), new Vector2(390f, 42f));

            var objectiveText = _ui.CreateText(
                "Objective",
                hudPanel.transform,
                string.Empty,
                23,
                FontStyle.Bold,
                UiPalette.LightText,
                TextAnchor.UpperLeft);
            UiFactory.SetAnchoredRect(
                objectiveText.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(390f, 66f),
                new Vector2(0f, 124f));
            objectiveText.horizontalOverflow = HorizontalWrapMode.Wrap;
            objectiveText.resizeTextForBestFit = true;
            objectiveText.resizeTextMinSize = 19;
            objectiveText.resizeTextMaxSize = 23;

            var progressText = _ui.CreateText(
                "Progress",
                hudPanel.transform,
                "0/0",
                18,
                FontStyle.Bold,
                UiPalette.MutedText,
                TextAnchor.MiddleLeft);
            UiFactory.SetCenteredRect(progressText.rectTransform, new Vector2(0f, 78f), new Vector2(390f, 25f));

            Image progressTrack = UiFactory.CreateImage(
                "Progress Track", hudPanel.transform, UiPalette.PanelTranslucent);
            UiFactory.SetCenteredRect(
                progressTrack.rectTransform, new Vector2(0f, 57f), new Vector2(390f, 8f));
            progressTrack.raycastTarget = false;

            Image progressFill = UiFactory.CreateImage(
                "Progress Fill", progressTrack.transform, UiPalette.CellGuide);
            UiFactory.Stretch(progressFill.rectTransform);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = 0;
            progressFill.fillAmount = 0f;
            progressFill.raycastTarget = false;

            var dialoguePanel = UiFactory.CreateImage(
                "Dialogue Panel",
                hudPanel.transform,
                UiPalette.PanelTranslucent);
            UiFactory.SetCenteredRect(dialoguePanel.rectTransform, new Vector2(0f, -4f), new Vector2(390f, 104f));

            var dialogueText = _ui.CreateText(
                "Dialogue",
                dialoguePanel.transform,
                string.Empty,
                19,
                FontStyle.Normal,
                UiPalette.LightText,
                TextAnchor.UpperLeft);
            UiFactory.SetAnchoredRect(
                dialogueText.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-24f, -20f),
                Vector2.zero);
            dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            dialogueText.verticalOverflow = VerticalWrapMode.Truncate;
            dialogueText.resizeTextForBestFit = true;
            dialogueText.resizeTextMinSize = 15;
            dialogueText.resizeTextMaxSize = 19;
            dialogueText.raycastTarget = false;
            Button dialogueAdvanceButton = dialoguePanel.gameObject.AddComponent<Button>();
            dialogueAdvanceButton.targetGraphic = dialoguePanel;
            dialogueAdvanceButton.transition = Selectable.Transition.None;
            dialogueAdvanceButton.interactable = false;
            dialoguePanel.gameObject.SetActive(false);

            var hintText = _ui.CreateText(
                "Hint",
                hudPanel.transform,
                string.Empty,
                18,
                FontStyle.Italic,
                UiPalette.HintText,
                TextAnchor.MiddleLeft);
            UiFactory.SetCenteredRect(
                hintText.rectTransform, new Vector2(0f, -85f), new Vector2(390f, 48f));
            hintText.horizontalOverflow = HorizontalWrapMode.Wrap;

            var feedbackText = _ui.CreateText(
                "Correction",
                hudPanel.transform,
                string.Empty,
                18,
                FontStyle.Bold,
                UiPalette.HintText,
                TextAnchor.MiddleLeft);
            feedbackText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.SetCenteredRect(feedbackText.rectTransform, new Vector2(0f, -130f), new Vector2(390f, 42f));
            feedbackText.gameObject.SetActive(false);

            var continueButton = _ui.CreateButton(
                hudPanel.transform,
                "Continue Button",
                _textProvider.GetText("tutorial.ui.continue"),
                UiPalette.Primary,
                UiPalette.PrimaryHighlight,
                () => { });
            UiFactory.SetCenteredRect(continueButton.GetComponent<RectTransform>(), new Vector2(0f, -168f), new Vector2(340f, 42f));

            var replayButton = _ui.CreateButton(
                hudPanel.transform,
                "Replay Step Button",
                _textProvider.GetText("tutorial.ui.replay"),
                UiPalette.Secondary,
                UiPalette.SecondaryHighlight,
                () => { });
            UiFactory.SetCenteredRect(replayButton.GetComponent<RectTransform>(), new Vector2(0f, -208f), new Vector2(340f, 36f));

            var presenter = new TutorialHudPresenter(
                hudPanel.gameObject,
                objectiveText,
                progressText,
                progressFill,
                hintText,
                dialoguePanel.gameObject,
                dialogueText,
                dialogueAdvanceButton,
                continueButton,
                null,
                replayButton,
                milestoneText,
                feedbackText,
                hudPanel.rectTransform,
                _textProvider.GetText("tutorial.ui.milestone_progress"));

            presenter.SetSpotlight(spotlight);

            _matchView.RegisterSpotlightTargets(presenter);
            hudPanel.gameObject.SetActive(false);
            _gameplayRoot.SetActive(false);
            return presenter;
        }

        private void BuildOverlays()
        {
            _confirmationShell = UiFactory.CreateOverlayShell(
                _canvas.transform,
                "Tutorial Confirmation",
                true,
                new Vector2(700f, 380f),
                UiPalette.GlassStrong);
            Transform confirmationPanel = _confirmationShell.Card.transform;
            _confirmationTitle = _ui.CreateOutlinedTitle(
                confirmationPanel, "Title", string.Empty, 42);
            UiFactory.SetCenteredRect(
                _confirmationTitle.rectTransform,
                new Vector2(0f, 112f),
                new Vector2(610f, 58f));

            _confirmationBody = _ui.CreateText(
                "Body", confirmationPanel, string.Empty, 25, FontStyle.Normal,
                UiPalette.LightText, TextAnchor.MiddleCenter);
            _confirmationBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.SetCenteredRect(
                _confirmationBody.rectTransform,
                new Vector2(0f, 28f),
                new Vector2(600f, 105f));

            _confirmationConfirm = _ui.CreateAccentButton(
                confirmationPanel, "Confirm", string.Empty,
                UiPalette.Quit, UiPalette.QuitHighlight,
                ConfirmPendingAction, 27);
            UiFactory.SetCenteredRect(
                _confirmationConfirm.GetComponent<RectTransform>(),
                new Vector2(-160f, -112f),
                new Vector2(285f, 62f));

            _confirmationCancel = _ui.CreateAccentButton(
                confirmationPanel, "Cancel", _textProvider.GetText("tutorial.ui.cancel"),
                UiPalette.Primary, UiPalette.PrimaryHighlight,
                CancelPendingAction, 27);
            UiFactory.SetCenteredRect(
                _confirmationCancel.GetComponent<RectTransform>(),
                new Vector2(160f, -112f),
                new Vector2(285f, 62f));
            SetNavigation(_confirmationConfirm, null, null, null, _confirmationCancel);
            SetNavigation(_confirmationCancel, null, null, _confirmationConfirm, null);

            _completionShell = UiFactory.CreateOverlayShell(
                _canvas.transform,
                "Tutorial Completion",
                true,
                new Vector2(920f, 700f),
                UiPalette.GlassStrong);
            Transform completionPanel = _completionShell.Card.transform;
            Text completionTitle = _ui.CreateOutlinedTitle(
                completionPanel,
                "Title",
                _textProvider.GetText("tutorial.ui.completion_title"),
                52);
            UiFactory.SetCenteredRect(completionTitle.rectTransform, new Vector2(0f, 275f), new Vector2(820f, 72f));

            _completionChapter = _ui.CreateText(
                "Chapter", completionPanel, string.Empty, 32, FontStyle.Bold,
                UiPalette.CellGuide, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_completionChapter.rectTransform, new Vector2(0f, 205f), new Vector2(800f, 52f));

            Text saved = _ui.CreateText(
                "Saved", completionPanel, _textProvider.GetText("tutorial.ui.completion_saved"),
                22, FontStyle.Normal, UiPalette.Success, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(saved.rectTransform, new Vector2(0f, 160f), new Vector2(700f, 36f));

            Text learned = _ui.CreateText(
                "Learned Heading", completionPanel, _textProvider.GetText("tutorial.ui.completion_learned"),
                24, FontStyle.Bold, UiPalette.LightText, TextAnchor.MiddleLeft);
            UiFactory.SetCenteredRect(learned.rectTransform, new Vector2(0f, 106f), new Vector2(720f, 38f));

            _completionOutcomes = _ui.CreateText(
                "Outcomes", completionPanel, string.Empty, 24, FontStyle.Normal,
                UiPalette.LightText, TextAnchor.UpperLeft);
            _completionOutcomes.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.SetCenteredRect(_completionOutcomes.rectTransform, new Vector2(0f, -4f), new Vector2(720f, 170f));

            _completionPrimary = _ui.CreateAccentButton(
                completionPanel, "Primary", string.Empty,
                UiPalette.Success, UiPalette.SuccessHighlight, () => { }, 28);
            UiFactory.SetCenteredRect(_completionPrimary.GetComponent<RectTransform>(), new Vector2(0f, -164f), new Vector2(520f, 66f));

            _completionReplay = _ui.CreateAccentButton(
                completionPanel, "Replay", _textProvider.GetText("tutorial.ui.completion_replay"),
                UiPalette.Secondary, UiPalette.SecondaryHighlight, () => { }, 24);
            UiFactory.SetCenteredRect(_completionReplay.GetComponent<RectTransform>(), new Vector2(-190f, -246f), new Vector2(340f, 56f));

            _completionPicker = _ui.CreateAccentButton(
                completionPanel, "Chapter Picker", _textProvider.GetText("tutorial.ui.completion_picker"),
                UiPalette.Primary, UiPalette.PrimaryHighlight, () => { }, 24);
            UiFactory.SetCenteredRect(_completionPicker.GetComponent<RectTransform>(), new Vector2(190f, -246f), new Vector2(340f, 56f));

            SetNavigation(_completionPrimary, null, _completionReplay, null, null);
            SetNavigation(_completionReplay, _completionPrimary, null, null, _completionPicker);
            SetNavigation(_completionPicker, _completionPrimary, null, _completionReplay, null);
        }

        private void ShowConfirmation(
            string titleKey,
            string bodyKey,
            string confirmKey,
            string cancelKey,
            Action onConfirm,
            Action onCancel)
        {
            _pendingConfirmation = onConfirm;
            _pendingCancellation = onCancel;
            _confirmationTitle.text = _textProvider.GetText(titleKey);
            _confirmationBody.text = _textProvider.GetText(bodyKey);
            Text confirmLabel = _confirmationConfirm.GetComponentInChildren<Text>();
            if (confirmLabel != null)
                confirmLabel.text = _textProvider.GetText(confirmKey);
            Text cancelLabel = _confirmationCancel.GetComponentInChildren<Text>();
            if (cancelLabel != null)
                cancelLabel.text = _textProvider.GetText(cancelKey);

            _confirmationShell.Root.transform.SetAsLastSibling();
            _confirmationShell.Open();
            UiFactory.Select(_confirmationCancel);
        }

        private void ConfirmPendingAction()
        {
            Action action = _pendingConfirmation;
            _pendingConfirmation = null;
            _pendingCancellation = null;
            action?.Invoke();
        }

        private void CancelPendingAction()
        {
            Action action = _pendingCancellation;
            _pendingConfirmation = null;
            _pendingCancellation = null;
            action?.Invoke();
        }

        private void CloseConfirmation()
        {
            _pendingConfirmation = null;
            _pendingCancellation = null;
            if (_confirmationShell != null && _confirmationShell.IsOpen)
                _confirmationShell.Close();
        }

        private void ShowCompletionSummary(string tutorialId)
        {
            _matchView?.HideTilePreview();
            TutorialChapterDescriptor chapter = TutorialChapterCatalog.Find(tutorialId);
            TutorialChapterDescriptor next = TutorialChapterCatalog.Next(tutorialId);
            _screenState = TutorialScreenState.CompletionSummary;
            _completionChapter.text = _textProvider.GetText(chapter.CardTitleKey);

            var outcomes = new System.Text.StringBuilder();
            for (int i = 0; i < chapter.OutcomeKeys.Count; i++)
                outcomes.Append("• ").AppendLine(_textProvider.GetText(chapter.OutcomeKeys[i]));
            _completionOutcomes.text = outcomes.ToString();

            _completionPrimary.onClick.RemoveAllListeners();
            Text primaryLabel = _completionPrimary.GetComponentInChildren<Text>();
            if (next != null)
            {
                if (primaryLabel != null)
                    primaryLabel.text = _textProvider.GetText("tutorial.ui.completion_next");
                _completionPrimary.onClick.AddListener(() => StartChapter(next.Id, resumeProgress: true));
            }
            else
            {
                if (primaryLabel != null)
                    primaryLabel.text = _textProvider.GetText("tutorial.ui.completion_finish");
                _completionPrimary.onClick.AddListener(ReturnToMainMenu);
            }

            _completionReplay.onClick.RemoveAllListeners();
            _completionReplay.onClick.AddListener(() => StartChapter(chapter.Id, resumeProgress: false));
            _completionPicker.onClick.RemoveAllListeners();
            _completionPicker.onClick.AddListener(() => ShowChapterPicker());

            _completionShell.Root.transform.SetAsLastSibling();
            _completionShell.Open();
            UiFactory.Select(_completionPrimary);
        }

        private void CloseCompletion()
        {
            if (_completionShell != null && _completionShell.IsOpen)
                _completionShell.Close();
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedHandler()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureTutorialForScene(scene);
        }

        internal static TutorialSceneController EnsureTutorialForScene(Scene scene)
        {
            if (!scene.IsValid() ||
                !scene.isLoaded ||
                scene.name != SceneName)
            {
                return null;
            }

            TutorialSceneController existing =
                UnityEngine.Object.FindFirstObjectByType<TutorialSceneController>(FindObjectsInactive.Include);
            if (existing != null)
                return existing;

            var root = new GameObject("Tutorial Bootstrap");
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.AddComponent<TutorialSceneController>();
        }
    }
}
