using System.Collections;
using UnityEngine;

namespace AMath.UI
{
    /// <summary>
    /// Slides and fades the in-match command drawer without requiring a tween
    /// package. A new transition starts from the current visual state, so
    /// repeatedly pressing the toggle never snaps the drawer.
    /// </summary>
    internal sealed class MatchCommandDrawerAnimator : MonoBehaviour
    {
        private const float CollapsedScale = 0.96f;

        private RectTransform _rect;
        private CanvasGroup _group;
        private Coroutine _running;
        private Vector2 _expandedPosition;
        private Vector2 _collapsedPosition;
        private float _duration = 0.30f;

        public bool IsExpanded { get; private set; }
        public bool IsAnimating => _running != null;
        public CanvasGroup Group
        {
            get
            {
                EnsureComponents();
                return _group;
            }
        }

        public void Configure(Vector2 expandedPosition, Vector2 collapsedPosition, float duration)
        {
            EnsureComponents();
            _expandedPosition = expandedPosition;
            _collapsedPosition = collapsedPosition;
            _duration = Mathf.Max(0.01f, duration);
        }

        public void Toggle() => SetExpanded(!IsExpanded);

        public void SetExpanded(bool expanded)
        {
            EnsureComponents();
            Stop();
            IsExpanded = expanded;

            // No command receives input while it is partly hidden. The toggle
            // remains outside this CanvasGroup and can still reverse the motion.
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _running = StartCoroutine(Animate(expanded));
        }

        public void SetExpandedInstant(bool expanded)
        {
            EnsureComponents();
            Stop();
            IsExpanded = expanded;
            ApplyVisual(expanded ? 1f : 0f);
            _group.blocksRaycasts = expanded;
            _group.interactable = expanded;
        }

        private IEnumerator Animate(bool expanding)
        {
            Vector2 fromPosition = _rect.anchoredPosition;
            Vector2 toPosition = expanding ? _expandedPosition : _collapsedPosition;
            float fromAlpha = _group.alpha;
            float toAlpha = expanding ? 1f : 0f;
            Vector3 fromScale = _rect.localScale;
            Vector3 toScale = Vector3.one * (expanding ? 1f : CollapsedScale);
            float elapsed = 0f;

            while (elapsed < _duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _duration));
                _rect.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, t);
                _group.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, t);
                _rect.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
                yield return null;
            }

            _running = null;
            ApplyVisual(expanding ? 1f : 0f);
            _group.blocksRaycasts = expanding;
            _group.interactable = expanding;
        }

        private void ApplyVisual(float expandedAmount)
        {
            _rect.anchoredPosition = Vector2.Lerp(_collapsedPosition, _expandedPosition, expandedAmount);
            _group.alpha = expandedAmount;
            _rect.localScale = Vector3.one * Mathf.Lerp(CollapsedScale, 1f, expandedAmount);
        }

        private void EnsureComponents()
        {
            if (_rect == null)
                _rect = transform as RectTransform;
            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
                if (_group == null)
                    _group = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void Stop()
        {
            if (_running == null)
                return;

            StopCoroutine(_running);
            _running = null;
        }

        private void OnDisable()
        {
            Stop();
            IsExpanded = false;
            if (_rect == null || _group == null)
                return;

            ApplyVisual(0f);
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }
    }
}
