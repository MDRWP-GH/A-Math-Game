using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AMath.UI
{
    /// <summary>
    /// Fades a list of menu entries in one after another, so the screen builds
    /// itself up instead of appearing all at once.
    ///
    /// Each target gets its own <see cref="CanvasGroup"/>, which keeps the fade
    /// independent of the button colour tints that drive hover and selection
    /// feedback. Timing runs on unscaled time so a paused game still animates.
    /// </summary>
    internal sealed class MenuEntranceAnimator : MonoBehaviour
    {
        private const float DefaultFadeDuration = 0.34f;
        private const float DefaultStagger = 0.08f;
        private const float DefaultStartDelay = 0.05f;

        private readonly List<CanvasGroup> _targets = new();
        private readonly List<Vector3> _targetScales = new();

        private Coroutine _running;
        private float _fadeDuration = DefaultFadeDuration;
        private float _stagger = DefaultStagger;
        private float _startDelay = DefaultStartDelay;

        /// <summary>True while entries are still fading in.</summary>
        public bool IsPlaying => _running != null;

        /// <summary>
        /// Registers the entries to animate, in the order they should appear.
        /// Nulls are skipped so a caller may pass optional buttons directly.
        /// </summary>
        public void SetTargets(params Component[] targets)
        {
            _targets.Clear();
            _targetScales.Clear();
            if (targets == null)
                return;

            foreach (Component target in targets)
            {
                if (target == null)
                    continue;

                var group = target.gameObject.GetComponent<CanvasGroup>();
                if (group == null)
                    group = target.gameObject.AddComponent<CanvasGroup>();

                _targets.Add(group);
                _targetScales.Add(group.transform.localScale);
            }
        }

        public void Configure(float fadeDuration, float stagger, float startDelay)
        {
            _fadeDuration = Mathf.Max(0f, fadeDuration);
            _stagger = Mathf.Max(0f, stagger);
            _startDelay = Mathf.Max(0f, startDelay);
        }

        /// <summary>Restarts the entrance from fully transparent.</summary>
        public void Play()
        {
            if (!isActiveAndEnabled || _targets.Count == 0)
                return;

            Stop();

            for (int i = 0; i < _targets.Count; i++)
                SetVisible(i, 0f);

            _running = StartCoroutine(RunEntrance());
        }

        /// <summary>Jumps straight to the finished state.</summary>
        public void Skip()
        {
            Stop();
            RevealAll();
        }

        private IEnumerator RunEntrance()
        {
            if (_startDelay > 0f)
                yield return new WaitForSecondsRealtime(_startDelay);

            // Every entry starts its own fade on a staggered clock, so a slow
            // fade can overlap the next entry instead of appearing strictly
            // one-at-a-time.
            float elapsed = 0f;
            float total = _stagger * (_targets.Count - 1) + _fadeDuration;

            while (elapsed < total)
            {
                for (int i = 0; i < _targets.Count; i++)
                    SetVisible(i, FadeProgress(elapsed - i * _stagger));

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _running = null;
            RevealAll();
        }

        private float FadeProgress(float localElapsed)
        {
            if (localElapsed <= 0f)
                return 0f;
            if (_fadeDuration <= 0f || localElapsed >= _fadeDuration)
                return 1f;

            // Smoothstep reads softer than a straight ramp at these durations.
            return Mathf.SmoothStep(0f, 1f, localElapsed / _fadeDuration);
        }

        private void Stop()
        {
            if (_running == null)
                return;

            StopCoroutine(_running);
            _running = null;
        }

        private void RevealAll()
        {
            for (int i = 0; i < _targets.Count; i++)
                SetVisible(i, 1f);
        }

        private void SetVisible(int index, float alpha)
        {
            CanvasGroup group = _targets[index];
            if (group == null)
                return;

            group.alpha = alpha;
            group.transform.localScale = _targetScales[index] * Mathf.Lerp(0.975f, 1f, alpha);

            // Only the barely visible part of the entrance ignores pointer input.
            // Once the control can be seen, users need not wait for the fade to finish.
            group.blocksRaycasts = alpha >= 0.25f;
        }

        /// <summary>
        /// A menu that is hidden mid-fade must not come back half-transparent,
        /// so being switched off always resolves to the finished state.
        /// </summary>
        private void OnDisable()
        {
            Stop();
            RevealAll();
        }
    }
}
