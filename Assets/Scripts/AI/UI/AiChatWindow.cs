using System;
using AMath.AI.Chat;
using AMath.AI.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace AMath.AI.UI
{
    /// <summary>
    /// UGUI implementation of the AI Chat Window. It only collects questions,
    /// selects a mode and displays text; all context capture, networking and
    /// restrictions remain in <see cref="AiAssistantController"/>.
    /// </summary>
    public sealed class AiChatWindow : MonoBehaviour, IAiChatView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _transcriptText;
        [SerializeField] private InputField _questionInput;
        [SerializeField] private Button _sendButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Dropdown _modeDropdown;
        [SerializeField] private GameObject _busyIndicator;

        [Tooltip("Stable mode ids matching Dropdown options by index.")]
        [SerializeField] private string[] _modeIds = Array.Empty<string>();

        private AiAssistantController _controller;

        /// <summary>Injects the chat controller after the composition root creates it.</summary>
        public void Configure(AiAssistantController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            SelectMode(_modeDropdown != null ? _modeDropdown.value : 0);
        }

        private void Awake()
        {
            _sendButton?.onClick.AddListener(SubmitCurrentQuestion);
            _closeButton?.onClick.AddListener(Close);
            _modeDropdown?.onValueChanged.AddListener(SelectMode);

            if (_root != null)
                _root.SetActive(false);
            SetBusy(false);
        }

        private void OnDestroy()
        {
            _sendButton?.onClick.RemoveListener(SubmitCurrentQuestion);
            _closeButton?.onClick.RemoveListener(Close);
            _modeDropdown?.onValueChanged.RemoveListener(SelectMode);
        }

        /// <inheritdoc />
        public void Open()
        {
            if (_root != null)
                _root.SetActive(true);
            _questionInput?.ActivateInputField();
        }

        /// <inheritdoc />
        public void Close()
        {
            if (_root != null)
                _root.SetActive(false);
        }

        /// <inheritdoc />
        public void ShowUserMessage(string text) => AppendMessage(text);

        /// <inheritdoc />
        public void ShowAssistantMessage(string text) => AppendMessage(text);

        /// <inheritdoc />
        public void ShowError(string text) => AppendMessage(text);

        /// <inheritdoc />
        public void SetBusy(bool isBusy)
        {
            if (_busyIndicator != null)
                _busyIndicator.SetActive(isBusy);
            if (_sendButton != null)
                _sendButton.interactable = !isBusy;
            if (_modeDropdown != null)
                _modeDropdown.interactable = !isBusy;
        }

        private async void SubmitCurrentQuestion()
        {
            if (_controller == null || _questionInput == null) return;

            string question = _questionInput.text;
            _questionInput.text = string.Empty;
            await _controller.AskAsync(question);
            _questionInput.ActivateInputField();
        }

        private void SelectMode(int index)
        {
            if (_controller == null || index < 0 || index >= _modeIds.Length)
                return;

            _controller.TrySelectMode(_modeIds[index]);
        }

        private void AppendMessage(string text)
        {
            if (_transcriptText == null || string.IsNullOrWhiteSpace(text))
                return;

            if (_transcriptText.text.Length > 0)
                _transcriptText.text += Environment.NewLine + Environment.NewLine;
            _transcriptText.text += text;
        }
    }
}
