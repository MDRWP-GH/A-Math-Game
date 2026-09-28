using System;
using System.Collections.Generic;
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
    /// Code-driven, low-friction tutorial HUD. Guidance remains visible while
    /// navigation controls stay available, mirroring modern card-game tutorials.
    /// </summary>
    public sealed class TutorialHudPresenter :
        ITutorialUiService,
        ITutorialHighlightService,
        ITutorialDialogueService
    {
        private readonly Dictionary<string, RectTransform> _spotlightTargets = new();
        private readonly Dictionary<string, Func<RectTransform>> _spotlightResolvers = new();
        private readonly GameObject _root;
        private readonly Text _milestoneText;
        private readonly Text _objectiveText;
        private readonly Text _progressText;
        private readonly Image _progressFill;
        private readonly Text _hintText;
        private readonly GameObject _dialoguePanel;
        private readonly Text _dialogueText;
        private readonly Button _dialogueAdvanceButton;
        private readonly Button _continueButton;
        private readonly Button _skipButton;
        private readonly Button _replayStepButton;
        private readonly Text _feedbackText;
        private readonly RectTransform _coachPanel;
        private readonly string _progressFormat;

        private TutorialSpotlightOverlay _spotlight;
        private TutorialManager _manager;
        private IEventBus _eventBus;
        private Action _dialogueFinished;
        private float _feedbackRemaining;
        private float _shakeRemaining;
        private Vector2 _coachBasePosition;
        private string _activeTargetId;
        private RectTransform _resolvedTarget;

        public TutorialHudPresenter(
            GameObject root,
            Text objectiveText,
            Text progressText,
            Image progressFill,
            Text hintText,
            GameObject dialoguePanel,
            Text dialogueText,
            Button dialogueAdvanceButton,
            Button continueButton,
            Button skipButton,
            Button replayStepButton,
            Text milestoneText = null,
            Text feedbackText = null,
            RectTransform coachPanel = null,
            string progressFormat = null)
        {
            _root = root;
            _objectiveText = objectiveText;
            _progressText = progressText;
            _progressFill = progressFill;
            _hintText = hintText;
            _dialoguePanel = dialoguePanel;
            _dialogueText = dialogueText;
            _dialogueAdvanceButton = dialogueAdvanceButton;
            _continueButton = continueButton;
            _skipButton = skipButton;
            _replayStepButton = replayStepButton;
            _milestoneText = milestoneText;
            _feedbackText = feedbackText;
            _coachPanel = coachPanel;
            _progressFormat = progressFormat;
            if (_coachPanel != null)
                _coachBasePosition = _coachPanel.anchoredPosition;

            if (_continueButton != null)
                _continueButton.gameObject.SetActive(false);
        }

        internal void SetSpotlight(TutorialSpotlightOverlay spotlight) => _spotlight = spotlight;

        public void RegisterSpotlightTarget(string targetId, RectTransform target)
        {
            if (!string.IsNullOrWhiteSpace(targetId) && target != null)
                _spotlightTargets[targetId] = target;
        }

        public void RegisterSpotlightTargetResolver(string targetId, Func<RectTransform> resolver)
        {
            if (!string.IsNullOrWhiteSpace(targetId) && resolver != null)
                _spotlightResolvers[targetId] = resolver;
        }

        public void Configure(TutorialManager manager, IEventBus eventBus)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            _continueButton?.onClick.AddListener(OnContinuePressed);
            _dialogueAdvanceButton?.onClick.AddListener(OnContinuePressed);
            _skipButton?.onClick.AddListener(OnSkipPressed);
            _replayStepButton?.onClick.AddListener(OnReplayStepPressed);
        }

        public void Dispose()
        {
            _continueButton?.onClick.RemoveListener(OnContinuePressed);
            _dialogueAdvanceButton?.onClick.RemoveListener(OnContinuePressed);
            _skipButton?.onClick.RemoveListener(OnSkipPressed);
            _replayStepButton?.onClick.RemoveListener(OnReplayStepPressed);
        }

        public void SetObjective(string objectiveText)
        {
            if (_root != null && !_root.activeSelf)
                _root.SetActive(true);
            if (_objectiveText != null)
                _objectiveText.text = objectiveText ?? string.Empty;
        }

        public void SetMilestone(string milestoneText)
        {
            if (_root != null && !_root.activeSelf)
                _root.SetActive(true);
            if (_milestoneText != null)
                _milestoneText.text = milestoneText ?? string.Empty;
        }

        public void SetProgress(int currentStep, int totalSteps)
        {
            if (_progressText != null)
                _progressText.text = string.IsNullOrWhiteSpace(_progressFormat)
                    ? $"{currentStep}/{totalSteps}"
                    : string.Format(_progressFormat, currentStep, totalSteps);
            if (_progressFill != null)
                _progressFill.fillAmount = totalSteps > 0
                    ? Mathf.Clamp01((float)currentStep / totalSteps)
                    : 0f;
        }

        public void ShowHint(string hintText)
        {
            if (_hintText != null)
                _hintText.text = hintText ?? string.Empty;
        }

        public void ClearHint()
        {
            if (_hintText != null)
                _hintText.text = string.Empty;
        }

        public void Highlight(string targetId)
        {
            ClearHighlight();
            if (string.IsNullOrWhiteSpace(targetId))
                return;

            _activeTargetId = targetId;
            RefreshHighlight();
        }

        public void ClearHighlight()
        {
            _activeTargetId = null;
            _resolvedTarget = null;
            _spotlight?.Hide();
        }

        public RectTransform ActiveSpotlightTarget => _resolvedTarget;

        private void RefreshHighlight()
        {
            if (_spotlight == null || string.IsNullOrEmpty(_activeTargetId)) return;
            RectTransform target = null;
            if (_spotlightResolvers.TryGetValue(_activeTargetId, out Func<RectTransform> resolver))
                target = resolver();
            else
                _spotlightTargets.TryGetValue(_activeTargetId, out target);
            if (target != null && !target.gameObject.activeInHierarchy)
                target = null;
            if (target == _resolvedTarget) return;
            _resolvedTarget = target;
            if (target == null)
            {
                _spotlight.Hide();
                return;
            }
            bool arrowAbove = _activeTargetId.StartsWith("demo-cell-", StringComparison.Ordinal);
            _spotlight.Show(target, arrowAbove);
            Selectable selectable = target.GetComponent<Selectable>();
            if (selectable != null && selectable.IsActive() && selectable.IsInteractable())
                selectable.Select();
        }

        /// <summary>Shows a short corrective message without changing tutorial progress.</summary>
        public void ShowCorrection(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (_feedbackText != null)
            {
                _feedbackText.text = message;
                _feedbackText.color = UiPalette.HintText;
                _feedbackText.gameObject.SetActive(true);
            }

            _feedbackRemaining = 2.8f;
            _shakeRemaining = 0.35f;
            if (!string.IsNullOrWhiteSpace(_activeTargetId))
                _spotlight?.ReplayPulse();
        }

        /// <summary>Updates transient correction feedback and the coach-panel nudge.</summary>
        public void Tick(float deltaTime)
        {
            RefreshHighlight();
            UpdateCoachPlacement();
            if (_feedbackRemaining > 0f)
            {
                _feedbackRemaining -= Mathf.Max(0f, deltaTime);
                if (_feedbackRemaining <= 0f && _feedbackText != null)
                {
                    _feedbackText.text = string.Empty;
                    _feedbackText.gameObject.SetActive(false);
                }
            }

            if (_coachPanel == null)
                return;

            if (_shakeRemaining > 0f)
            {
                _shakeRemaining -= Mathf.Max(0f, deltaTime);
                float offset = Mathf.Sin(_shakeRemaining * 70f) * 7f;
                _coachPanel.anchoredPosition = _coachBasePosition + new Vector2(offset, 0f);
            }
            else if (_coachPanel.anchoredPosition != _coachBasePosition)
            {
                _coachPanel.anchoredPosition = _coachBasePosition;
            }
        }

        private void UpdateCoachPlacement()
        {
            if (_coachPanel == null || !(_coachPanel.parent is RectTransform parent)) return;
            float side = 1f;
            if (_resolvedTarget != null)
            {
                Vector3[] corners = new Vector3[4];
                _resolvedTarget.GetWorldCorners(corners);
                Vector3 center = parent.InverseTransformPoint((corners[0] + corners[2]) * 0.5f);
                if (center.x > 0f) side = -1f;
            }
            float x = Mathf.Max(0f, parent.rect.width * 0.5f - _coachPanel.rect.width * 0.5f - 24f);
            _coachBasePosition = new Vector2(side * x, 0f);
        }

        public void Play(string localizedText, Action onFinished)
        {
            StopDialogue(invokeCallback: false);

            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(true);
            if (_dialogueText != null)
                _dialogueText.text = localizedText ?? string.Empty;

            _dialogueFinished = onFinished;
            bool waitsForDismiss = onFinished != null;
            if (_continueButton != null)
            {
                _continueButton.gameObject.SetActive(waitsForDismiss);
                if (waitsForDismiss)
                    _continueButton.Select();
            }
            if (_dialogueAdvanceButton != null)
                _dialogueAdvanceButton.interactable = waitsForDismiss;
        }

        public void DismissActiveDialogue()
        {
            if (_dialogueFinished != null)
                CompleteDialogue();
        }

        public void Skip() => StopDialogue(invokeCallback: true);

        private void OnContinuePressed()
        {
            if (_dialogueFinished == null)
                return;

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

        private void PublishButtonPress(string buttonId) =>
            _eventBus?.Publish(new ButtonPressedEvent { ButtonId = buttonId });

        private void StopDialogue(bool invokeCallback)
        {
            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(false);
            if (_continueButton != null)
                _continueButton.gameObject.SetActive(false);
            if (_dialogueAdvanceButton != null)
                _dialogueAdvanceButton.interactable = false;

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
            if (_continueButton != null)
                _continueButton.gameObject.SetActive(false);
            if (_dialogueAdvanceButton != null)
                _dialogueAdvanceButton.interactable = false;

            callback?.Invoke();
        }
    }
}
