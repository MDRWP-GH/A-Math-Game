using AMath.Art;
using UnityEngine;
using UnityEngine.UI;

namespace AMath.UI.Tutorial
{
    /// <summary>
    /// Master Duel-style dim overlay with a cutout over one UI target,
    /// a pulsing ring, and an optional arrow. The shade is intentionally
    /// non-blocking so Replay Step and Skip Tutorial always remain available.
    /// </summary>
    internal sealed class TutorialSpotlightOverlay : MonoBehaviour
    {
        private const float Padding = 10f;
        private const float ArrowOffset = 18f;

        private Canvas _canvas;
        private RectTransform _root;
        private RectTransform _top;
        private RectTransform _bottom;
        private RectTransform _left;
        private RectTransform _right;
        private RectTransform _ring;
        private Text _arrow;
        private TutorialHandPointer _handPointer;
        private RectTransform _target;
        private bool _active;
        private bool _arrowAbove = true;
        private float _pulse;

        public void Initialize(Canvas canvas)
        {
            _canvas = canvas;
            _root = GetComponent<RectTransform>();
            if (_root == null)
                _root = gameObject.AddComponent<RectTransform>();

            UiFactory.Stretch(_root);
            _root.SetAsLastSibling();

            _top = CreatePanel("Top");
            _bottom = CreatePanel("Bottom");
            _left = CreatePanel("Left");
            _right = CreatePanel("Right");

            Image ringImage = UiFactory.CreateImage("Ring", _root, Color.clear);
            ringImage.raycastTarget = false;
            _ring = ringImage.rectTransform;
            UiFactory.AddOutline(ringImage.gameObject, UiPalette.CellGuide, new Vector2(2f, -2f));

            _arrow = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text))
                .GetComponent<Text>();
            _arrow.transform.SetParent(_root, false);
            _arrow.font = GameFonts.Jersey25;
            _arrow.text = "▼";
            _arrow.fontSize = 34;
            _arrow.alignment = TextAnchor.MiddleCenter;
            _arrow.color = UiPalette.CellGuide;
            _arrow.raycastTarget = false;
            UiFactory.AddOutline(_arrow.gameObject, Color.black, new Vector2(2f, -2f));

            var handObject = new GameObject("Tutorial Hand Pointer", typeof(RectTransform), typeof(TutorialHandPointer));
            handObject.transform.SetParent(_root, false);
            _handPointer = handObject.GetComponent<TutorialHandPointer>();
            _handPointer.Initialize(_root);

            gameObject.SetActive(false);
        }

        public void Show(RectTransform target, bool arrowAbove = true)
        {
            _target = target;
            _arrowAbove = arrowAbove;
            _active = target != null && target.gameObject.activeInHierarchy;
            gameObject.SetActive(_active);
            if (!_active) return;

            UpdateLayout(arrowAbove);
        }

        public void Hide()
        {
            _active = false;
            _target = null;
            _handPointer?.Hide();
            if (_ring != null)
            {
                _ring.SetParent(_root, false);
                _ring.gameObject.SetActive(false);
            }

            gameObject.SetActive(false);
        }

        public void ReplayPulse()
        {
            _pulse = 0f;
            if (_active && _target != null)
                UpdateLayout(_arrowAbove);
        }

        private void LateUpdate()
        {
            if (!_active || _target == null) return;
            if (!_target.gameObject.activeInHierarchy)
            {
                Hide();
                return;
            }

            _pulse += Time.deltaTime * 3f;
            float alpha = 0.82f + Mathf.Sin(_pulse) * 0.12f;
            if (_ring != null)
            {
                Image ring = _ring.GetComponent<Image>();
                ring.color = new Color(UiPalette.CellGuide.r, UiPalette.CellGuide.g, UiPalette.CellGuide.b, alpha);
            }

            UpdateLayout(_arrowAbove);
        }

        private void UpdateLayout(bool arrowAbove)
        {
            if (_target == null || _canvas == null) return;

            Vector3[] corners = new Vector3[4];
            _target.GetWorldCorners(corners);

            RectTransform canvasRect = _canvas.transform as RectTransform;
            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

            Rect hole = WorldRectToCanvasRect(corners, canvasRect, cam);
            hole.xMin -= Padding;
            hole.yMin -= Padding;
            hole.xMax += Padding;
            hole.yMax += Padding;

            Rect full = canvasRect.rect;
            hole = Rect.MinMaxRect(
                Mathf.Clamp(hole.xMin, full.xMin, full.xMax),
                Mathf.Clamp(hole.yMin, full.yMin, full.yMax),
                Mathf.Clamp(hole.xMax, full.xMin, full.xMax),
                Mathf.Clamp(hole.yMax, full.yMin, full.yMax));
            SetPanel(_top, new Rect(full.xMin, hole.yMax, full.width, full.yMax - hole.yMax), full);
            SetPanel(_bottom, new Rect(full.xMin, full.yMin, full.width, hole.yMin - full.yMin), full);
            SetPanel(_left, new Rect(full.xMin, hole.yMin, hole.xMin - full.xMin, hole.height), full);
            SetPanel(_right, new Rect(hole.xMax, hole.yMin, full.xMax - hole.xMax, hole.height), full);

            _ring.gameObject.SetActive(true);
            _ring.SetParent(_target, false);
            UiFactory.Stretch(_ring);
            _ring.SetAsFirstSibling();

            Vector2 holeCenter = hole.center;
            bool placeAbove = arrowAbove && hole.yMax + ArrowOffset + 70f <= full.yMax;
            if (!arrowAbove && hole.yMin - ArrowOffset - 70f < full.yMin)
                placeAbove = true;
            float arrowY = placeAbove
                ? hole.yMax + ArrowOffset
                : hole.yMin - ArrowOffset;
            _arrow.text = placeAbove ? "▼" : "▲";
            _arrow.rectTransform.anchoredPosition = new Vector2(holeCenter.x, arrowY);
            _arrow.rectTransform.sizeDelta = new Vector2(48f, 48f);

            if (_handPointer != null)
            {
                float handY = placeAbove
                    ? hole.yMax + ArrowOffset + 36f
                    : hole.yMin - ArrowOffset - 36f;
                _handPointer.ShowAt(new Vector2(holeCenter.x, handY), placeAbove);
            }
        }

        private RectTransform CreatePanel(string name)
        {
            Image panel = UiFactory.CreateImage(name, _root, UiPalette.Overlay);
            panel.raycastTarget = false;
            return panel.rectTransform;
        }

        private static void SetPanel(RectTransform panel, Rect rect, Rect canvasBounds)
        {
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.zero;
            panel.pivot = Vector2.zero;
            panel.anchoredPosition = ToBottomLeftAnchorPosition(rect, canvasBounds);
            panel.sizeDelta = new Vector2(Mathf.Max(0f, rect.width), Mathf.Max(0f, rect.height));
        }

        internal static Vector2 ToBottomLeftAnchorPosition(Rect rect, Rect canvasBounds) =>
            new(rect.x - canvasBounds.xMin, rect.y - canvasBounds.yMin);

        private static Rect WorldRectToCanvasRect(Vector3[] corners, RectTransform canvasRect, Camera cam)
        {
            Vector2 screenMin = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 screenMax = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenMin, cam, out Vector2 localMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenMax, cam, out Vector2 localMax);

            return Rect.MinMaxRect(
                Mathf.Min(localMin.x, localMax.x),
                Mathf.Min(localMin.y, localMax.y),
                Mathf.Max(localMin.x, localMax.x),
                Mathf.Max(localMin.y, localMax.y));
        }
    }
}
