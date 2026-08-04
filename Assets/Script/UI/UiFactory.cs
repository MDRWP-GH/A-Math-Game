using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>
    /// Builders for the code-driven screens. Keeping them together lets the menu and the
    /// settings screen share a look without prefabs or scene wiring.
    /// </summary>
    internal sealed class UiFactory
    {
        private const float SliderHandleSize = 34f;

        private readonly Font _font;

        public UiFactory(Font font)
        {
            _font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public Font Font => _font;

        public Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            canvasObject.AddComponent<AdaptiveCanvasScaler>();

            return canvas;
        }

        public Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle style, Color color, TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);

            var text = textObject.GetComponent<Text>();
            text.font = _font;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            return text;
        }

        public Button CreateButton(Transform parent, string name, string label, Color normalColor, Color highlightedColor, Action onClick, int fontSize = 31)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.color = Color.white;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = normalColor,
                highlightedColor = highlightedColor,
                pressedColor = Color.Lerp(normalColor, Color.black, 0.16f),
                selectedColor = highlightedColor,
                disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f),
                colorMultiplier = 1f,
                fadeDuration = 0.12f
            };
            button.onClick.AddListener(onClick.Invoke);
            AddShadow(buttonObject, new Color(0f, 0f, 0f, 0.20f), new Vector2(0f, -5f));

            var buttonText = CreateText("Label", buttonObject.transform, label, fontSize, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(buttonText.rectTransform);

            return button;
        }

        /// <summary>
        /// A button that shows only its label until it is hovered or selected.
        /// </summary>
        public Button CreateFlatButton(Transform parent, string name, string label, int fontSize, TextAnchor alignment, Action onClick)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.color = Color.white;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = new Color(1f, 1f, 1f, 0f),
                highlightedColor = new Color(1f, 1f, 1f, 0.12f),
                pressedColor = new Color(1f, 1f, 1f, 0.22f),
                selectedColor = new Color(1f, 1f, 1f, 0.12f),
                disabledColor = new Color(1f, 1f, 1f, 0f),
                colorMultiplier = 1f,
                fadeDuration = 0.1f
            };
            button.onClick.AddListener(onClick.Invoke);

            var buttonText = CreateText("Label", buttonObject.transform, label, fontSize, FontStyle.Bold, UiPalette.LightText, alignment);
            SetStretchRect(buttonText.rectTransform, 20f, 0f, 20f, 0f);

            return button;
        }

        public Slider CreateSlider(Transform parent, string name, float value)
        {
            var sliderObject = new GameObject(name, typeof(RectTransform));
            sliderObject.transform.SetParent(parent, false);
            sliderObject.SetActive(false);

            var slider = sliderObject.AddComponent<Slider>();

            var background = CreateImage("Background", sliderObject.transform, UiPalette.Track);
            background.raycastTarget = true;
            SetHorizontalBar(background.rectTransform, 10f);

            var fillArea = CreateRect("Fill Area", sliderObject.transform);
            SetHorizontalBar(fillArea, 10f);
            fillArea.sizeDelta = new Vector2(-SliderHandleSize, 10f);

            var fill = CreateImage("Fill", fillArea, UiPalette.Primary);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            fill.rectTransform.sizeDelta = new Vector2(SliderHandleSize, 0f);

            var handleArea = CreateRect("Handle Slide Area", sliderObject.transform);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.pivot = new Vector2(0.5f, 0.5f);
            handleArea.sizeDelta = new Vector2(-SliderHandleSize, 0f);
            handleArea.anchoredPosition = Vector2.zero;

            var handle = CreateImage("Handle", handleArea, Color.white);
            handle.raycastTarget = true;
            handle.rectTransform.anchorMin = Vector2.zero;
            handle.rectTransform.anchorMax = new Vector2(0f, 1f);
            handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(SliderHandleSize, 0f);

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
                normalColor = new Color(0.90f, 0.94f, 1f, 1f),
                highlightedColor = Color.white,
                pressedColor = UiPalette.PrimaryHighlight,
                selectedColor = Color.white,
                disabledColor = new Color(0.5f, 0.5f, 0.5f, 1f),
                colorMultiplier = 1f,
                fadeDuration = 0.1f
            };

            sliderObject.SetActive(true);
            return slider;
        }

        public InputField CreateInputField(Transform parent, string name, string value, string placeholder, int fontSize = 32)
        {
            var fieldObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fieldObject.transform.SetParent(parent, false);
            fieldObject.SetActive(false);

            var background = fieldObject.GetComponent<Image>();
            background.color = Color.white;

            var text = CreateText("Text", fieldObject.transform, value, fontSize, FontStyle.Normal, UiPalette.DarkText, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            SetStretchRect(text.rectTransform, 16f, 4f, 16f, 4f);

            var hint = CreateText("Placeholder", fieldObject.transform, placeholder, fontSize, FontStyle.Italic, UiPalette.HintText, TextAnchor.MiddleLeft);
            hint.supportRichText = false;
            SetStretchRect(hint.rectTransform, 16f, 4f, 16f, 4f);

            var field = fieldObject.AddComponent<InputField>();
            field.targetGraphic = background;
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = 24;
            field.text = value;
            field.colors = new ColorBlock
            {
                normalColor = new Color(0.82f, 0.86f, 0.92f, 1f),
                highlightedColor = new Color(0.92f, 0.95f, 1f, 1f),
                pressedColor = new Color(0.74f, 0.79f, 0.88f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.5f, 0.5f, 0.5f, 1f),
                colorMultiplier = 1f,
                fadeDuration = 0.1f
            };

            fieldObject.SetActive(true);
            return field;
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            var image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image CreateImage(string name, Transform parent, Sprite sprite, Color? tint = null)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            var image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = tint ?? Color.white;
            image.raycastTarget = false;
            return image;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var rectObject = new GameObject(name, typeof(RectTransform));
            rectObject.transform.SetParent(parent, false);
            return (RectTransform)rectObject.transform;
        }

        public static void Stretch(RectTransform rectTransform)
        {
            SetStretchRect(rectTransform, 0f, 0f, 0f, 0f);
        }

        public static void SetStretchRect(RectTransform rectTransform, float left, float top, float right, float bottom)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = new Vector2(left, bottom);
            rectTransform.offsetMax = new Vector2(-right, -top);
        }

        public static void SetCenteredRect(RectTransform rectTransform, Vector2 position, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = position;
        }

        public static void SetAnchoredRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = position;
        }

        /// <summary>
        /// Places a rect using pixels measured from the top-left corner of its parent, which reads
        /// closer to the mock-up than Unity's centre-relative anchored positions.
        /// </summary>
        public static void SetTopLeftRect(RectTransform rectTransform, Vector2 offsetFromTopLeft, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = new Vector2(offsetFromTopLeft.x, -offsetFromTopLeft.y);
        }

        /// <summary>
        /// Stacks children downwards, so a list of rows never has to know its own row pitch.
        /// </summary>
        public static VerticalLayoutGroup AddVerticalLayout(GameObject target, float spacing, RectOffset padding = null)
        {
            var layout = target.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(layout, spacing, padding, TextAnchor.UpperLeft);

            // Rows should span the width they are given rather than shrink to their contents.
            layout.childForceExpandWidth = true;
            return layout;
        }

        /// <summary>
        /// Places children left to right and centres them vertically, so a row only has to
        /// describe widths.
        /// </summary>
        public static HorizontalLayoutGroup AddHorizontalLayout(GameObject target, float spacing, RectOffset padding = null)
        {
            var layout = target.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(layout, spacing, padding, TextAnchor.MiddleLeft);
            return layout;
        }

        /// <summary>
        /// Tells the parent layout group how much room this element wants. A flexible width above
        /// zero lets it absorb whatever the fixed elements in the same group leave over.
        /// </summary>
        public static LayoutElement SetLayoutSize(GameObject target, float preferredWidth, float preferredHeight, float flexibleWidth = 0f)
        {
            var element = target.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = target.AddComponent<LayoutElement>();
            }

            element.preferredWidth = preferredWidth;
            element.preferredHeight = preferredHeight;
            element.flexibleWidth = flexibleWidth;
            element.flexibleHeight = 0f;
            return element;
        }

        public static void AddShadow(GameObject target, Color color, Vector2 distance)
        {
            var shadow = target.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
        }

        public static void SetVerticalNavigation(Selectable selectable, Selectable up, Selectable down)
        {
            var navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;
            navigation.selectOnLeft = null;
            navigation.selectOnRight = null;
            selectable.navigation = navigation;
        }

        public static void Select(Selectable selectable)
        {
            if (selectable != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }
        }

        private static void ConfigureLayout(HorizontalOrVerticalLayoutGroup layout, float spacing, RectOffset padding, TextAnchor alignment)
        {
            layout.spacing = spacing;
            layout.childAlignment = alignment;

            // Sizes come from each child's LayoutElement, so the group may resize children but
            // must not stretch or scale them beyond what they asked for.
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childScaleWidth = false;
            layout.childScaleHeight = false;

            if (padding != null)
            {
                layout.padding = padding;
            }
        }

        private static void SetHorizontalBar(RectTransform rectTransform, float height)
        {
            rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rectTransform.anchorMax = new Vector2(1f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(0f, height);
            rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
