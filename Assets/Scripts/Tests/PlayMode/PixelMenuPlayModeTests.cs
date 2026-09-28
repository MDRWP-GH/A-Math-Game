using System.Collections;
using System.IO;
using AMath.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AMath.Tests
{
    public sealed class PixelMenuPlayModeTests
    {
        [UnityTest]
        public IEnumerator SettingsTransition_KeepsMenuBackdropAndBlocksItsInput()
        {
            var root = new GameObject("Pixel UI PlayMode Test");
            int originalWidth = Screen.width;
            int originalHeight = Screen.height;
            var ui = new UiFactory();
            Canvas menuCanvas = ui.CreateCanvas(root.transform, "Menu Canvas", 100);
            UiFactory.CreateFullScreenBackground(menuCanvas.transform, "Main Menu Backgrounds", UiPalette.Background);
            CanvasGroup menuInput = menuCanvas.gameObject.AddComponent<CanvasGroup>();

            Button back = ui.CreateAccentButton(menuCanvas.transform, "Back", "Back",
                UiPalette.Danger, UiPalette.DangerHighlight, () => { });
            UiFactory.SetCenteredRect(back.GetComponent<RectTransform>(), new Vector2(-220f, 35f), new Vector2(320f, 64f));
            Button leave = ui.CreateAccentButton(menuCanvas.transform, "Leave", "Leave",
                UiPalette.Danger, UiPalette.DangerHighlight, () => { });
            UiFactory.SetCenteredRect(leave.GetComponent<RectTransform>(), new Vector2(220f, 35f), new Vector2(320f, 64f));
            Button logout = ui.CreateAccentButton(menuCanvas.transform, "Logout", "Log out",
                UiPalette.Quit, UiPalette.QuitHighlight, () => { });
            UiFactory.SetCenteredRect(logout.GetComponent<RectTransform>(), new Vector2(0f, -65f), new Vector2(320f, 64f));
            Button choice = ui.CreateButton(menuCanvas.transform, "Selected Format", "Individual",
                UiPalette.Secondary, UiPalette.SecondaryHighlight, () => { });
            UiFactory.SetCenteredRect(choice.GetComponent<RectTransform>(), new Vector2(0f, -160f), new Vector2(320f, 64f));
            UiFactory.SetChoiceSelected(choice, true);

            SettingsMenuController settings = SettingsMenuController.Create(root.transform, ui.Font);
            try
            {
                SceneTransitionController.NotifyStartupReady();
                yield return new WaitForSecondsRealtime(0.8f);
                // This test uses an isolated scene with no bootstrap menu to dismiss
                // the startup surface, so hide that surface before visual capture.
                var transition = UnityEngine.Object.FindFirstObjectByType<SceneTransitionController>();
                CanvasGroup loadingGroup = transition?.transform.Find("Loading Canvas")?.GetComponent<CanvasGroup>();
                if (loadingGroup != null)
                {
                    loadingGroup.alpha = 0f;
                    loadingGroup.blocksRaycasts = false;
                }

                foreach ((int width, int height) in new[] { (1920, 1080), (1280, 720) })
                {
                    Screen.SetResolution(width, height, false);
                    yield return null;
                    if (!Application.isBatchMode)
                    {
                        yield return new WaitForEndOfFrame();
                        CaptureIfRequested($"pixel-menu-requested-{width}x{height}-actual-{Screen.width}x{Screen.height}.png");
                    }

                    menuInput.interactable = false;
                    menuInput.blocksRaycasts = false;
                    settings.Open();
                    Assert.IsTrue(menuCanvas.gameObject.activeInHierarchy);
                    Assert.IsFalse(menuInput.interactable);
                    Assert.IsFalse(menuInput.blocksRaycasts);
                    yield return new WaitForSecondsRealtime(0.25f);
                    if (!Application.isBatchMode)
                        yield return new WaitForEndOfFrame();
                    Assert.IsTrue(settings.IsOpen);
                    if (!Application.isBatchMode)
                        CaptureIfRequested($"pixel-settings-requested-{width}x{height}-actual-{Screen.width}x{Screen.height}.png");

                    settings.Close();
                    menuInput.interactable = true;
                    menuInput.blocksRaycasts = true;
                    Assert.IsTrue(menuInput.interactable);
                    Assert.IsTrue(menuInput.blocksRaycasts);
                }
            }
            finally
            {
                Screen.SetResolution(originalWidth, originalHeight, false);
                UnityEngine.Object.Destroy(root);
            }
        }

        private static void CaptureIfRequested(string fileName)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../tmp"));
            Directory.CreateDirectory(directory);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, fileName));
        }
    }
}
