using System;
using UnityEngine;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>
    /// Popup option list for the settings rows: every choice is visible at once,
    /// so a value is picked directly instead of clicking a row repeatedly to
    /// cycle past the options in between.
    ///
    /// A screen owns one instance and every row opens it in turn, which keeps
    /// "only one list open at a time", focus restoration and Cancel handling in
    /// a single place instead of spread across the rows.
    /// </summary>
    internal sealed class UiDropdown
    {
        #region Constants

        private const float ItemHeight = 56f;

        /// <summary>Tallest the list may get before it starts scrolling.</summary>
        private const float MaxListHeight = 420f;

        private const float VerticalGap = 6f;
        private const float PanelPadding = 8f;
        private const float MinPanelWidth = 240f;
        private const int ItemFontSize = 30;

        #endregion

        #region Fields

        private readonly UiFactory _ui;

        /// <summary>Full-screen rect that must be the last canvas child, so lists draw on top.</summary>
        private readonly RectTransform _layer;

        private GameObject _popup;
        private ScrollRect _scroll;
        private Selectable[] _items;
        private Selectable _restoreFocus;
        private Action<int> _onChoose;

        #endregion

        public UiDropdown(UiFactory ui, RectTransform layer)
        {
            _ui = ui;
            _layer = layer;
        }

        /// <summary>True while a list is on screen. Cancel should close it before the screen.</summary>
        public bool IsOpen => _popup != null;

        #region Caret sprite

        private static Sprite _caretSprite;

        /// <summary>
        /// Downward caret that marks a row as a list opener (cached). Generated
        /// rather than authored because the display font has no triangle glyph.
        /// </summary>
        public static Sprite CaretSprite
        {
            get
            {
                if (_caretSprite != null)
                {
                    return _caretSprite;
                }

                const int size = 32;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                for (var y = 0; y < size; y++)
                {
                    // Texture y counts up from the bottom, so rows narrow as y
                    // falls, giving a triangle that points down.
                    var halfWidth = (y + 0.5f) / size * (size * 0.5f);
                    for (var x = 0; x < size; x++)
                    {
                        var distanceFromCentre = Mathf.Abs(x + 0.5f - size * 0.5f);
                        texture.SetPixel(x, y, distanceFromCentre <= halfWidth ? Color.white : Color.clear);
                    }
                }

                texture.Apply(false, true);
                _caretSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f),
                    100f);
                return _caretSprite;
            }
        }

        #endregion

        #region Open / close

        /// <summary>
        /// Opens the list directly under <paramref name="anchor"/> (above it when
        /// there is no room below). <paramref name="onChoose"/> runs after the
        /// list is torn down, so the caller is free to rebuild the row.
        /// </summary>
        public void Open(
            Selectable anchor,
            int count,
            int selectedIndex,
            Func<int, string> format,
            Action<int> onChoose)
        {
            Close();

            if (anchor == null || count <= 0 || format == null)
            {
                return;
            }

            _restoreFocus = anchor;
            _onChoose = onChoose;
            var selected = Mathf.Clamp(selectedIndex, 0, count - 1);

            _popup = UiFactory.CreateRect("Dropdown", _layer).gameObject;
            UiFactory.Stretch(_popup.GetComponent<RectTransform>());

            BuildDismissCatcher(_popup.transform);

            var listHeight = Mathf.Min(MaxListHeight, count * ItemHeight + PanelPadding * 2f);
            var panel = BuildPanel((RectTransform)anchor.transform, listHeight);
            var viewHeight = listHeight - PanelPadding * 2f;
            var content = BuildScrollArea(panel);

            BuildItems(content, count, selected, format, viewHeight);

            UiFactory.Select(_items[selected]);
            ScrollTo(selected, count, viewHeight);
        }

        /// <summary>Tears down the list and hands focus back to the row that opened it.</summary>
        public void Close()
        {
            if (_popup == null)
            {
                return;
            }

            var restore = _restoreFocus;

            UnityEngine.Object.Destroy(_popup);
            _popup = null;
            _scroll = null;
            _items = null;
            _onChoose = null;
            _restoreFocus = null;

            UiFactory.Select(restore);
        }

        #endregion

        #region Building

        /// <summary>
        /// Invisible full-screen button behind the list: clicking anywhere else
        /// dismisses it. Kept out of the navigation graph so arrow keys cannot
        /// land on it and strand the player on an invisible control.
        /// </summary>
        private void BuildDismissCatcher(Transform parent)
        {
            // Fully transparent images stop receiving raycasts, so the catcher
            // keeps a sliver of alpha rather than being truly invisible.
            var catcher = UiFactory.CreateImage("Catcher", parent, new Color(0f, 0f, 0f, 0.01f));
            catcher.raycastTarget = true;
            UiFactory.Stretch(catcher.rectTransform);

            var button = catcher.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(Close);
        }

        private Image BuildPanel(RectTransform anchorRect, float listHeight)
        {
            var corners = new Vector3[4];
            anchorRect.GetWorldCorners(corners);
            Vector3 bottomLeft = _layer.InverseTransformPoint(corners[0]);
            Vector3 topRight = _layer.InverseTransformPoint(corners[2]);

            var width = Mathf.Max(MinPanelWidth, topRight.x - bottomLeft.x);

            // Flip above the row when the list would run off the bottom of the screen.
            var dropsBelowScreen = bottomLeft.y - VerticalGap - listHeight < -_layer.rect.height * 0.5f;
            var top = dropsBelowScreen
                ? topRight.y + VerticalGap + listHeight
                : bottomLeft.y - VerticalGap;

            var panel = UiFactory.CreateGlassPanel(_popup.transform, "Panel", UiPalette.GlassStrong);
            UiFactory.SetAnchoredRect(
                panel.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 1f),
                new Vector2(width, listHeight),
                new Vector2(bottomLeft.x, top));
            return panel;
        }

        private RectTransform BuildScrollArea(Image panel)
        {
            var viewport = UiFactory.CreateRect("Viewport", panel.transform);
            UiFactory.SetStretchRect(viewport, PanelPadding, PanelPadding, PanelPadding, PanelPadding);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = UiFactory.CreateRect("Items", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            UiFactory.AddVerticalLayout(content.gameObject, 0f);

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.content = content;
            _scroll.viewport = viewport;
            _scroll.horizontal = false;
            _scroll.vertical = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 28f;

            return content;
        }

        private void BuildItems(
            RectTransform content,
            int count,
            int selected,
            Func<int, string> format,
            float viewHeight)
        {
            _items = new Selectable[count];

            for (var i = 0; i < count; i++)
            {
                var index = i;
                var item = _ui.CreateFlatButton(
                    content,
                    "Option " + i,
                    string.Empty,
                    ItemFontSize,
                    TextAnchor.MiddleLeft,
                    () => Choose(index));
                UiFactory.SetLayoutSize(item.gameObject, 0f, ItemHeight, 1f);

                var label = item.GetComponentInChildren<Text>();
                label.color = i == selected ? UiPalette.Primary : UiPalette.LightText;
                UiText.Set(label, format(i), _ui.Font);

                _items[i] = item;
            }

            // Wrapping chain, matching the tab column, so the list is fully
            // reachable with a gamepad or the arrow keys.
            for (var i = 0; i < count; i++)
            {
                UiFactory.SetVerticalNavigation(
                    _items[i],
                    _items[(i - 1 + count) % count],
                    _items[(i + 1) % count]);
            }

            // Scrolling is only enabled once the items actually overflow, so a
            // short list cannot be dragged out of view.
            if (_scroll != null)
            {
                _scroll.vertical = count * ItemHeight > viewHeight;
            }
        }

        #endregion

        #region Behaviour

        private void Choose(int index)
        {
            // Applying the value can rebuild this row (a resolution change fires
            // DisplaySettings.Changed), so the list is gone before that runs.
            var onChoose = _onChoose;
            Close();
            onChoose?.Invoke(index);
        }

        /// <summary>Centres the active option, so a long resolution list opens on the current value.</summary>
        private void ScrollTo(int index, int count, float viewHeight)
        {
            if (_scroll == null || !_scroll.vertical)
            {
                return;
            }

            var contentHeight = count * ItemHeight;
            var overflow = contentHeight - viewHeight;
            if (overflow <= 0f)
            {
                return;
            }

            var offset = index * ItemHeight - (viewHeight - ItemHeight) * 0.5f;
            _scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(offset / overflow);
        }

        #endregion
    }
}
