using System;
using AMath.Art;
using UnityEngine;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>
    /// Multi-page How To Play overlay matching the mockup: scene background,
    /// translucent panel, outlined title/Exit, and orange arrow page controls.
    /// Page 4 (Your Turn) is extra guidance beyond the three mockup pages.
    /// </summary>
    internal sealed class HowToPlayOverlay
    {
        private const int PageCount = 4;

        private readonly GameObject _root;
        private readonly OverlayFade _fade;
        private readonly Text _heading;
        private readonly Text _body;
        private readonly Text _pageLabel;
        private readonly Button _prevButton;
        private readonly Button _nextButton;
        private readonly Button _closeButton;
        private readonly GameObject _equipmentIcons;

        private int _page;

        /// <summary>Raised when the player closes the overlay.</summary>
        public event Action Closed;

        public bool IsOpen => _root.activeSelf;

        public HowToPlayOverlay(UiFactory ui, Transform canvasTransform)
        {
            _root = UiFactory.CreateRect("How To Play Overlay", canvasTransform).gameObject;
            UiFactory.Stretch(_root.GetComponent<RectTransform>());
            _fade = OverlayFade.Ensure(_root);

            // Full-screen catcher so clicks do not fall through to the main menu.
            var catcher = UiFactory.CreateImage("Catcher", _root.transform, new Color(0f, 0f, 0f, 0.01f));
            catcher.raycastTarget = true;
            UiFactory.Stretch(catcher.rectTransform);

            UiFactory.CreateFullScreenBackground(
                _root.transform,
                "Main Menu Backgrounds",
                UiPalette.Background);

            var panel = UiFactory.CreateGlassPanel(_root.transform, "Panel", UiPalette.Glass);
            UiFactory.SetStretchRect(panel.rectTransform, 72f, 128f, 72f, 64f);

            var title = ui.CreateOutlinedTitle(
                _root.transform,
                "Title",
                string.Empty,
                68,
                TextAnchor.UpperLeft);
            UiFactory.SetTopLeftRect(title.rectTransform, new Vector2(72f, 28f), new Vector2(720f, 88f));
            LocalizedText.Bind(title, "ui.help.title");

            _heading = ui.CreateText(
                "Heading",
                panel.transform,
                string.Empty,
                40,
                FontStyle.Normal,
                Color.white,
                TextAnchor.UpperLeft);
            UiFactory.SetTopLeftRect(_heading.rectTransform, new Vector2(48f, 36f), new Vector2(1400f, 56f));
            UiFactory.AddDoubleOutline(_heading.gameObject, new Vector2(3f, -3f), new Vector2(1.5f, -1.5f));

            _body = ui.CreateText(
                "Body",
                panel.transform,
                string.Empty,
                28,
                FontStyle.Normal,
                UiPalette.LightText,
                TextAnchor.UpperLeft);
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.verticalOverflow = VerticalWrapMode.Overflow;
            _body.lineSpacing = 1.12f;
            // Leave room at the bottom for Exit / arrows, and for the equipment icon row.
            UiFactory.SetStretchRect(_body.rectTransform, 48f, 100f, 48f, 180f);

            _equipmentIcons = CreateEquipmentIcons(panel.transform);
            _equipmentIcons.SetActive(false);

            _closeButton = ui.CreateTextMenuButton(
                panel.transform,
                "Exit",
                string.Empty,
                40,
                Close);
            UiFactory.SetAnchoredRect(
                _closeButton.GetComponent<RectTransform>(),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(180f, 56f),
                new Vector2(48f, 28f));
            var closeLabel = _closeButton.GetComponentInChildren<Text>();
            closeLabel.alignment = TextAnchor.MiddleLeft;
            LocalizedText.Bind(closeLabel, "ui.help.close");

            _prevButton = UiFactory.CreateIconButton(
                panel.transform,
                "Prev",
                GameImages.LoadIcon("left"),
                UiPalette.Primary,
                () => ShowPage(_page - 1));
            UiFactory.SetAnchoredRect(
                _prevButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(64f, 56f),
                new Vector2(-168f, 28f));

            _pageLabel = ui.CreateText(
                "Page",
                panel.transform,
                "1",
                36,
                FontStyle.Normal,
                Color.white,
                TextAnchor.MiddleCenter);
            UiFactory.SetAnchoredRect(
                _pageLabel.rectTransform,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(56f, 56f),
                new Vector2(-100f, 28f));
            UiFactory.AddDoubleOutline(_pageLabel.gameObject, new Vector2(3f, -3f), new Vector2(1.5f, -1.5f));

            _nextButton = UiFactory.CreateIconButton(
                panel.transform,
                "Next",
                GameImages.LoadIcon("right"),
                UiPalette.Primary,
                () => ShowPage(_page + 1));
            UiFactory.SetAnchoredRect(
                _nextButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(64f, 56f),
                new Vector2(-28f, 28f));

            ConfigureNavigation();
            _root.SetActive(false);
        }

        public void Open()
        {
            _fade.FadeIn();
            ShowPage(0);
            UiFactory.Select(_nextButton.gameObject.activeSelf ? _nextButton : _closeButton);
        }

        public void Close()
        {
            if (!_root.activeSelf)
            {
                return;
            }

            _fade.FadeOut();
            Closed?.Invoke();
        }

        private void ShowPage(int page)
        {
            _page = Mathf.Clamp(page, 0, PageCount - 1);

            int pageNumber = _page + 1;
            LocalizedText.Bind(_heading, $"ui.help.p{pageNumber}.heading");
            LocalizedText.Bind(_body, $"ui.help.p{pageNumber}.body");
            _pageLabel.text = pageNumber.ToString();

            _equipmentIcons.SetActive(_page == 1);

            bool hasPrev = _page > 0;
            bool hasNext = _page < PageCount - 1;
            _prevButton.gameObject.SetActive(hasPrev);
            _nextButton.gameObject.SetActive(hasNext);

            // Keep the page number tucked beside whichever arrows are visible.
            float pageX = hasNext ? -100f : (hasPrev ? -100f : -28f);
            if (!hasNext && hasPrev)
            {
                pageX = -28f;
                UiFactory.SetAnchoredRect(
                    _prevButton.GetComponent<RectTransform>(),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(64f, 56f),
                    new Vector2(-96f, 28f));
            }
            else if (hasPrev)
            {
                UiFactory.SetAnchoredRect(
                    _prevButton.GetComponent<RectTransform>(),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(64f, 56f),
                    new Vector2(-168f, 28f));
            }

            UiFactory.SetAnchoredRect(
                _pageLabel.rectTransform,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(56f, 56f),
                new Vector2(pageX, 28f));

            ConfigureNavigation();
        }

        private void ConfigureNavigation()
        {
            Button leftOfClose = null;
            Button rightOfClose = _prevButton.gameObject.activeSelf
                ? _prevButton
                : (_nextButton.gameObject.activeSelf ? _nextButton : null);

            SetHorizontal(_closeButton, leftOfClose, rightOfClose);

            if (_prevButton.gameObject.activeSelf)
            {
                SetHorizontal(
                    _prevButton,
                    _closeButton,
                    _nextButton.gameObject.activeSelf ? _nextButton : null);
            }

            if (_nextButton.gameObject.activeSelf)
            {
                SetHorizontal(
                    _nextButton,
                    _prevButton.gameObject.activeSelf ? _prevButton : _closeButton,
                    null);
            }
        }

        private static void SetHorizontal(Selectable selectable, Selectable left, Selectable right)
        {
            var navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnLeft = left;
            navigation.selectOnRight = right;
            navigation.selectOnUp = null;
            navigation.selectOnDown = null;
            selectable.navigation = navigation;
        }

        private static GameObject CreateEquipmentIcons(Transform panel)
        {
            var row = UiFactory.CreateRect("Equipment Icons", panel);
            UiFactory.SetAnchoredRect(
                row,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(420f, 64f),
                new Vector2(48f, 100f));
            UiFactory.AddHorizontalLayout(row.gameObject, 12f);

            string[] icons = { "+", "-", "x", "÷", "blank" };
            foreach (var iconName in icons)
            {
                var sprite = GameImages.LoadIcon(iconName);
                if (sprite == null)
                {
                    continue;
                }

                var image = UiFactory.CreateImage("Icon " + iconName, row, sprite);
                image.preserveAspect = true;
                UiFactory.SetLayoutSize(image.gameObject, 56f, 56f);
            }

            return row.gameObject;
        }

    }
}
