using System;
using System.Collections.Generic;
using AMath.Core.Assistance;
using AMath.Core.Events;
using AMath.Tutorial.Interfaces;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AMath.Tutorial.UI
{
    /// <summary>
    /// Scene-facing tutorial presenter. Implements the Tutorial presentation
    /// service contracts while keeping all scene references localized here.
    /// It receives its manager, event bus and optional AI entry point through
    /// <see cref="Configure"/>; it never locates global services itself.
    /// </summary>
    public sealed class TutorialUI : MonoBehaviour,
        ITutorialUiService,
        ITutorialHighlightService,
        ITutorialDialogueService
    {
        [Header("Tutorial HUD")]
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _objectiveText;
        [SerializeField] private Text _progressText;
        [SerializeField] private Text _hintText;

        [Header("Dialogue")]
        [SerializeField] private GameObject _dialoguePanel;
        [SerializeField] private Text _dialogueText;
        [SerializeField] private AudioSource _voiceSource;

        [Header("Player controls")]
        [SerializeField] private Button _skipButton;
        [SerializeField] private Button _replayStepButton;
        [SerializeField] private Button _askAiButton;

        [Header("Highlights")]
        [SerializeField] private TutorialHighlightTarget[] _highlightTargets = Array.Empty<TutorialHighlightTarget>();

        [Header("Scripted button signals")]
        [SerializeField] private TutorialButtonSignal[] _buttonSignals = Array.Empty<TutorialButtonSignal>();

        private readonly Dictionary<string, GameObject> _highlights = new();
        private readonly List<(Button button, UnityAction callback)> _boundSignals = new();

        private TutorialManager _manager;
        private IEventBus _eventBus;
        private IAiEntryPoint _aiEntryPoint;
        private Action _dialogueFinished;
        private bool _aiUnlockedByTutorial;

        /// <summary>
        /// Injects the runtime collaborators. This must be called by the
        /// composition root before the tutorial begins; <paramref name="aiEntryPoint"/>
        /// may be null when the AI assembly/module is disabled or absent.
        /// </summary>
        public void Configure(
            TutorialManager manager,
            IEventBus eventBus,
            IAiEntryPoint aiEntryPoint = null)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _aiEntryPoint = aiEntryPoint;
            RefreshAiButton();
        }

        private void Awake()
        {
            BuildHighlightLookup();
            BindControls();
            SetAiButtonAvailable(false);

            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(false);
        }

        private void Update()
        {
            if (_dialogueFinished != null &&
                _voiceSource != null &&
                _voiceSource.clip != null &&
                !_voiceSource.isPlaying)
            {
                CompleteDialogue();
            }
        }

        private void OnDestroy()
        {
            foreach ((Button button, UnityAction callback) in _boundSignals)
                button.onClick.RemoveListener(callback);

            _skipButton?.onClick.RemoveListener(OnSkipPressed);
            _replayStepButton?.onClick.RemoveListener(OnReplayStepPressed);
            _askAiButton?.onClick.RemoveListener(OnAskAiPressed);
        }

        /// <inheritdoc />
        public void SetObjective(string objectiveText)
        {
            if (_root != null && !_root.activeSelf)
                _root.SetActive(true);
            if (_objectiveText != null)
                _objectiveText.text = objectiveText ?? string.Empty;
        }

        /// <inheritdoc />
        public void SetProgress(int currentStep, int totalSteps)
        {
            if (_progressText != null)
                _progressText.text = $"{currentStep}/{totalSteps}";
        }

        /// <inheritdoc />
        public void ShowHint(string hintText)
        {
            if (_hintText != null)
                _hintText.text = hintText ?? string.Empty;
        }

        /// <inheritdoc />
        public void ClearHint()
        {
            if (_hintText != null)
                _hintText.text = string.Empty;
        }

        /// <inheritdoc />
        public void SetAiButtonAvailable(bool available)
        {
            _aiUnlockedByTutorial = available;
            RefreshAiButton();
        }

        /// <inheritdoc />
        public void Highlight(string targetId)
        {
            ClearHighlight();
            if (!string.IsNullOrWhiteSpace(targetId) &&
                _highlights.TryGetValue(targetId, out GameObject highlight) &&
                highlight != null)
            {
                highlight.SetActive(true);
            }
        }

        /// <inheritdoc />
        public void ClearHighlight()
        {
            foreach (GameObject highlight in _highlights.Values)
            {
                if (highlight != null)
                    highlight.SetActive(false);
            }
        }

        /// <inheritdoc />
        public void Play(string localizedText, AudioClip voiceClip, Action onFinished)
        {
            StopCurrentDialogue(invokeCallback: false);

            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(true);
            if (_dialogueText != null)
                _dialogueText.text = localizedText ?? string.Empty;

            _dialogueFinished = onFinished;
            if (voiceClip != null && _voiceSource != null)
            {
                _voiceSource.clip = voiceClip;
                _voiceSource.Play();
            }
        }

        /// <summary>Dismisses visible dialogue when a scripted button is pressed.</summary>
        public void DismissActiveDialogue()
        {
            if (_dialogueFinished != null)
                StopCurrentDialogue(invokeCallback: true);
        }

        /// <inheritdoc />
        public void Skip() => StopCurrentDialogue(invokeCallback: true);

        private void BindControls()
        {
            if (_skipButton != null)
                _skipButton.onClick.AddListener(OnSkipPressed);
            if (_replayStepButton != null)
                _replayStepButton.onClick.AddListener(OnReplayStepPressed);
            if (_askAiButton != null)
                _askAiButton.onClick.AddListener(OnAskAiPressed);

            foreach (TutorialButtonSignal signal in _buttonSignals)
            {
                if (signal == null || signal.Button == null || string.IsNullOrWhiteSpace(signal.ButtonId))
                    continue;

                UnityAction callback = () =>
                {
                    DismissActiveDialogue();
                    PublishButtonPress(signal.ButtonId);
                };
                signal.Button.onClick.AddListener(callback);
                _boundSignals.Add((signal.Button, callback));
            }
        }

        private void BuildHighlightLookup()
        {
            foreach (TutorialHighlightTarget target in _highlightTargets)
            {
                if (target == null ||
                    string.IsNullOrWhiteSpace(target.TargetId) ||
                    target.HighlightVisual == null ||
                    _highlights.ContainsKey(target.TargetId))
                {
                    continue;
                }

                _highlights.Add(target.TargetId, target.HighlightVisual);
                target.HighlightVisual.SetActive(false);
            }
        }

        private void OnSkipPressed()
        {
            PublishButtonPress(TutorialButtonIds.Skip);
            _manager?.Skip();
        }

        private void OnReplayStepPressed()
        {
            PublishButtonPress(TutorialButtonIds.ReplayStep);
            _manager?.ReplayCurrentStep();
        }

        private void OnAskAiPressed()
        {
            PublishButtonPress(TutorialButtonIds.AskAi);
            if (_aiUnlockedByTutorial && _aiEntryPoint?.IsAvailable == true)
                _aiEntryPoint.OpenChat();
        }

        private void PublishButtonPress(string buttonId)
        {
            _eventBus?.Publish(new ButtonPressedEvent { ButtonId = buttonId });
        }

        private void RefreshAiButton()
        {
            if (_askAiButton == null) return;

            bool show = _aiUnlockedByTutorial && _aiEntryPoint?.IsAvailable == true;
            _askAiButton.gameObject.SetActive(show);
        }

        private void StopCurrentDialogue(bool invokeCallback)
        {
            if (_voiceSource != null)
            {
                _voiceSource.Stop();
                _voiceSource.clip = null;
            }

            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(false);

            if (invokeCallback)
                CompleteDialogue();
            else
                _dialogueFinished = null;
        }

        private void CompleteDialogue()
        {
            Action callback = _dialogueFinished;
            _dialogueFinished = null;

            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(false);

            callback?.Invoke();
        }
    }

    /// <summary>Stable identifiers emitted for Tutorial-owned controls.</summary>
    public static class TutorialButtonIds
    {
        /// <summary>Skip Tutorial button id.</summary>
        public const string Skip = "tutorial.skip";

        /// <summary>Replay Step button id.</summary>
        public const string ReplayStep = "tutorial.replay-step";

        /// <summary>Ask AI button id.</summary>
        public const string AskAi = "tutorial.ask-ai";
    }

    /// <summary>Scene binding that makes an arbitrary UGUI button observable by a scripted condition.</summary>
    [Serializable]
    public sealed class TutorialButtonSignal
    {
        /// <summary>Button to observe.</summary>
        [SerializeField] private Button _button;

        /// <summary>Stable tutorial condition id published on click.</summary>
        [SerializeField] private string _buttonId;

        /// <summary>Button to observe.</summary>
        public Button Button => _button;

        /// <summary>Stable event id published when the button is pressed.</summary>
        public string ButtonId => _buttonId;
    }
}
