using System.Reflection;
using AMath.Tutorial.UI;
using NUnit.Framework;
using UnityEngine;

namespace AMath.Tests
{
    public sealed class TutorialUiTests
    {
        [Test]
        public void DismissActiveDialogue_StopsAndClearsVoiceBeforeCompleting()
        {
            var root = new GameObject("Tutorial UI");
            try
            {
                var ui = root.AddComponent<TutorialUI>();
                var panel = new GameObject("Dialogue Panel");
                var source = root.AddComponent<AudioSource>();
                SetPrivateField(ui, "_dialoguePanel", panel);
                SetPrivateField(ui, "_voiceSource", source);

                int completed = 0;
                AudioClip clip = AudioClip.Create("Voice", 32, 1, 44100, false);
                ui.Play("Dialogue", clip, () => completed++);
                ui.DismissActiveDialogue();

                Assert.AreEqual(1, completed);
                Assert.IsNull(source.clip);
                Assert.IsFalse(panel.activeSelf);

                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(panel);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void SetPrivateField<T>(TutorialUI target, string name, T value)
        {
            FieldInfo field = typeof(TutorialUI).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }
    }
}
