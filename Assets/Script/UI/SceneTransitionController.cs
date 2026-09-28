using System.Collections;
using AMath.Art;
using AMath.UI.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>
    /// Owns the one loading surface used by the game.  Keeping scene changes here prevents a
    /// second click from starting another load and makes the visual hand-off between scenes
    /// consistent.
    /// </summary>
    public sealed class SceneTransitionController : MonoBehaviour
    {
        private const float MinimumVisibleSeconds = 0.35f;
        private const float FadeInSeconds = 0.14f;
        private const float FadeOutSeconds = 0.42f;
        private const int SortingOrder = 10000;

        private static SceneTransitionController _instance;

        internal static event System.Action DestinationRevealStarted;

        private CanvasGroup _group;
        private Slider _progressBar;
        private RectTransform _loadingContent;
        private RectTransform _progressBarTransform;
        private Text _loadingTitle;
        private Text _status;
        private Coroutine _running;
        private Coroutine _fade;
        private GameObject _focusBeforeLoad;
        private bool _isLoading;
        private float _shownAt;

        private void Update()
        {
            if (_group == null || _group.alpha <= 0f)
                return;

            float time = Time.unscaledTime;
            if (_loadingTitle != null)
            {
                int dots = 1 + Mathf.FloorToInt(time * 2.2f) % 3;
                _loadingTitle.text = "Loading" + new string('.', dots);
                _loadingTitle.rectTransform.anchoredPosition = new Vector2(0f, 70f + Mathf.Sin(time * 3.2f) * 7f);
            }

            // The small lift and squash makes the loading bar feel alive without
            // obscuring a real progress value or looking like a conventional spinner.
            if (_progressBarTransform != null)
            {
                float bounce = Mathf.Sin(time * 5.1f);
                _progressBarTransform.anchoredPosition = new Vector2(0f, -100f + bounce * 3f);
                _progressBarTransform.localScale = new Vector3(1f + bounce * 0.012f, 1f - bounce * 0.09f, 1f);
            }
        }

        /// <summary>True while a scene request is in flight. Used to reject duplicate actions.</summary>
        public static bool IsTransitioning => _instance != null && _instance._isLoading;

        /// <summary>Creates the persistent loading surface before the first scene begins.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateAtStartup()
        {
            EnsureExists().Show("ui.loading.startup", 0f);
        }

        /// <summary>Loads a scene by name, returning false when it is unavailable or already loading.</summary>
        public static bool LoadScene(string sceneName, string statusKey = "ui.loading.scene")
        {
            if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"A-Math: cannot load scene '{sceneName}'. It is not in Build Settings.");
                _instance?.CancelAndRestoreFocus();
                return false;
            }

            return EnsureExists().BeginLoad(sceneName, statusKey);
        }

        /// <summary>Loads a scene by build index, returning false when it is unavailable or already loading.</summary>
        public static bool LoadScene(int buildIndex, string statusKey = "ui.loading.scene")
        {
            if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError($"A-Math: cannot load build index {buildIndex}. It is not in Build Settings.");
                _instance?.CancelAndRestoreFocus();
                return false;
            }

            return EnsureExists().BeginLoad(buildIndex, statusKey);
        }

        /// <summary>Updates startup feedback while account and menu services are being prepared.</summary>
        public static void SetStartupStatus(string statusKey)
        {
            SceneTransitionController controller = EnsureExists();
            if (!controller._isLoading)
                controller.Show(statusKey, controller._progressBar == null ? 0f : controller._progressBar.value);
        }

        /// <summary>Dismisses the startup surface after Login or Main Menu has built its first focus target.</summary>
        public static void NotifyStartupReady()
        {
            if (_instance != null && !_instance._isLoading)
                _instance.FinishVisiblePeriod();
        }

        internal static bool IsLoadableScene(string sceneName) =>
            !string.IsNullOrWhiteSpace(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);

        private static SceneTransitionController EnsureExists()
        {
            if (_instance != null)
                return _instance;

            var root = new GameObject("Scene Transition Controller");
            _instance = root.AddComponent<SceneTransitionController>();
            DontDestroyOnLoad(root);
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildOverlay();
        }

        private bool BeginLoad(string sceneName, string statusKey)
        {
            if (_isLoading)
                return false;

            _running = StartCoroutine(LoadRoutine(() => SceneManager.LoadSceneAsync(sceneName), statusKey));
            return true;
        }

        private bool BeginLoad(int buildIndex, string statusKey)
        {
            if (_isLoading)
                return false;

            _running = StartCoroutine(LoadRoutine(() => SceneManager.LoadSceneAsync(buildIndex), statusKey));
            return true;
        }

        private IEnumerator LoadRoutine(System.Func<AsyncOperation> createOperation, string statusKey)
        {
            _isLoading = true;
            _focusBeforeLoad = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            Show(statusKey, 0f);

            AsyncOperation operation = createOperation();
            if (operation == null)
            {
                Debug.LogError("A-Math: Unity did not create the requested scene load operation.");
                CancelAndRestoreFocus();
                yield break;
            }

            while (!operation.isDone)
            {
                // Unity reports 0..0.9 until activation; presenting 0..1 is clearer to players.
                SetProgress(Mathf.Clamp01(operation.progress / 0.9f));
                yield return null;
            }

            SetProgress(1f);
            SetStatus("ui.loading.ready");
            yield return null; // let the destination scene create its first frame
            yield return WaitForMinimumVisiblePeriod();
            DestinationRevealStarted?.Invoke();
            yield return HideRoutine();
            _isLoading = false;
            _running = null;
        }

        private void FinishVisiblePeriod()
        {
            if (_running != null)
                StopCoroutine(_running);
            _running = StartCoroutine(HideAfterMinimumVisiblePeriod());
        }

        private IEnumerator HideAfterMinimumVisiblePeriod()
        {
            yield return WaitForMinimumVisiblePeriod();
            SetProgress(1f);
            SetStatus("ui.loading.ready");
            DestinationRevealStarted?.Invoke();
            yield return HideRoutine();
            _running = null;
        }

        private IEnumerator WaitForMinimumVisiblePeriod()
        {
            while (Time.realtimeSinceStartup - _shownAt < MinimumVisibleSeconds)
                yield return null;
        }

        private void CancelAndRestoreFocus()
        {
            _isLoading = false;
            _running = null;
            Hide();
            if (_focusBeforeLoad != null && _focusBeforeLoad.activeInHierarchy && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_focusBeforeLoad);
        }

        private void BuildOverlay()
        {
            var ui = new UiFactory(GameFonts.Jersey25);
            Canvas canvas = ui.CreateCanvas(transform, "Loading Canvas", SortingOrder);
            canvas.overrideSorting = true;

            Image backdrop = UiFactory.CreateImage("Loading Backdrop", canvas.transform, Color.black);
            backdrop.raycastTarget = true;
            UiFactory.Stretch(backdrop.rectTransform);

            _loadingContent = UiFactory.CreateRect("Loading Content", canvas.transform);
            UiFactory.SetCenteredRect(_loadingContent, Vector2.zero, new Vector2(760f, 320f));

            _loadingTitle = ui.CreateText("Loading Title", _loadingContent, "Loading...", 82, FontStyle.Normal,
                Color.white, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_loadingTitle.rectTransform, new Vector2(0f, 70f), new Vector2(720f, 104f));

            _status = ui.CreateText("Loading Status", _loadingContent, string.Empty, 28, FontStyle.Normal,
                new Color(1f, 1f, 1f, 0.82f), TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_status.rectTransform, new Vector2(0f, -35f), new Vector2(720f, 48f));

            _progressBar = ui.CreateSlider(_loadingContent, "Loading Progress", 0f);
            _progressBarTransform = _progressBar.GetComponent<RectTransform>();
            UiFactory.SetCenteredRect(_progressBarTransform, new Vector2(0f, -100f), new Vector2(500f, 14f));
            _progressBar.interactable = false;
            StyleProgressBar();

            Text watermark = ui.CreateText("A-Math Watermark", canvas.transform, "A-MATH", 56, FontStyle.Normal,
                new Color(1f, 1f, 1f, 0.18f), TextAnchor.MiddleCenter);
            watermark.font = GameFonts.JainiPurva;
            UiFactory.SetAnchoredRect(watermark.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(500f, 80f), new Vector2(0f, 66f));

            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        private void Show(string statusKey, float progress)
        {
            if (_group == null)
                return;

            _shownAt = Time.realtimeSinceStartup;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            SetStatus(statusKey);
            SetProgress(progress);
            StartFade(1f, FadeInSeconds);
        }

        private void Hide()
        {
            if (_group == null)
                return;

            StartFade(0f, FadeOutSeconds);
        }

        private IEnumerator HideRoutine()
        {
            Hide();
            while (_fade != null)
                yield return null;
        }

        private void StartFade(float targetAlpha, float duration)
        {
            if (_fade != null)
                StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(_group.alpha, targetAlpha, duration));
        }

        private IEnumerator FadeRoutine(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                _group.alpha = Mathf.Lerp(from, to, progress);
                if (_loadingContent != null)
                {
                    float scale = to > from
                        ? Mathf.Lerp(0.985f, 1f, progress)
                        : Mathf.Lerp(1f, 1.015f, progress);
                    _loadingContent.localScale = Vector3.one * scale;
                }
                yield return null;
            }

            _group.alpha = to;
            if (_loadingContent != null)
                _loadingContent.localScale = Vector3.one;
            _group.blocksRaycasts = to > 0f;
            _group.interactable = to > 0f;
            _fade = null;
        }

        private void StyleProgressBar()
        {
            Image background = _progressBar.transform.Find("Background")?.GetComponent<Image>();
            Image fill = _progressBar.transform.Find("Fill Area/Fill")?.GetComponent<Image>();
            Image handle = _progressBar.transform.Find("Handle Slide Area/Handle")?.GetComponent<Image>();

            if (background != null)
                background.color = new Color(1f, 1f, 1f, 0.18f);
            if (fill != null)
                fill.color = new Color(1f, 1f, 1f, 0.94f);
            if (handle != null)
                handle.color = new Color(1f, 1f, 1f, 0.94f);
        }

        private void SetStatus(string statusKey)
        {
            if (_status != null)
                UiText.Set(_status, UiLocalizationProvider.Shared.GetText(statusKey));
        }

        private void SetProgress(float progress)
        {
            if (_progressBar != null)
                _progressBar.SetValueWithoutNotify(Mathf.Clamp01(progress));
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
