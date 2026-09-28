using System;
using System.Runtime.CompilerServices;
using AMath.Art;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("AMath.Tests.EditMode")]
[assembly: InternalsVisibleTo("AMath.Tests.PlayMode")]

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

        public UiFactory(Font font = null)
        {
            _font = font != null ? font : GameFonts.Jersey25;
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
            image.sprite = PixelButtonSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 0.5f;
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

            // Jersey 25 is a single-weight display face; keep labels as Text (not sprites)
            // and avoid synthetic Bold so glyphs stay crisp.
            var buttonText = CreateText("Label", buttonObject.transform, label, fontSize, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);
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

            var buttonText = CreateText("Label", buttonObject.transform, label, fontSize, FontStyle.Normal, UiPalette.LightText, alignment);
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

        /// <summary>
        /// Creates a full-screen background from the named resource, preserving its aspect ratio
        /// while filling its parent. Uses <paramref name="fallbackColor"/> when the asset is absent.
        /// </summary>
        public static Image CreateFullScreenBackground(Transform parent, string resourceName, Color fallbackColor)
        {
            var sprite = GameImages.LoadBackground(resourceName);
            var background = sprite != null
                ? CreateImage("Background", parent, sprite)
                : CreateImage("Background", parent, fallbackColor);
            Stretch(background.rectTransform);

            if (sprite != null && sprite.rect.height > 0f)
            {
                var fitter = background.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            }

            BackgroundMotion.TryAttach(background, resourceName);

            return background;
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

        public static void AddOutline(GameObject target, Color color, Vector2 distance)
        {
            var outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        /// <summary>Adds the paired black outline used by the code-driven screens.</summary>
        public static void AddDoubleOutline(
            GameObject target,
            Vector2 outerDistance,
            Vector2 innerDistance)
        {
            AddOutline(target, Color.black, outerDistance);
            AddOutline(target, Color.black, innerDistance);
        }

        /// <summary>Soft circle sprite for avatar badges (cached).</summary>
        public static Sprite CircleSprite
        {
            get
            {
                if (_circleSprite != null)
                    return _circleSprite;

                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                float radius = (size - 1) * 0.5f;
                float radiusSq = radius * radius;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - radius;
                        float dy = y - radius;
                        texture.SetPixel(x, y, dx * dx + dy * dy <= radiusSq ? Color.white : Color.clear);
                    }
                }

                texture.Apply(false, true);
                _circleSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f),
                    100f);
                return _circleSprite;
            }
        }

        private static Sprite _circleSprite;

        /// <summary>A cached, point-filtered nine-slice with stepped corners and a pixel bevel.</summary>
        internal static Sprite PixelButtonSprite
        {
            get
            {
                if (_pixelButtonSprite != null)
                    return _pixelButtonSprite;

                const int size = 24;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                for (int y = 0; y < size; y++)
                {
                    int rowInset = y == 0 || y == size - 1 ? 6
                        : y < 3 || y >= size - 3 ? 3
                        : y < 6 || y >= size - 6 ? 1 : 0;
                    for (int x = 0; x < size; x++)
                    {
                        if (x < rowInset || x >= size - rowInset)
                        {
                            texture.SetPixel(x, y, Color.clear);
                            continue;
                        }

                        bool edge = x <= rowInset + 1 || x >= size - rowInset - 2
                            || y <= 1 || y >= size - 2;
                        float shade = edge
                            ? (y < size / 2 && x < size - rowInset - 2 ? 0.88f : 0.56f)
                            : (y == 3 ? 0.94f : 1f);
                        texture.SetPixel(x, y, new Color(shade, shade, shade, 1f));
                    }
                }

                texture.Apply(false, true);
                _pixelButtonSprite = Sprite.Create(
                    texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, new Vector4(7f, 7f, 7f, 7f));
                return _pixelButtonSprite;
            }
        }

        private static Sprite _pixelButtonSprite;

        /// <summary>Keeps an option visibly selected independent of Button hover or keyboard focus.</summary>
        internal static void SetChoiceSelected(Button button, bool selected)
        {
            if (button == null)
                return;

            Transform existing = button.transform.Find("Selected Fill");
            if (existing == null && !selected)
                return;

            Image fill = existing != null
                ? existing.GetComponent<Image>()
                : CreateImage("Selected Fill", button.transform, PixelButtonSprite, UiPalette.Primary);
            if (existing == null)
            {
                fill.type = Image.Type.Sliced;
                fill.pixelsPerUnitMultiplier = 0.5f;
                fill.raycastTarget = false;
                Stretch(fill.rectTransform);
                fill.transform.SetAsFirstSibling();
            }
            fill.gameObject.SetActive(selected);
        }

        /// <summary>Creates an icon-only button with the standard hover treatment.</summary>
        public static Button CreateIconButton(
            Transform parent,
            string name,
            Sprite sprite,
            Color fallbackColor,
            Action onClick)
        {
            var buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = sprite != null;
            image.color = sprite != null ? Color.white : fallbackColor;
            image.raycastTarget = true;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1f, 0.92f, 0.75f, 1f),
                pressedColor = new Color(0.90f, 0.85f, 0.70f, 1f),
                selectedColor = new Color(1f, 0.92f, 0.75f, 1f),
                disabledColor = new Color(1f, 1f, 1f, 0.35f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            button.onClick.AddListener(onClick.Invoke);
            return button;
        }

        /// <summary>
        /// Text-only menu button: invisible hit box, white label with black outline
        /// and soft hover / press feedback.
        /// </summary>
        public Button CreateTextMenuButton(Transform parent, string name, string label, int fontSize, Action onClick)
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

            var buttonText = CreateText("Label", buttonObject.transform, label, fontSize, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);
            Stretch(buttonText.rectTransform);
            AddOutline(buttonText.gameObject, Color.black, new Vector2(3.5f, -3.5f));
            AddOutline(buttonText.gameObject, Color.black, new Vector2(1.5f, -1.5f));
            MenuButtonFeedback.Attach(button, buttonText);

            return button;
        }

        /// <summary>
        /// Small pill accent used for Back / Play style actions on mockup screens.
        /// </summary>
        public Button CreateAccentButton(
            Transform parent,
            string name,
            string label,
            Color normalColor,
            Color highlightedColor,
            Action onClick,
            int fontSize = 28)
        {
            var button = CreateButton(parent, name, label, normalColor, highlightedColor, onClick, fontSize);
            AddOutline(button.GetComponentInChildren<Text>().gameObject, Color.black, new Vector2(2f, -2f));
            return button;
        }

        /// <summary>Large outlined title shared by menu / overlay screens.</summary>
        public Text CreateOutlinedTitle(
            Transform parent,
            string name,
            string value,
            int fontSize,
            TextAnchor alignment = TextAnchor.MiddleCenter,
            Color? color = null)
        {
            var title = CreateText(name, parent, value, fontSize, FontStyle.Normal, color ?? Color.white, alignment);
            AddDoubleOutline(title.gameObject, new Vector2(4f, -4f), new Vector2(2f, -2f));
            return title;
        }

        /// <summary>Translucent glass panel with a soft drop shadow.</summary>
        public static Image CreateGlassPanel(Transform parent, string name, Color? color = null)
        {
            var panel = CreateImage(name, parent, color ?? UiPalette.Glass);
            panel.raycastTarget = true;
            AddShadow(panel.gameObject, new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, -8f));
            return panel;
        }

        /// <summary>
        /// Full-screen overlay root: dim catcher, optional glass card, and a fade helper.
        /// </summary>
        public static OverlayShell CreateOverlayShell(
            Transform parent,
            string name,
            bool includeGlassCard = true,
            Vector2? cardSize = null,
            Color? glassColor = null)
        {
            var root = CreateRect(name, parent).gameObject;
            Stretch(root.GetComponent<RectTransform>());

            var catcher = CreateImage("Catcher", root.transform, UiPalette.Overlay);
            catcher.raycastTarget = true;
            Stretch(catcher.rectTransform);

            Image card = null;
            if (includeGlassCard)
            {
                card = CreateGlassPanel(root.transform, "Panel", glassColor ?? UiPalette.GlassStrong);
                SetCenteredRect(card.rectTransform, Vector2.zero, cardSize ?? new Vector2(900f, 720f));
            }

            var fade = OverlayFade.Ensure(root);
            root.SetActive(false);

            return new OverlayShell(root, catcher, card, fade);
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

        /// <summary>Selects the first visible, enabled candidate for keyboard/gamepad navigation.</summary>
        public static Selectable SelectFirstInteractable(params Selectable[] candidates)
        {
            if (candidates == null)
                return null;

            foreach (Selectable candidate in candidates)
            {
                if (candidate == null || !candidate.IsActive() || !candidate.IsInteractable())
                    continue;

                Select(candidate);
                return candidate;
            }

            return null;
        }

        /// <summary>Returns the project's UI Cancel action when one is configured.</summary>
        public static InputAction FindCancelAction() =>
            InputSystem.actions?.FindAction("UI/Cancel", false);

        /// <summary>Detects Cancel with Escape as a keyboard fallback.</summary>
        public static bool WasCancelPressed(InputAction cancelAction) =>
            (cancelAction != null && cancelAction.enabled && cancelAction.WasPressedThisFrame()) ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);

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
