using System;
using System.Collections.Generic;
using AMath.Core.Assistance;
using AMath.Core.Events;
using AMath.Tutorial;
using AMath.Tutorial.Definitions;
using AMath.Tutorial.Interfaces;
using AMath.Tutorial.UI;
using UnityEngine;
using UnityEngine.UI;

namespace AMath.UI.Tutorial
{
    /// <summary>
    /// Code-driven tutorial HUD that implements the three presentation
    /// services consumed by <see cref="AMath.Tutorial.TutorialManager"/>.
    /// </summary>
    public sealed class TutorialHudPresenter :
        ITutorialUiService,
        ITutorialHighlightService,
        ITutorialDialogueService
    {
        private readonly Dictionary<string, GameObject> _highlights = new();
        private readonly GameObject _root;
        private readonly Text _objectiveText;
        private readonly Text _progressText;
        private readonly Text _hintText;
        private readonly GameObject _dialoguePanel;
        private readonly Text _dialogueText;
        private readonly Button _continueButton;
        private readonly Button _skipButton;
        private readonly Button _replayStepButton;
        private readonly Button _askAiButton;

        private TutorialManager _manager;
        private IEventBus _eventBus;
        private IAiEntryPoint _aiEntryPoint;
        private Action _dialogueFinished;

        public TutorialHudPresenter(
            GameObject root,
            Text objectiveText,
            Text progressText,
            Text hintText,
            GameObject dialoguePanel,
            Text dialogueText,
            Button continueButton,
            Button skipButton,
            Button replayStepButton,
            Button askAiButton = null)
        {
            _root = root;
            _objectiveText = objectiveText;
            _progressText = progressText;
            _hintText = hintText;
            _dialoguePanel = dialoguePanel;
            _dialogueText = dialogueText;
            _continueButton = continueButton;
            _skipButton = skipButton;
            _replayStepButton = replayStepButton;
            _askAiButton = askAiButton;

            if (_askAiButton != null)
                _askAiButton.gameObject.SetActive(false);
        }

        /// <summary>Registers a highlight overlay for a stable target id.</summary>
        public void RegisterHighlight(string targetId, GameObject highlightVisual)
        {
            if (string.IsNullOrWhiteSpace(targetId) || highlightVisual == null)
                return;

            _highlights[targetId] = highlightVisual;
            highlightVisual.SetActive(false);
        }

        /// <summary>Wires runtime collaborators after the manager is created.</summary>
        public void Configure(TutorialManager manager, IEventBus eventBus, IAiEntryPoint aiEntryPoint = null)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _aiEntryPoint = aiEntryPoint;

            _continueButton.onClick.AddListener(OnContinuePressed);
            _skipButton.onClick.AddListener(OnSkipPressed);
            _replayStepButton.onClick.AddListener(OnReplayStepPressed);
            if (_askAiButton != null)
                _askAiButton.onClick.AddListener(OnAskAiPressed);
        }

        /// <summary>Call from the scene controller each frame.</summary>
        public void Tick()
        {
            // Voice playback completion is handled by TutorialUI when an AudioSource
            // is present. The bootstrap HUD has no voice source, so dialogue waits
            // for the Continue button instead.
        }

        /// <summary>Releases button listeners.</summary>
        public void Dispose()
        {
            if (_continueButton != null)
                _continueButton.onClick.RemoveListener(OnContinuePressed);
            if (_skipButton != null)
                _skipButton.onClick.RemoveListener(OnSkipPressed);
            if (_replayStepButton != null)
                _replayStepButton.onClick.RemoveListener(OnReplayStepPressed);
            if (_askAiButton != null)
                _askAiButton.onClick.RemoveListener(OnAskAiPressed);
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
            if (_askAiButton == null)
                return;

            // Offering the affordance without something behind it would give
            // the player a button that does nothing.
            _askAiButton.gameObject.SetActive(available && _aiEntryPoint != null);
        }

        /// <inheritdoc />
        public void Highlight(string targetId)
        {
            ClearHighlight();
            if (string.IsNullOrWhiteSpace(targetId))
                return;

            if (_highlights.TryGetValue(targetId, out GameObject highlight) && highlight != null)
                highlight.SetActive(true);
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
            StopDialogue(invokeCallback: false);

            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(true);
            if (_dialogueText != null)
                _dialogueText.text = localizedText ?? string.Empty;

            _dialogueFinished = onFinished;
        }

        /// <summary>Dismisses visible dialogue when Continue is pressed.</summary>
        public void DismissActiveDialogue()
        {
            if (_dialogueFinished != null)
                CompleteDialogue();
        }

        /// <inheritdoc />
        public void Skip() => StopDialogue(invokeCallback: true);

        private void OnContinuePressed()
        {
            DismissActiveDialogue();
            PublishButtonPress(IntroTutorialSequence.ContinueButtonId);
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
            _aiEntryPoint?.OpenChat();
        }

        private void PublishButtonPress(string buttonId)
        {
            _eventBus?.Publish(new ButtonPressedEvent { ButtonId = buttonId });
        }

        private void StopDialogue(bool invokeCallback)
        {
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
}
