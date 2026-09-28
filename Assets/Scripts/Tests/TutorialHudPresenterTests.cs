using AMath.UI.Tutorial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AMath.Tests
{
    public sealed class TutorialHudPresenterTests
    {
        [Test]
        public void DismissActiveDialogue_CompletesAndHidesPanel()
        {
            var root = new GameObject("HUD");
            var dialoguePanel = new GameObject("Dialogue");
            var dialogueText = dialoguePanel.AddComponent<Text>();
            var continueGo = new GameObject("Continue");
            continueGo.transform.SetParent(root.transform, false);
            continueGo.AddComponent<Button>();
            continueGo.AddComponent<Image>();

            var presenter = new TutorialHudPresenter(
                root,
                objectiveText: null,
                progressText: null,
                progressFill: null,
                hintText: null,
                dialoguePanel,
                dialogueText,
                dialogueAdvanceButton: null,
                continueButton: continueGo.GetComponent<Button>(),
                skipButton: null,
                replayStepButton: null);

            int completed = 0;
            presenter.Play("Line", () => completed++);
            presenter.DismissActiveDialogue();

            Assert.AreEqual(1, completed);
            Assert.IsFalse(dialoguePanel.activeSelf);

            Object.DestroyImmediate(continueGo);
            Object.DestroyImmediate(dialoguePanel);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void Continue_IsVisibleOnlyForDialogueThatWaitsForConfirmation()
        {
            var root = new GameObject("HUD");
            var panel = new GameObject("Dialogue", typeof(Image), typeof(Button));
            var continueGo = new GameObject("Continue", typeof(Image), typeof(Button));
            panel.transform.SetParent(root.transform, false);
            continueGo.transform.SetParent(root.transform, false);
            var presenter = new TutorialHudPresenter(
                root,
                objectiveText: null,
                progressText: null,
                progressFill: null,
                hintText: null,
                panel,
                dialogueText: null,
                panel.GetComponent<Button>(),
                continueGo.GetComponent<Button>(),
                skipButton: null,
                replayStepButton: null);

            presenter.Play("Act now", onFinished: null);
            Assert.IsFalse(continueGo.activeSelf);
            Assert.IsFalse(panel.GetComponent<Button>().interactable);

            presenter.Play("Read this", () => { });
            Assert.IsTrue(continueGo.activeSelf);
            Assert.IsTrue(panel.GetComponent<Button>().interactable);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void Progress_UpdatesTextAndFillTogether()
        {
            var root = new GameObject("HUD");
            Text progress = new GameObject("Progress", typeof(Text)).GetComponent<Text>();
            Image fill = new GameObject("Fill", typeof(Image)).GetComponent<Image>();
            progress.transform.SetParent(root.transform, false);
            fill.transform.SetParent(root.transform, false);
            var presenter = new TutorialHudPresenter(
                root, null, progress, fill, null, null, null, null, null, null, null);

            presenter.SetProgress(3, 4);

            Assert.AreEqual("3/4", progress.text);
            Assert.AreEqual(0.75f, fill.fillAmount);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void SpotlightShade_DoesNotBlockReplayOrSkipControls()
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var overlayObject = new GameObject(
                "Spotlight", typeof(RectTransform), typeof(TutorialSpotlightOverlay));
            overlayObject.transform.SetParent(canvasObject.transform, false);

            TutorialSpotlightOverlay overlay = overlayObject.GetComponent<TutorialSpotlightOverlay>();
            overlay.Initialize(canvasObject.GetComponent<Canvas>());

            foreach (string panelName in new[] { "Top", "Bottom", "Left", "Right" })
                Assert.IsFalse(overlayObject.transform.Find(panelName).GetComponent<Image>().raycastTarget);

            Object.DestroyImmediate(canvasObject);
        }

        [Test]
        public void SpotlightShade_ConvertsCenteredCanvasCoordinatesToBottomLeftAnchors()
        {
            Rect canvas = new Rect(-960f, -540f, 1920f, 1080f);
            Assert.AreEqual(Vector2.zero, TutorialSpotlightOverlay.ToBottomLeftAnchorPosition(
                new Rect(-960f, -540f, 100f, 100f), canvas));
            Assert.AreEqual(new Vector2(960f, 540f), TutorialSpotlightOverlay.ToBottomLeftAnchorPosition(
                new Rect(0f, 0f, 100f, 100f), canvas));
        }

        [Test]
        public void Correction_IsTransient_AndDoesNotChangeProgress()
        {
            var root = new GameObject("HUD", typeof(RectTransform));
            var feedback = new GameObject("Feedback", typeof(Text)).GetComponent<Text>();
            feedback.transform.SetParent(root.transform, false);
            var progress = new GameObject("Progress", typeof(Text)).GetComponent<Text>();
            progress.transform.SetParent(root.transform, false);
            var presenter = new TutorialHudPresenter(
                root, null, progress, null, null, null, null, null, null, null, null,
                feedbackText: feedback,
                coachPanel: root.GetComponent<RectTransform>());

            presenter.SetProgress(2, 4);
            presenter.ShowCorrection("Choose the highlighted tile");

            Assert.IsTrue(feedback.gameObject.activeSelf);
            Assert.AreEqual("Choose the highlighted tile", feedback.text);
            Assert.AreEqual("2/4", progress.text);

            presenter.Tick(3f);
            Assert.IsFalse(feedback.gameObject.activeSelf);
            Assert.AreEqual("2/4", progress.text);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void SpotlightResolver_FollowsCurrentTarget_AndClearsHiddenTarget()
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var overlayObject = new GameObject("Spotlight", typeof(RectTransform), typeof(TutorialSpotlightOverlay));
            overlayObject.transform.SetParent(canvasObject.transform, false);
            var first = new GameObject("First", typeof(RectTransform));
            var second = new GameObject("Second", typeof(RectTransform));
            first.transform.SetParent(canvasObject.transform, false);
            second.transform.SetParent(canvasObject.transform, false);
            var overlay = overlayObject.GetComponent<TutorialSpotlightOverlay>();
            overlay.Initialize(canvasObject.GetComponent<Canvas>());
            var presenter = new TutorialHudPresenter(
                canvasObject, null, null, null, null, null, null, null, null, null, null);
            presenter.SetSpotlight(overlay);
            RectTransform current = first.GetComponent<RectTransform>();
            presenter.RegisterSpotlightTargetResolver("changing", () => current);

            presenter.Highlight("changing");
            Assert.AreSame(current, presenter.ActiveSpotlightTarget);
            current = second.GetComponent<RectTransform>();
            presenter.Tick(0f);
            Assert.AreSame(current, presenter.ActiveSpotlightTarget);
            second.SetActive(false);
            presenter.Tick(0f);
            Assert.IsNull(presenter.ActiveSpotlightTarget);

            Object.DestroyImmediate(canvasObject);
        }

        [Test]
        public void GuidedRackTargets_UseExactTileAndNextExchangeIndex()
        {
            Assert.AreEqual(2, TutorialMatchView.FindGuidedRackIndex(
                new byte[] { 8, 9, 13, 13 }, 13));
            Assert.AreEqual(-1, TutorialMatchView.FindGuidedRackIndex(
                new byte[] { 8, 9 }, 13));
            Assert.AreEqual(0, TutorialMatchView.NextExchangeIndex(new[] { 0, 1 }, new int[0]));
            Assert.AreEqual(1, TutorialMatchView.NextExchangeIndex(new[] { 0, 1 }, new[] { 0 }));
            Assert.AreEqual(-1, TutorialMatchView.NextExchangeIndex(new[] { 0, 1 }, new[] { 0, 1 }));
        }
    }
}
