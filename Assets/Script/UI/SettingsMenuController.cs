using System;
using System.Collections.Generic;
using AMath.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>
    /// The settings screen: a tab column on the left and one page of rows on the right.
    /// Every change is applied and stored as soon as it is made, so there is no Apply button.
    /// </summary>
    public sealed class SettingsMenuController : MonoBehaviour
    {
        private const int GeneralTab = 0;
        private const int DisplayTab = 1;
        private const int AudioTab = 2;
        private const int ExitTab = 3;

        private const float ScreenPaddingLeft = 96f;
        private const float TitleTop = 46f;
        private const float TitleWidth = 700f;
        private const float TitleHeight = 116f;

        // The tab column and the rows share this top edge, and a row is as tall as a tab, so the
        // first row always lines up with the first tab.
        private const float BodyTop = 208f;

        private const float TabColumnLeft = 150f;
        private const float TabColumnWidth = 320f;
        private const float TabHeight = 74f;
        private const float TabSpacing = 12f;
        private const float MarkerSize = 32f;
        // Far enough left of a tab to leave the diamond in the margin the title starts at.
        private const float MarkerDistanceFromTab = 38f;

        private const float BackdropLeft = 470f;
        private const float BackdropTop = 150f;
        private const float BackdropRight = 48f;
        private const float BackdropBottom = 56f;

        private const float RowsLeft = 540f;
        private const float RowsRight = 80f;
        private const float RowsBottom = 120f;
        private const float RowHeight = TabHeight;
        private const float RowSpacing = 18f;
        private const float LabelWidth = 400f;
        private const float LabelGap = 100f;

        private const float ArrowSize = 56f;
        private const float ArrowGap = 12f;
        private const int ControlInset = 20;
        private const float SliderHeight = 48f;
        private const float FieldWidth = 500f;
        private const float FieldHeight = 56f;

        private static readonly string[] TabLabels = { "General", "Display", "Audio", "Exit" };

        private readonly List<Button> _tabButtons = new List<Button>();
        private readonly List<Text> _tabTexts = new List<Text>();
        private readonly GameObject[] _pages = new GameObject[3];
        private readonly Selectable[] _pageFocus = new Selectable[3];
        private readonly List<Action> _refreshers = new List<Action>();

        private UiFactory _ui;
        private GameObject _screen;
        private RectTransform _navMarker;
        private InputAction _cancelAction;
        private int _activeTab = GeneralTab;

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

            _screen.SetActive(true);
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

            GameSettings.Flush();
            _screen.SetActive(false);
            Closed?.Invoke();
        }

        private void Build(Font font)
        {
            _ui = new UiFactory(font);
            BuildScreen();
            _screen.SetActive(false);

            DisplaySettings.Changed += RefreshAll;
            GameSettings.Changed += RefreshAll;
            _cancelAction = FindCancelAction();
        }

        private void OnDestroy()
        {
            DisplaySettings.Changed -= RefreshAll;
            GameSettings.Changed -= RefreshAll;
        }

        private void Update()
        {
            if (IsOpen && WasCancelPressed())
            {
                Close();
            }
        }

        private void BuildScreen()
        {
            var canvas = _ui.CreateCanvas(transform, "Settings Canvas", 200);
            _screen = canvas.gameObject;

            var backdrop = UiFactory.CreateImage("Backdrop", _screen.transform, UiPalette.Background);
            backdrop.raycastTarget = true;
            UiFactory.Stretch(backdrop.rectTransform);

            var title = _ui.CreateText("Title", _screen.transform, "Settings", 84, FontStyle.Bold, UiPalette.LightText, TextAnchor.UpperLeft);
            UiFactory.SetTopLeftRect(title.rectTransform, new Vector2(ScreenPaddingLeft, TitleTop), new Vector2(TitleWidth, TitleHeight));
            UiFactory.AddShadow(title.gameObject, UiPalette.Shadow, new Vector2(0f, -4f));

            var contentBackdrop = UiFactory.CreateImage("Content Backdrop", _screen.transform, UiPalette.PanelTranslucent);
            UiFactory.SetStretchRect(contentBackdrop.rectTransform, BackdropLeft, BackdropTop, BackdropRight, BackdropBottom);

            BuildNavigation(_screen.transform);

            // The rows sit on top of the backdrop rather than inside it so that they can share the
            // tab column's top edge instead of restating it in the backdrop's own coordinates.
            var rows = UiFactory.CreateRect("Rows", _screen.transform);
            UiFactory.SetStretchRect(rows, RowsLeft, BodyTop, RowsRight, RowsBottom);

            _pages[GeneralTab] = BuildGeneralPage(rows);
            _pages[DisplayTab] = BuildDisplayPage(rows);
            _pages[AudioTab] = BuildAudioPage(rows);

            var hint = _ui.CreateText("Hint", _screen.transform, "Esc / B  •  back to menu", 22, FontStyle.Normal, new Color(0.66f, 0.75f, 0.93f, 0.7f), TextAnchor.LowerLeft);
            UiFactory.SetAnchoredRect(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(520f, 40f), new Vector2(ScreenPaddingLeft, 28f));

            ShowTab(GeneralTab);
        }

        private void BuildNavigation(Transform parent)
        {
            var column = UiFactory.CreateRect("Tab Column", parent);
            var columnHeight = TabLabels.Length * TabHeight + (TabLabels.Length - 1) * TabSpacing;
            UiFactory.SetTopLeftRect(column, new Vector2(TabColumnLeft, BodyTop), new Vector2(TabColumnWidth, columnHeight));
            UiFactory.AddVerticalLayout(column.gameObject, TabSpacing);

            for (var i = 0; i < TabLabels.Length; i++)
            {
                var index = i;
                var button = _ui.CreateFlatButton(column, "Tab " + TabLabels[i], TabLabels[i], 50, TextAnchor.MiddleLeft, () => OnTabPressed(index));
                UiFactory.SetLayoutSize(button.gameObject, TabColumnWidth, TabHeight);

                _tabButtons.Add(button);
                _tabTexts.Add(button.GetComponentInChildren<Text>());
            }

            for (var i = 0; i < _tabButtons.Count; i++)
            {
                var up = _tabButtons[(i - 1 + _tabButtons.Count) % _tabButtons.Count];
                var down = _tabButtons[(i + 1) % _tabButtons.Count];
                UiFactory.SetVerticalNavigation(_tabButtons[i], up, down);
            }

            _navMarker = CreateSelectionMarker(_tabButtons[0].transform);
        }

        /// <summary>
        /// The diamond lives inside a tab rather than beside the column, so moving it is a matter
        /// of reparenting it and never of knowing where that tab ended up.
        /// </summary>
        private static RectTransform CreateSelectionMarker(Transform tab)
        {
            var marker = UiFactory.CreateImage("Selection Marker", tab, UiPalette.Primary).rectTransform;
            UiFactory.SetAnchoredRect(
                marker,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(MarkerSize, MarkerSize),
                new Vector2(-MarkerDistanceFromTab, 0f));
            marker.localEulerAngles = new Vector3(0f, 0f, 45f);
            return marker;
        }

        private GameObject BuildGeneralPage(Transform parent)
        {
            var page = CreatePage(parent, "General Page");

            _pageFocus[GeneralTab] = AddTextRow(
                page.transform,
                "Player Name",
                () => GameSettings.PlayerName,
                value => GameSettings.PlayerName = value);

            AddOptionRow(
                page.transform,
                "Language",
                () => GameSettings.LanguageLabels.Length,
                () => GameSettings.LanguageIndex,
                index => GameSettings.LanguageIndex = index,
                index => GameSettings.LanguageLabels[index]);

            return page;
        }

        private GameObject BuildDisplayPage(Transform parent)
        {
            var page = CreatePage(parent, "Display Page");

            _pageFocus[DisplayTab] = AddOptionRow(
                page.transform,
                "View Mode",
                () => DisplaySettings.ViewModeLabels.Length,
                () => (int)DisplaySettings.ViewMode,
                index => DisplaySettings.SetViewMode((ViewMode)index),
                index => DisplaySettings.ViewModeLabels[index]);

            AddOptionRow(
                page.transform,
                "Resolution",
                () => DisplaySettings.AvailableSizes.Count,
                () => DisplaySettings.ResolutionIndex,
                index => DisplaySettings.SetResolution(DisplaySettings.AvailableSizes[index]),
                index => DisplaySettings.AvailableSizes[index].Label);

            return page;
        }

        private GameObject BuildAudioPage(Transform parent)
        {
            var page = CreatePage(parent, "Audio Page");

            _pageFocus[AudioTab] = AddSliderRow(
                page.transform,
                "Sound Effects",
                () => GameSettings.SoundEffectsVolume,
                value => GameSettings.SoundEffectsVolume = value);

            AddSliderRow(
                page.transform,
                "Music Volume",
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
            _activeTab = Mathf.Clamp(index, 0, _pages.Length - 1);

            for (var i = 0; i < _pages.Length; i++)
            {
                if (_pages[i] != null)
                {
                    _pages[i].SetActive(i == _activeTab);
                }
            }

            for (var i = 0; i < _tabTexts.Count; i++)
            {
                _tabTexts[i].color = i == _activeTab ? UiPalette.LightText : UiPalette.MutedText;
            }

            _navMarker.SetParent(_tabButtons[_activeTab].transform, false);
        }

        private GameObject CreatePage(Transform parent, string name)
        {
            var page = UiFactory.CreateRect(name, parent);
            UiFactory.Stretch(page);
            UiFactory.AddVerticalLayout(page.gameObject, RowSpacing);
            return page.gameObject;
        }

        /// <summary>
        /// Builds "label on the left, control area on the right" and returns the control area.
        /// The page stacks the rows, so a row only states how wide its two halves are.
        /// </summary>
        private RectTransform CreateRow(Transform page, string label)
        {
            var row = UiFactory.CreateRect("Row " + label, page);
            UiFactory.SetLayoutSize(row.gameObject, 0f, RowHeight);
            UiFactory.AddHorizontalLayout(row.gameObject, LabelGap);

            var labelText = _ui.CreateText("Label", row, label, 38, FontStyle.Bold, UiPalette.LightText, TextAnchor.MiddleLeft);
            UiFactory.SetLayoutSize(labelText.gameObject, LabelWidth, RowHeight);

            var control = UiFactory.CreateRect("Control", row);
            UiFactory.SetLayoutSize(control.gameObject, 0f, RowHeight, flexibleWidth: 1f);
            return control;
        }

        private Selectable AddOptionRow(
            Transform page,
            string label,
            Func<int> countProvider,
            Func<int> indexProvider,
            Action<int> applyIndex,
            Func<int, string> formatIndex)
        {
            var control = CreateRow(page, label);
            UiFactory.AddHorizontalLayout(control.gameObject, ArrowGap);

            // The arrows close over the value label, but the layout group orders the row by
            // creation, so the left arrow still has to be built first.
            Text valueText = null;

            var previous = CreateArrowButton(control, "Previous", "<", () => Step(-1));

            valueText = _ui.CreateText("Value", control, string.Empty, 38, FontStyle.Normal, UiPalette.LightText, TextAnchor.MiddleCenter);
            UiFactory.SetLayoutSize(valueText.gameObject, 0f, RowHeight, flexibleWidth: 1f);

            CreateArrowButton(control, "Next", ">", () => Step(1));

            void Step(int direction)
            {
                var count = countProvider();
                if (count <= 0)
                {
                    return;
                }

                applyIndex((indexProvider() + direction + count) % count);
                Refresh();
            }

            void Refresh()
            {
                var count = countProvider();
                valueText.text = count <= 0 ? "-" : formatIndex(Mathf.Clamp(indexProvider(), 0, count - 1));
            }

            _refreshers.Add(Refresh);
            Refresh();
            return previous;
        }

        private Selectable AddSliderRow(Transform page, string label, Func<float> getValue, Action<float> setValue)
        {
            var control = CreateRow(page, label);
            UiFactory.AddHorizontalLayout(control.gameObject, 0f, new RectOffset(ControlInset, ControlInset, 0, 0));

            var slider = _ui.CreateSlider(control, "Slider", getValue());
            UiFactory.SetLayoutSize(slider.gameObject, 0f, SliderHeight, flexibleWidth: 1f);
            slider.onValueChanged.AddListener(value => setValue(value));

            _refreshers.Add(() => slider.SetValueWithoutNotify(getValue()));
            return slider;
        }

        private Selectable AddTextRow(Transform page, string label, Func<string> getValue, Action<string> setValue)
        {
            var control = CreateRow(page, label);
            UiFactory.AddHorizontalLayout(control.gameObject, 0f, new RectOffset(ControlInset, 0, 0, 0));

            var field = _ui.CreateInputField(control, "Input", getValue(), "Your name");
            UiFactory.SetLayoutSize(field.gameObject, FieldWidth, FieldHeight);
            field.onEndEdit.AddListener(value => setValue(value));

            _refreshers.Add(() => field.SetTextWithoutNotify(getValue()));
            return field;
        }

        private Button CreateArrowButton(Transform control, string name, string glyph, Action onClick)
        {
            var button = _ui.CreateButton(control, name, glyph, UiPalette.Secondary, UiPalette.SecondaryHighlight, onClick, 34);
            UiFactory.SetLayoutSize(button.gameObject, ArrowSize, ArrowSize);
            return button;
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
            if (_cancelAction != null && _cancelAction.enabled && _cancelAction.WasPressedThisFrame())
            {
                return true;
            }

            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        }

        private static InputAction FindCancelAction()
        {
            var actions = InputSystem.actions;
            return actions == null ? null : actions.FindAction("UI/Cancel", false);
        }
    }
}
