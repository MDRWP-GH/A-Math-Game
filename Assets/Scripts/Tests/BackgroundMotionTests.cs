using AMath.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AMath.Tests
{
    public sealed class BackgroundMotionTests
    {
        [TestCase("Main Menu Backgrounds", "Chimney Smoke A")]
        [TestCase("In Game Backgrounds", "Upper Left Clouds")]
        public void AnimatedBackground_PreservesHitTarget_AndAddsNoninteractiveLayers(
            string resourceName, string expectedLayer)
        {
            var root = new GameObject("Background Test", typeof(RectTransform));
            try
            {
                Image background = UiFactory.CreateFullScreenBackground(
                    root.transform, resourceName, Color.magenta);
                Assert.NotNull(background.sprite);
                Assert.NotNull(background.GetComponent<BackgroundMotion>());
                Assert.That(background.color.a, Is.EqualTo(0f));

                background.raycastTarget = true;
                Assert.IsTrue(background.raycastTarget);
                Assert.IsTrue(background.IsRaycastLocationValid(Vector2.zero, null));
                Assert.NotNull(background.transform.Find("Clean Plate"));
                Assert.NotNull(background.transform.Find(expectedLayer));
                foreach (RawImage layer in background.GetComponentsInChildren<RawImage>())
                    Assert.IsFalse(layer.raycastTarget, layer.name);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MissingArtwork_LeavesOriginalColorFallback()
        {
            var root = new GameObject("Missing Background Test", typeof(RectTransform));
            try
            {
                Color fallback = Color.magenta;
                Image background = UiFactory.CreateFullScreenBackground(
                    root.transform, "No Such Background", fallback);
                Assert.IsNull(background.GetComponent<BackgroundMotion>());
                Assert.AreEqual(fallback, background.color);
                Assert.AreEqual(0, background.transform.childCount);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MotionRepeatsAtItsPeriod()
        {
            var root = new GameObject("Motion Loop Test", typeof(RectTransform));
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
                Image background = UiFactory.CreateFullScreenBackground(
                    root.transform, "Main Menu Backgrounds", Color.black);
                BackgroundMotion motion = background.GetComponent<BackgroundMotion>();
                Assert.NotNull(motion);

                RectTransform cloud = background.transform.Find("Left Clouds").GetComponent<RectTransform>();
                motion.ApplyAt(0f);
                Vector2 initial = cloud.anchoredPosition;
                motion.ApplyAt(5.75f);
                Assert.That(Mathf.Abs(cloud.anchoredPosition.x - initial.x), Is.GreaterThan(0.01f));
                motion.ApplyAt(23f);
                Assert.That(cloud.anchoredPosition.x, Is.EqualTo(initial.x).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
