using System.Reflection;
using AMath.Art;
using AMath.Settings;
using AMath.UI;
using AMath.UI.Localization;
using AMath.UI.Tutorial;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AMath.Tests
{
    public sealed class SceneTransitionPresentationTests
    {
        [Test]
        public void LoadingStrings_AreAvailableInEnglishAndThai()
        {
            int originalLanguage = GameSettings.LanguageIndex;
            try
            {
                foreach (int language in new[] { 0, 1 })
                {
                    GameSettings.LanguageIndex = language;
                    Assert.That(UiLocalizationProvider.Shared.GetText("ui.loading.startup"), Is.Not.Empty);
                    Assert.That(UiLocalizationProvider.Shared.GetText("ui.loading.account"), Is.Not.Empty);
                    Assert.That(UiLocalizationProvider.Shared.GetText("ui.loading.tutorial"), Is.Not.Empty);
                    Assert.That(UiLocalizationProvider.Shared.GetText("ui.loading.menu"), Is.Not.Empty);
                    Assert.That(UiLocalizationProvider.Shared.GetText("ui.loading.ready"), Is.Not.Empty);
                }
            }
            finally
            {
                GameSettings.LanguageIndex = originalLanguage;
            }
        }

        [Test]
        public void LoadingOverlay_HasProgressAndInputBlockingSurface()
        {
            var root = new GameObject("Loading Overlay Test");
            try
            {
                var controller = root.AddComponent<SceneTransitionController>();
                if (controller.transform.Find("Loading Canvas") == null)
                {
                    MethodInfo buildOverlay = typeof(SceneTransitionController).GetMethod(
                        "BuildOverlay",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.NotNull(buildOverlay);
                    buildOverlay.Invoke(controller, null);
                }
                Transform canvas = controller.transform.Find("Loading Canvas");
                Assert.NotNull(canvas);
                Assert.NotNull(canvas.Find("Loading Backdrop").GetComponent<Image>());
                Assert.That(canvas.Find("Loading Backdrop").GetComponent<Image>().color, Is.EqualTo(Color.black));
                Assert.NotNull(canvas.Find("Loading Content/Loading Title").GetComponent<Text>());

                RectTransform content = canvas.Find("Loading Content").GetComponent<RectTransform>();
                Assert.That(content.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(content.anchoredPosition, Is.EqualTo(Vector2.zero));
                RectTransform title = content.Find("Loading Title").GetComponent<RectTransform>();
                RectTransform barRect = content.Find("Loading Progress").GetComponent<RectTransform>();
                float visualMidpoint = (title.anchoredPosition.y + title.sizeDelta.y * 0.5f
                    + barRect.anchoredPosition.y - barRect.sizeDelta.y * 0.5f) * 0.5f;
                Assert.That(Mathf.Abs(visualMidpoint), Is.LessThan(15f));

                Slider progress = canvas.Find("Loading Content/Loading Progress").GetComponent<Slider>();
                Assert.NotNull(progress);
                Assert.AreEqual(0f, progress.minValue);
                Assert.AreEqual(1f, progress.maxValue);
                Assert.IsFalse(progress.interactable);

                Text watermark = canvas.Find("A-Math Watermark").GetComponent<Text>();
                Assert.That(watermark.text, Is.EqualTo("A-MATH"));
                Assert.That(watermark.color.a, Is.InRange(0.10f, 0.25f));

                CanvasGroup group = canvas.GetComponent<CanvasGroup>();
                Assert.NotNull(group);
                Assert.IsFalse(group.blocksRaycasts, "The overlay must not block input before it is shown.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MissingScene_IsRejectedBeforeStartingATransition()
        {
            Assert.IsFalse(SceneTransitionController.IsLoadableScene("DefinitelyMissingAMathScene"));
            LogAssert.Expect(
                LogType.Error,
                "A-Math: cannot load scene 'DefinitelyMissingAMathScene'. It is not in Build Settings.");
            Assert.IsFalse(SceneTransitionController.LoadScene("DefinitelyMissingAMathScene"));
            Assert.IsFalse(SceneTransitionController.IsTransitioning);
        }

        [Test]
        public void SceneBootstraps_RegisterBeforeScenesLoad()
        {
            AssertRegistersBeforeSceneLoad(typeof(MainMenuBootstrap));
            AssertRegistersBeforeSceneLoad(typeof(TutorialSceneBootstrap));
        }

        [Test]
        public void MainMenuBootstrap_RoutesStartupSceneAndDoesNotCreateDuplicates()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/SampleScene.unity",
                OpenSceneMode.Additive);
            GameObject root = null;
            try
            {
                root = new GameObject("Existing Main Menu");
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, scene);
                MainMenuController existing = root.AddComponent<MainMenuController>();

                MainMenuController first = MainMenuBootstrap.EnsureMenuForScene(scene);
                MainMenuController second = MainMenuBootstrap.EnsureMenuForScene(scene);

                Assert.AreSame(existing, first);
                Assert.AreSame(first, second);
                Assert.AreEqual(scene, first.gameObject.scene);
                Assert.IsNull(TutorialSceneBootstrap.EnsureTutorialForScene(scene));
            }
            finally
            {
                if (root != null)
                    Object.DestroyImmediate(root);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TutorialBootstrap_RoutesTutorialSceneAndDoesNotCreateDuplicates()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/TutorialScene.unity",
                OpenSceneMode.Additive);
            GameObject root = null;
            try
            {
                root = new GameObject("Existing Tutorial Bootstrap");
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, scene);
                TutorialSceneController existing = root.AddComponent<TutorialSceneController>();

                TutorialSceneController first = TutorialSceneBootstrap.EnsureTutorialForScene(scene);
                TutorialSceneController second = TutorialSceneBootstrap.EnsureTutorialForScene(scene);

                Assert.AreSame(existing, first);
                Assert.AreSame(first, second);
                Assert.AreEqual(scene, first.gameObject.scene);
                Assert.IsNull(MainMenuBootstrap.EnsureMenuForScene(scene));
            }
            finally
            {
                if (root != null)
                    Object.DestroyImmediate(root);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AssertRegistersBeforeSceneLoad(System.Type bootstrapType)
        {
            MethodInfo method = bootstrapType.GetMethod(
                "RegisterSceneLoadedHandler",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method, $"{bootstrapType.Name} must register a sceneLoaded callback.");

            var attribute = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.NotNull(attribute);
            Assert.AreEqual(RuntimeInitializeLoadType.BeforeSceneLoad, attribute.loadType);
        }
    }
}
