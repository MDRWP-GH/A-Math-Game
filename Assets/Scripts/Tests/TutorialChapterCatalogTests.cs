using System.Collections.Generic;
using AMath.Tutorial.Definitions;
using AMath.Tutorial.Interfaces;
using AMath.Tutorial.Save;
using AMath.UI.Tutorial;
using NUnit.Framework;

namespace AMath.Tests
{
    public sealed class TutorialChapterCatalogTests
    {
        private sealed class MemorySaveStore : ITutorialSaveStore
        {
            private readonly Dictionary<string, TutorialProgressData> _data = new();

            public bool TryLoad(string tutorialId, out TutorialProgressData data) =>
                _data.TryGetValue(tutorialId, out data);

            public void Save(string tutorialId, TutorialProgressData data) =>
                _data[tutorialId] = data;
        }

        [Test]
        public void ChapterStates_FollowSequentialUnlockAndSavedProgress()
        {
            var save = new MemorySaveStore();
            TutorialChapterDescriptor intro = TutorialChapterCatalog.All[0];
            TutorialChapterDescriptor connect = TutorialChapterCatalog.All[1];
            TutorialChapterDescriptor premium = TutorialChapterCatalog.All[2];

            Assert.AreEqual(IntroTutorialSequence.IntroTutorialId, intro.Id);
            Assert.AreEqual(TutorialChapterState.New, intro.ResolveState(save, out _));
            Assert.AreEqual(TutorialChapterState.Locked, connect.ResolveState(save, out _));
            Assert.AreEqual(TutorialChapterState.Locked, premium.ResolveState(save, out _));

            save.Save(intro.Id, new TutorialProgressData { TutorialId = intro.Id, StepIndex = 5 });
            Assert.AreEqual(TutorialChapterState.InProgress, intro.ResolveState(save, out _));

            save.Save(intro.Id, new TutorialProgressData { TutorialId = intro.Id, IsCompleted = true });
            Assert.AreEqual(TutorialChapterState.Completed, intro.ResolveState(save, out _));
            Assert.AreEqual(TutorialChapterState.New, connect.ResolveState(save, out _));

            save.Save(connect.Id, new TutorialProgressData { TutorialId = connect.Id, IsCompleted = true });
            Assert.AreEqual(TutorialChapterState.New, premium.ResolveState(save, out _));
            Assert.AreEqual(3, premium.OutcomeKeys.Count);
        }
    }
}
