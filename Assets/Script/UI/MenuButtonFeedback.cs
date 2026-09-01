using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>
    /// Soft hover / press feedback for outlined text menu buttons: tint the
    /// label and scale the hit box slightly without fighting Button colour tints
    /// (those stay transparent on text-only menus).
    /// </summary>
    internal sealed class MenuButtonFeedback : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        private const float HoverScale = 1.06f;
        private const float PressScale = 0.97f;
        private const float LerpSpeed = 14f;

        private Text _label;
        private RectTransform _rect;
        private Vector3 _baseScale = Vector3.one;
        private Color _normalColor = Color.white;
        private Color _hoverColor = UiPalette.HoverText;
        private Color _pressColor = UiPalette.PressedText;
        private bool _hovered;
        private bool _pressed;
        private bool _selected;
        private float _scale = 1f;
        private Color _color = Color.white;

        public static MenuButtonFeedback Attach(Button button, Text label = null)
        {
            if (button == null)
                return null;

            var feedback = button.gameObject.GetComponent<MenuButtonFeedback>();
            if (feedback == null)
                feedback = button.gameObject.AddComponent<MenuButtonFeedback>();

            feedback.Configure(label != null ? label : button.GetComponentInChildren<Text>());
            return feedback;
        }

        public void Configure(Text label)
        {
            _label = label;
            _rect = (RectTransform)transform;
            _baseScale = _rect.localScale;
            if (_label != null)
                _normalColor = _label.color;
            _color = _normalColor;
            _scale = 1f;
        }

        private void Update()
        {
            float targetScale = _pressed ? PressScale : (_hovered || _selected ? HoverScale : 1f);
            Color targetColor = _pressed
                ? _pressColor
                : (_hovered || _selected ? _hoverColor : _normalColor);

            float t = 1f - Mathf.Exp(-LerpSpeed * Time.unscaledDeltaTime);
            _scale = Mathf.Lerp(_scale, targetScale, t);
            _color = Color.Lerp(_color, targetColor, t);

            if (_rect != null)
                _rect.localScale = _baseScale * _scale;
            if (_label != null)
                _label.color = _color;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData) => _pressed = true;
        public void OnPointerUp(PointerEventData eventData) => _pressed = false;
        public void OnSelect(BaseEventData eventData) => _selected = true;
        public void OnDeselect(BaseEventData eventData) => _selected = false;

        private void OnDisable()
        {
            _hovered = false;
            _pressed = false;
            _selected = false;
            _scale = 1f;
            _color = _normalColor;
            if (_rect != null)
                _rect.localScale = _baseScale;
            if (_label != null)
                _label.color = _normalColor;
        }
    }
}
