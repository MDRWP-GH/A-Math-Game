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
        private static readonly Color ModeNormalColor = new(0.20f, 0.55f, 0.82f, 1f);
        private static readonly Color ModeSelectedColor = new(0.12f, 0.75f, 0.45f, 1f);

        [SerializeField] private GameObject _root;
        [SerializeField] private Text _transcriptText;
        [SerializeField] private InputField _questionInput;
        [SerializeField] private Button _sendButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _ruleModeButton;
        [SerializeField] private Button _strategyModeButton;
        [SerializeField] private Dropdown _modeDropdown;
        [SerializeField] private GameObject _busyIndicator;

        [Tooltip("Stable mode ids matching Dropdown options by index.")]
        [SerializeField] private string[] _modeIds = Array.Empty<string>();

        private AiAssistantController _controller;
        private bool _controlsBound;

        /// <summary>Builds the minimal chat UI used by the code-driven play flow.</summary>
        public static AiChatWindow CreateRuntime(Transform parent)
        {
            var root = new GameObject("AiChatWindow", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Stretch((RectTransform)root.transform);

            var window = root.AddComponent<AiChatWindow>();
            window._root = root;

            Image overlay = CreateImage("Overlay", root.transform, new Color(0f, 0f, 0f, 0.55f));
            Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;

            Image panel = CreateImage("Panel", root.transform, new Color(0.10f, 0.16f, 0.29f, 1f));
            SetCentered(panel.rectTransform, new Vector2(0f, 0f), new Vector2(760f, 600f));

            Text title = CreateText("Title", panel.transform, "AI Assistant", 36, TextAnchor.MiddleCenter);
            SetCentered(title.rectTransform, new Vector2(0f, 250f), new Vector2(640f, 52f));

            window._transcriptText = CreateText("Transcript", panel.transform, string.Empty, 21, TextAnchor.UpperLeft);
            window._transcriptText.horizontalOverflow = HorizontalWrapMode.Wrap;
            window._transcriptText.verticalOverflow = VerticalWrapMode.Overflow;
            SetCentered(window._transcriptText.rectTransform, new Vector2(0f, 35f), new Vector2(660f, 260f));

            window._modeIds = new[] { "rule_assistant", "strategy_coach" };
            window._ruleModeButton = CreateButton("Rules Mode", panel.transform, "Rules");
            SetCentered(window._ruleModeButton.GetComponent<RectTransform>(), new Vector2(-115f, 198f), new Vector2(200f, 38f));

            window._strategyModeButton = CreateButton("Strategy Mode", panel.transform, "Strategy");
            SetCentered(window._strategyModeButton.GetComponent<RectTransform>(), new Vector2(115f, 198f), new Vector2(200f, 38f));

            window._questionInput = CreateInputField(panel.transform);
            SetCentered(window._questionInput.GetComponent<RectTransform>(), new Vector2(-70f, -195f), new Vector2(510f, 60f));

            window._sendButton = CreateButton("Send", panel.transform, "Ask");
            SetCentered(window._sendButton.GetComponent<RectTransform>(), new Vector2(250f, -195f), new Vector2(120f, 60f));

            window._closeButton = CreateButton("Close", panel.transform, "Close");
            SetCentered(window._closeButton.GetComponent<RectTransform>(), new Vector2(0f, -265f), new Vector2(180f, 48f));

            window._busyIndicator = CreateText("Busy", panel.transform, "Thinking...", 18, TextAnchor.MiddleCenter).gameObject;
            SetCentered(window._busyIndicator.GetComponent<RectTransform>(), new Vector2(0f, -135f), new Vector2(300f, 32f));
            window.Initialize();
            root.SetActive(false);
            return window;
        }

        /// <summary>Injects the chat controller after the composition root creates it.</summary>
        public void Configure(AiAssistantController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            SelectMode(_modeDropdown != null ? _modeDropdown.value : 0);
        }

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_controlsBound)
            {
                return;
            }

            // Awake runs before CreateRuntime assigns its controls, so defer binding
            // until all runtime controls exist.
            if (_sendButton == null || _closeButton == null)
            {
                return;
            }

            _controlsBound = true;
            _sendButton?.onClick.AddListener(SubmitCurrentQuestion);
            _closeButton?.onClick.AddListener(Close);
            _ruleModeButton?.onClick.AddListener(SelectRuleMode);
            _strategyModeButton?.onClick.AddListener(SelectStrategyMode);
            _modeDropdown?.onValueChanged.AddListener(SelectMode);

            if (_root != null)
                _root.SetActive(false);
            SetBusy(false);
        }

        private void OnDestroy()
        {
            _controller?.CancelRequest();
            _sendButton?.onClick.RemoveListener(SubmitCurrentQuestion);
            _closeButton?.onClick.RemoveListener(Close);
            _ruleModeButton?.onClick.RemoveListener(SelectRuleMode);
            _strategyModeButton?.onClick.RemoveListener(SelectStrategyMode);
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
            _controller?.CancelRequest();
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
            if (_ruleModeButton != null)
                _ruleModeButton.interactable = !isBusy;
            if (_strategyModeButton != null)
                _strategyModeButton.interactable = !isBusy;
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

            if (_controller.TrySelectMode(_modeIds[index]))
            {
                SetSelectedMode(index);
            }
        }

        private void SelectRuleMode() => SelectMode(0);

        private void SelectStrategyMode() => SelectMode(1);

        private void SetSelectedMode(int selectedIndex)
        {
            SetModeButtonColor(_ruleModeButton, selectedIndex == 0);
            SetModeButtonColor(_strategyModeButton, selectedIndex == 1);
        }

        private static void SetModeButtonColor(Button button, bool selected)
        {
            if (button != null && button.targetGraphic is Image image)
            {
                image.color = selected ? ModeSelectedColor : ModeNormalColor;
            }
        }

        private void AppendMessage(string text)
        {
            if (_transcriptText == null || string.IsNullOrWhiteSpace(text))
                return;

            if (_transcriptText.text.Length > 0)
                _transcriptText.text += Environment.NewLine + Environment.NewLine;
            _transcriptText.text += text;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string value, int fontSize, TextAnchor alignment)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static InputField CreateInputField(Transform parent)
        {
            Image background = CreateImage("Question", parent, Color.white);
            Text text = CreateText("Text", background.transform, string.Empty, 20, TextAnchor.MiddleLeft);
            text.color = Color.black;
            Stretch(text.rectTransform, 12f);
            Text placeholder = CreateText("Placeholder", background.transform, "Ask about the rules...", 20, TextAnchor.MiddleLeft);
            placeholder.color = new Color(0.35f, 0.35f, 0.35f);
            Stretch(placeholder.rectTransform, 12f);

            InputField field = background.gameObject.AddComponent<InputField>();
            field.targetGraphic = background;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }

        private static Button CreateButton(string name, Transform parent, string label)
        {
            Image image = CreateImage(name, parent, new Color(0.20f, 0.55f, 0.82f, 1f));
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text text = CreateText("Label", image.transform, label, 20, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 0f);
            return button;
        }

        private static void Stretch(RectTransform rectTransform, float horizontalPadding = 0f)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(horizontalPadding, 0f);
            rectTransform.offsetMax = new Vector2(-horizontalPadding, 0f);
        }

        private static void SetCentered(RectTransform rectTransform, Vector2 position, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = position;
        }
    }
}
