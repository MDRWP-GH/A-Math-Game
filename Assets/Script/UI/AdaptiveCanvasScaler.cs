using AMath.Settings;
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
        private float _lastGuiScale;

        private void OnEnable()
        {
            _lastWidth = 0;
            _lastHeight = 0;
            _lastGuiScale = 0f;
            DisplaySettings.Changed += Invalidate;
            ApplyIfScreenChanged();
        }

        private void OnDisable()
        {
            DisplaySettings.Changed -= Invalidate;
        }

        private void Update()
        {
            ApplyIfScreenChanged();
        }

        private void Invalidate()
        {
            _lastWidth = 0;
            _lastHeight = 0;
        }

        private void ApplyIfScreenChanged()
        {
            var width = Screen.width;
            var height = Screen.height;
            DisplaySettings.EnsureGuiScaleFits(width, height);
            var guiScale = DisplaySettings.GuiScale;

            if (width == _lastWidth &&
                height == _lastHeight &&
                Mathf.Approximately(guiScale, _lastGuiScale))
            {
                return;
            }

            _lastWidth = width;
            _lastHeight = height;
            _lastGuiScale = guiScale;

            if (_scaler == null)
            {
                _scaler = GetComponent<CanvasScaler>();
            }

            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = ReferenceResolution / guiScale;
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            var referenceAspect = ReferenceResolution.x / ReferenceResolution.y;
            var currentAspect = width / (float)Mathf.Max(1, height);
            _scaler.matchWidthOrHeight = currentAspect >= referenceAspect ? 1f : 0f;
        }
    }
}
