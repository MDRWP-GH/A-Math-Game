using System.Collections;
using UnityEngine;

namespace AMath.UI
{
    /// <summary>
    /// Fades a full-screen overlay root in and out via CanvasGroup so open/close
    /// never hard-cuts against the forest backdrop.
    /// </summary>
    internal sealed class OverlayFade : MonoBehaviour
    {
        private const float DefaultDuration = 0.18f;

        private CanvasGroup _group;
        private Coroutine _running;
        private float _duration = DefaultDuration;

        public static OverlayFade Ensure(GameObject root)
        {
            if (root == null)
                return null;

            var fade = root.GetComponent<OverlayFade>();
            if (fade == null)
                fade = root.AddComponent<OverlayFade>();

            fade.EnsureGroup();
            return fade;
        }

        public void Configure(float duration)
        {
            _duration = Mathf.Max(0.01f, duration);
        }

        public void ShowInstant()
        {
            Stop();
            EnsureGroup();
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
        }

        public void HideInstant()
        {
            Stop();
            EnsureGroup();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        public void FadeIn()
        {
            Stop();
            EnsureGroup();
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            _group.alpha = 0f;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            _running = StartCoroutine(Animate(0f, 1f, deactivateAtEnd: false));
        }

        public void FadeOut()
        {
            Stop();
            EnsureGroup();
            if (!gameObject.activeSelf)
                return;
            _running = StartCoroutine(Animate(_group.alpha, 0f, deactivateAtEnd: true));
        }

        private IEnumerator Animate(float from, float to, bool deactivateAtEnd)
        {
            EnsureGroup();
            float elapsed = 0f;
            _group.alpha = from;
            while (elapsed < _duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _duration));
                _group.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            _group.alpha = to;
            _group.blocksRaycasts = to > 0.5f;
            _group.interactable = to > 0.5f;
            _running = null;
            if (deactivateAtEnd)
                gameObject.SetActive(false);
        }

        private void EnsureGroup()
        {
            if (_group != null)
                return;
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null)
                _group = gameObject.AddComponent<CanvasGroup>();
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
        }
    }
}
