using UnityEngine;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>
    /// Keeps a <see cref="CanvasScaler"/> usable on every screen size the player can pick.
    /// Matching height on wide screens and width on tall ones means the layout is letterboxed
    /// instead of cropped, so no control is ever pushed outside the visible area.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasScaler))]
    internal sealed class AdaptiveCanvasScaler : MonoBehaviour
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private CanvasScaler _scaler;
        private int _lastWidth;
        private int _lastHeight;

        private void OnEnable()
        {
            _lastWidth = 0;
            _lastHeight = 0;
            ApplyIfScreenChanged();
        }

        private void Update()
        {
            ApplyIfScreenChanged();
        }

        private void ApplyIfScreenChanged()
        {
            if (Screen.width == _lastWidth && Screen.height == _lastHeight)
            {
                return;
            }

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;

            if (_scaler == null)
            {
                _scaler = GetComponent<CanvasScaler>();
            }

            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = ReferenceResolution;
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            var referenceAspect = ReferenceResolution.x / ReferenceResolution.y;
            var currentAspect = _lastWidth / (float)Mathf.Max(1, _lastHeight);
            _scaler.matchWidthOrHeight = currentAspect >= referenceAspect ? 1f : 0f;
        }
    }
}
