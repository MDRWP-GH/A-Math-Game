using System.Collections.Generic;
using System.IO;
using AMath.Core;
using AMath.Core.Assistance;
using AMath.Core.Events;
using AMath.Tutorial;
using AMath.Tutorial.Bootstrap;
using AMath.Tutorial.Definitions;
using AMath.Tutorial.Interfaces;
using AMath.Tutorial.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

namespace AMath.Tests
{
    public sealed class TutorialManagerTests
    {
        private sealed class MemorySaveStore : ITutorialSaveStore
        {
            private readonly Dictionary<string, TutorialProgressData> _data = new();

            public bool TryLoad(string tutorialId, out TutorialProgressData data) =>
                _data.TryGetValue(tutorialId, out data);

            public void Save(string tutorialId, TutorialProgressData data) =>
                _data[tutorialId] = data;
        }

        private sealed class StubUi : ITutorialUiService
        {
            public string Milestone { get; private set; }
            public int CurrentProgress { get; private set; }
            public int TotalProgress { get; private set; }

            public void SetMilestone(string milestoneText) => Milestone = milestoneText;
            public void SetObjective(string objectiveText) { }
            public void SetProgress(int currentStep, int totalSteps)
            {
                CurrentProgress = currentStep;
                TotalProgress = totalSteps;
            }
            public void ShowHint(string hintText) { }
            public void ClearHint() { }
        }

        private sealed class StubHighlighter : ITutorialHighlightService
        {
            public void Highlight(string targetId) { }
            public void ClearHighlight() { }
        }

        private sealed class StubDialogue : ITutorialDialogueService
        {
            public void Play(string localizedText, System.Action onFinished) { }
            public void Skip() { }
        }

        private sealed class KeysAsText : ILocalizedTextProvider
        {
            public string GetText(string key) => key ?? string.Empty;
        }

        [Test]
        public void Skip_DoesNotMarkTutorialCompleted()
        {
            var bus = new EventBus();
            var save = new MemorySaveStore();
            var context = new TutorialRuntimeContext(
                bus,
                boardState: null,
                playerState: null,
                matchState: null,
                new StubHighlighter(),
                new StubDialogue(),
                new StubUi());

            using var manager = new TutorialManager(bus, context, new KeysAsText(), save);
            manager.Start(new IntroTutorialSequence(new KeysAsText()), resumeProgress: false);
            manager.Skip();

            Assert.IsTrue(save.TryLoad(IntroTutorialSequence.IntroTutorialId, out TutorialProgressData data));
            Assert.IsFalse(data.IsCompleted);
        }

        private sealed class UnavailableSaveStore : ITutorialSaveStore
        {
            public bool TryLoad(string tutorialId, out TutorialProgressData data)
            {
                data = null;
                return false;
            }

            public void Save(string tutorialId, TutorialProgressData data) =>
                throw new IOException("Storage is full");
        }

        [Test]
        public void UnavailableStorage_DoesNotInterruptTutorialOrSkip()
        {
            var bus = new EventBus();
            var context = new TutorialRuntimeContext(
                bus, null, null, null,
                new StubHighlighter(), new StubDialogue(), new StubUi());
            using var manager = new TutorialManager(bus, context, new KeysAsText(), new UnavailableSaveStore());
            LogAssert.Expect(LogType.Warning, new Regex("Progress was not saved"));

            Assert.DoesNotThrow(() => manager.Start(new IntroTutorialSequence(new KeysAsText())));
            Assert.AreEqual(AMath.Tutorial.StateMachine.TutorialPhase.Running, manager.Phase);
            Assert.DoesNotThrow(manager.Skip);
        }

        [Test]
        public void Resume_UsesMilestoneProgressWithoutChangingSavedStepIndex()
        {
            var bus = new EventBus();
            var save = new MemorySaveStore();
            save.Save(IntroTutorialSequence.IntroTutorialId, new TutorialProgressData
            {
                TutorialId = IntroTutorialSequence.IntroTutorialId,
                StepIndex = 5,
                IsCompleted = false
            });
            var ui = new StubUi();
            var context = new TutorialRuntimeContext(
                bus,
                boardState: null,
                playerState: null,
                matchState: null,
                new StubHighlighter(),
                new StubDialogue(),
                ui);

            using var manager = new TutorialManager(bus, context, new KeysAsText(), save);
            manager.Start(new IntroTutorialSequence(new KeysAsText()), resumeProgress: true);

            Assert.AreEqual(5, manager.CurrentStepIndex);
            Assert.AreEqual("tutorial.intro.milestone.equation", ui.Milestone);
            Assert.AreEqual(2, ui.CurrentProgress);
            Assert.AreEqual(4, ui.TotalProgress);
        }

        [Test]
        public void ReplayCompletedChapter_PreservesCompletionWhenExited()
        {
            var bus = new EventBus();
            var save = new MemorySaveStore();
            save.Save(IntroTutorialSequence.IntroTutorialId, new TutorialProgressData
            {
                TutorialId = IntroTutorialSequence.IntroTutorialId,
                StepIndex = 99,
                IsCompleted = true
            });
            var context = new TutorialRuntimeContext(
                bus,
                boardState: null,
                playerState: null,
                matchState: null,
                new StubHighlighter(),
                new StubDialogue(),
                new StubUi());

            using var manager = new TutorialManager(bus, context, new KeysAsText(), save);
            manager.Start(new IntroTutorialSequence(new KeysAsText()), resumeProgress: false);
            manager.Skip();

            Assert.IsTrue(save.TryLoad(IntroTutorialSequence.IntroTutorialId, out TutorialProgressData data));
            Assert.IsTrue(data.IsCompleted);
        }
    }
}
