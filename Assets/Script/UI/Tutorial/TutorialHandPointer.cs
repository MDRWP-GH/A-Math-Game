using AMath.Art;
using UnityEngine;
using UnityEngine.UI;

namespace AMath.UI.Tutorial
{
    /// <summary>
    /// Animated hand cursor that bounces toward a spotlight target, Master Duel-style.
    /// </summary>
    internal sealed class TutorialHandPointer : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _hand;
        private Text _emojiHand;
        private Image _spriteHand;
        private bool _active;
        private float _phase;
        private Vector2 _basePosition;
        private bool _pointDown;

        public void Initialize(Transform parent)
        {
            _root = GetComponent<RectTransform>();
            if (_root == null)
                _root = gameObject.AddComponent<RectTransform>();

            transform.SetParent(parent, false);
            UiFactory.Stretch(_root);

            Sprite pointerSprite = TileIcons.LoadIcon("left");
            if (pointerSprite != null)
            {
                _spriteHand = UiFactory.CreateImage("Hand Sprite", _root, pointerSprite);
                _spriteHand.preserveAspect = true;
                _spriteHand.raycastTarget = false;
                _hand = _spriteHand.rectTransform;
                _hand.sizeDelta = new Vector2(72f, 72f);
                _hand.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            else
            {
                _emojiHand = new GameObject("Hand Emoji", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text))
                    .GetComponent<Text>();
                _emojiHand.transform.SetParent(_root, false);
                _emojiHand.font = GameFonts.Jersey25;
                _emojiHand.text = "👆";
                _emojiHand.fontSize = 52;
                _emojiHand.alignment = TextAnchor.MiddleCenter;
                _emojiHand.raycastTarget = false;
                _hand = _emojiHand.rectTransform;
                _hand.sizeDelta = new Vector2(64f, 64f);
            }

            gameObject.SetActive(false);
        }

        public void ShowAt(Vector2 anchoredPosition, bool pointDown = true)
        {
            bool restart = !_active || _pointDown != pointDown;
            _active = true;
            _pointDown = pointDown;
            _basePosition = anchoredPosition;
            if (restart)
                _phase = 0f;
            gameObject.SetActive(true);
            if (_hand != null)
            {
                _hand.anchoredPosition = anchoredPosition;
                _hand.localScale = Vector3.one;
            }
        }

        public void Hide()
        {
            _active = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_active || _hand == null)
                return;

            _phase += Time.deltaTime * 4f;
            float bounce = Mathf.Sin(_phase) * 14f;
            float tilt = Mathf.Sin(_phase * 0.5f) * 6f;
            float scale = 1f + Mathf.Sin(_phase * 2f) * 0.06f;

            Vector2 offset = _pointDown ? new Vector2(0f, bounce) : new Vector2(0f, -bounce);
            _hand.anchoredPosition = _basePosition + offset;
            _hand.localScale = Vector3.one * scale;
            _hand.localRotation = Quaternion.Euler(0f, 0f, (_pointDown ? 90f : -90f) + tilt);
        }
    }
}
