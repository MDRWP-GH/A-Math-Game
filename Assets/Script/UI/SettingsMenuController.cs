using System;
using System.Collections.Generic;
using AMath.Art;
using AMath.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>
    /// Settings screen matching the mockup: scene background, translucent panel,
    /// outlined Jersey text, tab column with selection marker, and one content page.
    /// Changes apply immediately; there is no Apply button.
    /// </summary>
    public sealed class SettingsMenuController : MonoBehaviour
    {
        private const int GeneralTab = 0;
        private const int DisplayTab = 1;
        private const int AudioTab = 2;
        private const int ExitTab = 3;

        private const float TitleLeft = 72f;
        private const float TitleTop = 36f;
        private const float TitleWidth = 640f;
        private const float TitleHeight = 96f;

        private const float PanelLeft = 48f;
        private const float PanelTop = 140f;
        private const float PanelRight = 360f;
        private const float PanelBottom = 48f;

        private const float BodyTop = 200f;
        private const float TabColumnLeft = 96f;
        private const float TabColumnWidth = 300f;
        private const float TabHeight = 70f;
        private const float TabSpacing = 10f;
        private const float MarkerSize = 44f;
        private const float TabTextLeftPad = 56f;

        private const float RowsLeft = 460f;
        private const float RowsRight = 400f;
        private const float RowsBottom = 100f;
        private const float RowHeight = 70f;
        private const float RowSpacing = 28f;
        private const float LabelWidth = 340f;
        private const float LabelGap = 48f;

        private const float FieldWidth = 420f;
        private const float FieldHeight = 52f;
        private const float SliderHeight = 40f;
        private const float SliderHandleSize = 28f;
        private const float GuiScaleValueWidth = 96f;
        private const float CaretSize = 22f;

        private static readonly Color SliderTrack = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color SliderHandle = new Color(0.82f, 0.84f, 0.88f, 1f);
        private static readonly Color MarkerFallback = new Color(0.35f, 0.78f, 1f, 1f);

        private static readonly string[] TabKeys =
        {
            "ui.settings.tab_general",
            "ui.settings.tab_display",
            "ui.settings.tab_audio",
            "ui.settings.tab_exit"
        };

        private static readonly string[] ViewModeKeys =
        {
            "ui.settings.view_window",
            "ui.settings.view_fullscreen",
            "ui.settings.view_borderless"
        };

        private readonly List<Button> _tabButtons = new List<Button>();
        private readonly GameObject[] _pages = new GameObject[3];
        private readonly Selectable[] _pageFocus = new Selectable[3];
        private readonly List<Action> _refreshers = new List<Action>();

        private UiFactory _ui;
        private GameObject _screen;
        private RectTransform _navMarker;
        private InputAction _cancelAction;
        private int _activeTab = GeneralTab;

        /// <summary>Shared by every option row, so only one list can be open at a time.</summary>
        private UiDropdown _dropdown;

        /// <summary>Raised when the player leaves the settings screen.</summary>
        public event Action Closed;

        public bool IsOpen => _screen != null && _screen.activeSelf;

        public static SettingsMenuController Create(Transform parent, Font font)
        {
            var root = new GameObject("Settings Menu");
            root.transform.SetParent(parent, false);

            var controller = root.AddComponent<SettingsMenuController>();
            controller.Build(font);
            return controller;
        }

        public void Open()
        {
            if (_screen == null)
            {
                return;
            }

            var fade = OverlayFade.Ensure(_screen);
            fade.FadeIn();
            RefreshAll();
            ShowTab(_activeTab);
            UiFactory.Select(_tabButtons[_activeTab]);
        }

        public void Close()
        {
            if (_screen == null || !_screen.activeSelf)
            {
                return;
            }

            _dropdown?.Close();
            GameSettings.Flush();
            // Instant hide so the main menu can reclaim focus without waiting on
            // the fade; the fade-in on Open still softens the next visit.
            OverlayFade.Ensure(_screen).HideInstant();
            Closed?.Invoke();
        }

        private void Build(Font font)
        {
            _ui = new UiFactory(font);
            BuildScreen();
            _screen.SetActive(false);

            DisplaySettings.Changed += RefreshAll;
            GameSettings.Changed += RefreshAll;
            _cancelAction = UiFactory.FindCancelAction();
        }

        private void OnDestroy()
        {
            DisplaySettings.Changed -= RefreshAll;
            GameSettings.Changed -= RefreshAll;
        }

        private void Update()
        {
            if (!IsOpen || !WasCancelPressed())
            {
                return;
            }

            // An open option list is a layer over the page, so Cancel dismisses
            // it first rather than closing the whole screen from underneath it.
            if (_dropdown != null && _dropdown.IsOpen)
            {
                _dropdown.Close();
                return;
            }

            Close();
        }

        private void BuildScreen()
        {
            var canvas = _ui.CreateCanvas(transform, "Settings Canvas", 200);
            _screen = canvas.gameObject;

            UiFactory.CreateFullScreenBackground(
                _screen.transform,
                "Main Menu Backgrounds",
                UiPalette.Background);

            var panel = UiFactory.CreateGlassPanel(_screen.transform, "Panel", UiPalette.Glass);
            UiFactory.SetStretchRect(panel.rectTransform, PanelLeft, PanelTop, PanelRight, PanelBottom);

            var title = _ui.CreateOutlinedTitle(
                _screen.transform,
                "Title",
                string.Empty,
                72,
                TextAnchor.UpperLeft);
            UiFactory.SetTopLeftRect(
                title.rectTransform,
                new Vector2(TitleLeft, TitleTop),
                new Vector2(TitleWidth, TitleHeight));
            LocalizedText.Bind(title, "ui.settings.title");

            BuildNavigation(_screen.transform);

            var rows = UiFactory.CreateRect("Rows", _screen.transform);
            UiFactory.SetStretchRect(rows, RowsLeft, BodyTop, RowsRight, RowsBottom);

            // Created before the pages so the rows can reference it, but moved to
            // the end of the canvas afterwards: option lists have to draw over
            // the page they belong to.
            var dropdownLayer = UiFactory.CreateRect("Dropdown Layer", _screen.transform);
            UiFactory.Stretch(dropdownLayer);
            _dropdown = new UiDropdown(_ui, dropdownLayer);

            _pages[GeneralTab] = BuildGeneralPage(rows);
            _pages[DisplayTab] = BuildDisplayPage(rows);
            _pages[AudioTab] = BuildAudioPage(rows);

            dropdownLayer.SetAsLastSibling();

            ShowTab(GeneralTab);
        }

        private void BuildNavigation(Transform parent)
        {
            var column = UiFactory.CreateRect("Tab Column", parent);
            var columnHeight = TabKeys.Length * TabHeight + (TabKeys.Length - 1) * TabSpacing;
            UiFactory.SetTopLeftRect(
                column,
                new Vector2(TabColumnLeft, BodyTop),
                new Vector2(TabColumnWidth, columnHeight));
            UiFactory.AddVerticalLayout(column.gameObject, TabSpacing);

            for (var i = 0; i < TabKeys.Length; i++)
            {
                var index = i;
                var button = CreateTabButton(column, "Tab " + TabKeys[i], () => OnTabPressed(index));
                UiFactory.SetLayoutSize(button.gameObject, TabColumnWidth, TabHeight);

                var text = button.GetComponentInChildren<Text>();
                LocalizedText.Bind(text, TabKeys[i]);

                _tabButtons.Add(button);
            }

            for (var i = 0; i < _tabButtons.Count; i++)
            {
                var up = _tabButtons[(i - 1 + _tabButtons.Count) % _tabButtons.Count];
                var down = _tabButtons[(i + 1) % _tabButtons.Count];
                UiFactory.SetVerticalNavigation(_tabButtons[i], up, down);
            }

            _navMarker = CreateSelectionMarker(_tabButtons[0].transform);
        }

        private Button CreateTabButton(Transform parent, string name, Action onClick)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = true;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = new Color(1f, 1f, 1f, 0f),
                highlightedColor = new Color(1f, 1f, 1f, 0f),
                pressedColor = new Color(1f, 1f, 1f, 0f),
                selectedColor = new Color(1f, 1f, 1f, 0f),
                disabledColor = new Color(1f, 1f, 1f, 0f),
                colorMultiplier = 1f,
                fadeDuration = 0f
            };
            button.onClick.AddListener(onClick.Invoke);

            var label = _ui.CreateText(
                "Label",
                buttonObject.transform,
                string.Empty,
                44,
                FontStyle.Normal,
                Color.white,
                TextAnchor.MiddleLeft);
            UiFactory.SetStretchRect(label.rectTransform, TabTextLeftPad, 0f, 12f, 0f);
            UiFactory.AddDoubleOutline(label.gameObject, new Vector2(3f, -3f), new Vector2(1.5f, -1.5f));
            MenuButtonFeedback.Attach(button, label);

            return button;
        }

        private static RectTransform CreateSelectionMarker(Transform tab)
        {
            var sprite = GameImages.LoadIcon("Selected");
            var marker = sprite != null
                ? UiFactory.CreateImage("Selection Marker", tab, sprite)
                : UiFactory.CreateImage("Selection Marker", tab, MarkerFallback);
            marker.preserveAspect = true;
            UiFactory.SetAnchoredRect(
                marker.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(MarkerSize, MarkerSize),
                new Vector2(MarkerSize * 0.45f, 0f));
            return marker.rectTransform;
        }

        private GameObject BuildGeneralPage(Transform parent)
        {
            var page = CreatePage(parent, "General Page");

            _pageFocus[GeneralTab] = AddTextRow(
                page.transform,
                "ui.settings.player_name",
                () => GameSettings.PlayerName,
                value => GameSettings.PlayerName = value);

            AddDropdownRow(
                page.transform,
                "ui.settings.language",
                () => GameSettings.LanguageLabels.Length,
                () => GameSettings.LanguageIndex,
                index => GameSettings.LanguageIndex = index,
                index => GameSettings.LanguageLabels[index]);

            return page;
        }

        private GameObject BuildDisplayPage(Transform parent)
        {
            var page = CreatePage(parent, "Display Page");

            _pageFocus[DisplayTab] = AddDropdownRow(
                page.transform,
                "ui.settings.view_mode",
                () => ViewModeKeys.Length,
                () => (int)DisplaySettings.ViewMode,
                index => DisplaySettings.SetViewMode((ViewMode)index),
                index => Localization.UiLocalizationProvider.Shared.GetText(ViewModeKeys[index]));

            AddDropdownRow(
                page.transform,
                "ui.settings.resolution",
                () => DisplaySettings.AvailableSizes.Count,
                () => DisplaySettings.ResolutionIndex,
                index => DisplaySettings.SetResolution(DisplaySettings.AvailableSizes[index]),
                index => DisplaySettings.AvailableSizes[index].Label);

            AddGuiScaleRow(page.transform);

            return page;
        }

        private GameObject BuildAudioPage(Transform parent)
        {
            var page = CreatePage(parent, "Audio Page");

            _pageFocus[AudioTab] = AddSliderRow(
                page.transform,
                "ui.settings.sfx",
                () => GameSettings.SoundEffectsVolume,
                value => GameSettings.SoundEffectsVolume = value);

            AddSliderRow(
                page.transform,
                "ui.settings.music",
                () => GameSettings.MusicVolume,
                value => GameSettings.MusicVolume = value);

            return page;
        }

        private void OnTabPressed(int index)
        {
            if (index == ExitTab)
            {
                Close();
                return;
            }

            ShowTab(index);
            UiFactory.Select(_pageFocus[index]);
        }

        private void ShowTab(int index)
        {
            // Otherwise a list opened on one page keeps hovering over the next.
            _dropdown?.Close();

            _activeTab = Mathf.Clamp(index, 0, _pages.Length - 1);

            for (var i = 0; i < _pages.Length; i++)
            {
                if (_pages[i] != null)
                {
                    _pages[i].SetActive(i == _activeTab);
                }
            }

            _navMarker.SetParent(_tabButtons[_activeTab].transform, false);
            _navMarker.SetAsFirstSibling();
        }

        private GameObject CreatePage(Transform parent, string name)
        {
            var page = UiFactory.CreateRect(name, parent);
            UiFactory.Stretch(page);
            UiFactory.AddVerticalLayout(page.gameObject, RowSpacing);
            return page.gameObject;
        }

        private RectTransform CreateRow(Transform page, string labelKey)
        {
            var row = UiFactory.CreateRect("Row " + labelKey, page);
            UiFactory.SetLayoutSize(row.gameObject, 0f, RowHeight);
            UiFactory.AddHorizontalLayout(row.gameObject, LabelGap);

            var labelText = _ui.CreateText(
                "Label",
                row,
                string.Empty,
                36,
                FontStyle.Normal,
                Color.white,
                TextAnchor.MiddleLeft);
            UiFactory.SetLayoutSize(labelText.gameObject, LabelWidth, RowHeight);
            UiFactory.AddDoubleOutline(labelText.gameObject, new Vector2(3f, -3f), new Vector2(1.5f, -1.5f));
            LocalizedText.Bind(labelText, labelKey);

            var control = UiFactory.CreateRect("Control", row);
            UiFactory.SetLayoutSize(control.gameObject, 0f, RowHeight, flexibleWidth: 1f);
            return control;
        }

        /// <summary>
        /// Label + current value; clicking opens a list of every option so the
        /// player can jump straight to the one they want instead of clicking
        /// through all the options in between to reach it.
        /// </summary>
        private Selectable AddDropdownRow(
            Transform page,
            string label,
            Func<int> countProvider,
            Func<int> indexProvider,
            Action<int> applyIndex,
            Func<int, string> formatIndex)
        {
            var control = CreateRow(page, label);
            UiFactory.AddHorizontalLayout(control.gameObject, 0f);

            var buttonObject = new GameObject(
                "Value",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(control, false);
            UiFactory.SetLayoutSize(buttonObject, 0f, RowHeight, flexibleWidth: 1f);

            var image = buttonObject.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = true;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = new Color(1f, 1f, 1f, 0f),
                highlightedColor = new Color(1f, 1f, 1f, 0f),
                pressedColor = new Color(1f, 1f, 1f, 0f),
                selectedColor = new Color(1f, 1f, 1f, 0f),
                disabledColor = new Color(1f, 1f, 1f, 0f),
                colorMultiplier = 1f,
                fadeDuration = 0f
            };

            var valueText = _ui.CreateText(
                "Label",
                buttonObject.transform,
                string.Empty,
                36,
                FontStyle.Normal,
                Color.white,
                TextAnchor.MiddleLeft);
            UiFactory.SetStretchRect(valueText.rectTransform, 0f, 0f, CaretSize + 12f, 0f);
            UiFactory.AddDoubleOutline(valueText.gameObject, new Vector2(3f, -3f), new Vector2(1.5f, -1.5f));

            var caret = UiFactory.CreateImage(
                "Caret",
                buttonObject.transform,
                UiDropdown.CaretSprite,
                UiPalette.LightText);
            caret.preserveAspect = true;
            UiFactory.SetAnchoredRect(
                caret.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(CaretSize, CaretSize),
                new Vector2(-4f, 0f));

            void Refresh()
            {
                var count = countProvider();
                var value = count <= 0 ? "-" : formatIndex(Mathf.Clamp(indexProvider(), 0, count - 1));
                UiText.Set(valueText, value, _ui.Font);
            }

            button.onClick.AddListener(() => _dropdown?.Open(
                button,
                countProvider(),
                indexProvider(),
                formatIndex,
                index =>
                {
                    applyIndex(index);
                    Refresh();
                }));

            _refreshers.Add(Refresh);
            Refresh();
            return button;
        }

        private Selectable AddSliderRow(Transform page, string label, Func<float> getValue, Action<float> setValue)
        {
            var control = CreateRow(page, label);
            UiFactory.AddHorizontalLayout(control.gameObject, 0f);

            var slider = CreateSettingsSlider(control, "Slider", getValue());
            UiFactory.SetLayoutSize(slider.gameObject, 0f, SliderHeight, flexibleWidth: 1f);
            slider.onValueChanged.AddListener(value => setValue(value));

            _refreshers.Add(() => slider.SetValueWithoutNotify(getValue()));
            return slider;
        }

        private Selectable AddGuiScaleRow(Transform page)
        {
            var control = CreateRow(page, "ui.settings.gui_scale");
            UiFactory.AddHorizontalLayout(control.gameObject, 16f);

            var slider = CreateSettingsSlider(control, "GUI Scale Slider", DisplaySettings.GuiScale);
            UiFactory.SetLayoutSize(slider.gameObject, 0f, SliderHeight, flexibleWidth: 1f);

            var valueText = _ui.CreateText(
                "Value",
                control,
                string.Empty,
                32,
                FontStyle.Normal,
                Color.white,
                TextAnchor.MiddleRight);
            UiFactory.SetLayoutSize(valueText.gameObject, GuiScaleValueWidth, RowHeight);
            UiFactory.AddDoubleOutline(valueText.gameObject, new Vector2(3f, -3f), new Vector2(1.5f, -1.5f));

            slider.onValueChanged.AddListener(DisplaySettings.SetGuiScale);

            void Refresh()
            {
                slider.minValue = DisplaySettings.GuiScaleMinimum;
                slider.maxValue = DisplaySettings.GuiScaleMaximum;
                slider.SetValueWithoutNotify(Mathf.Clamp(
                    DisplaySettings.GuiScale,
                    slider.minValue,
                    slider.maxValue));
                UiText.Set(valueText, $"{Mathf.RoundToInt(DisplaySettings.GuiScale * 100f)}%", _ui.Font);
            }

            _refreshers.Add(Refresh);
            Refresh();
            return slider;
        }

        private Slider CreateSettingsSlider(Transform parent, string name, float value)
        {
            var sliderObject = new GameObject(name, typeof(RectTransform));
            sliderObject.transform.SetParent(parent, false);

            var slider = sliderObject.AddComponent<Slider>();

            var background = UiFactory.CreateImage("Background", sliderObject.transform, SliderTrack);
            background.raycastTarget = true;
            background.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            background.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            background.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            background.rectTransform.sizeDelta = new Vector2(0f, 3f);
            background.rectTransform.anchoredPosition = Vector2.zero;

            var fillArea = UiFactory.CreateRect("Fill Area", sliderObject.transform);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.pivot = new Vector2(0.5f, 0.5f);
            fillArea.sizeDelta = new Vector2(-SliderHandleSize, 3f);
            fillArea.anchoredPosition = Vector2.zero;

            var fill = UiFactory.CreateImage("Fill", fillArea, new Color(1f, 1f, 1f, 0f));
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            fill.rectTransform.sizeDelta = new Vector2(SliderHandleSize, 0f);

            var handleArea = UiFactory.CreateRect("Handle Slide Area", sliderObject.transform);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.pivot = new Vector2(0.5f, 0.5f);
            handleArea.sizeDelta = new Vector2(-SliderHandleSize, 0f);
            handleArea.anchoredPosition = Vector2.zero;

            var handle = UiFactory.CreateImage("Handle", handleArea, SliderHandle);
            handle.raycastTarget = true;
            handle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            handle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(SliderHandleSize, SliderHandleSize);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = Mathf.Clamp01(value);
            slider.colors = new ColorBlock
            {
                normalColor = SliderHandle,
                highlightedColor = Color.white,
                pressedColor = Color.white,
                selectedColor = Color.white,
                disabledColor = new Color(0.5f, 0.5f, 0.5f, 1f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            return slider;
        }

        private Selectable AddTextRow(Transform page, string label, Func<string> getValue, Action<string> setValue)
        {
            var control = CreateRow(page, label);
            UiFactory.AddHorizontalLayout(control.gameObject, 0f);

            var border = UiFactory.CreateImage("Field Border", control, UiPalette.FieldBorder);
            UiFactory.SetLayoutSize(border.gameObject, FieldWidth, FieldHeight);

            var fieldObject = new GameObject(
                "Input",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            fieldObject.transform.SetParent(border.transform, false);
            UiFactory.SetStretchRect(fieldObject.GetComponent<RectTransform>(), 2f, 2f, 2f, 2f);

            var background = fieldObject.GetComponent<Image>();
            background.color = UiPalette.Field;

            var text = _ui.CreateText(
                "Text",
                fieldObject.transform,
                getValue(),
                30,
                FontStyle.Normal,
                Color.white,
                TextAnchor.MiddleLeft);
            text.supportRichText = false;
            UiFactory.SetStretchRect(text.rectTransform, 14f, 4f, 14f, 4f);

            var hint = _ui.CreateText(
                "Placeholder",
                fieldObject.transform,
                string.Empty,
                30,
                FontStyle.Normal,
                new Color(1f, 1f, 1f, 0.35f),
                TextAnchor.MiddleLeft);
            hint.supportRichText = false;
            UiFactory.SetStretchRect(hint.rectTransform, 14f, 4f, 14f, 4f);
            LocalizedText.Bind(hint, "ui.settings.name_placeholder");

            var field = fieldObject.AddComponent<InputField>();
            field.targetGraphic = background;
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = 24;
            field.text = getValue();
            field.colors = new ColorBlock
            {
                normalColor = UiPalette.Field,
                highlightedColor = UiPalette.Field,
                pressedColor = UiPalette.Field,
                selectedColor = UiPalette.Field,
                disabledColor = UiPalette.Field,
                colorMultiplier = 1f,
                fadeDuration = 0f
            };
            field.onEndEdit.AddListener(value => setValue(value));

            _refreshers.Add(() =>
            {
                // Keep the raw string in the field; only swap the font for Thai display.
                text.font = UiText.IsThai ? GameFonts.K2D : _ui.Font;
                text.lineSpacing = UiText.IsThai ? 1.4f : 1f;
                hint.font = text.font;
                hint.lineSpacing = text.lineSpacing;
                field.SetTextWithoutNotify(getValue());
            });
            return field;
        }

        private void RefreshAll()
        {
            foreach (var refresh in _refreshers)
            {
                refresh();
            }
        }

        private bool WasCancelPressed()
        {
            return UiFactory.WasCancelPressed(_cancelAction);
        }
    }
}
