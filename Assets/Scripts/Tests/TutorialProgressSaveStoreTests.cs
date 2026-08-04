using System;
using System.IO;
using AMath.Tutorial.Save;
using NUnit.Framework;

namespace AMath.Tests
{
    public sealed class TutorialProgressSaveStoreTests
    {
        private string _directory;
        private TutorialProgressSaveStore _store;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "amath-tutorial-save-" + Guid.NewGuid().ToString("N"));
            _store = new TutorialProgressSaveStore(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        [Test]
        public void TryLoad_ReturnsFalse_WhenFileMissing()
        {
            Assert.IsFalse(_store.TryLoad("intro", out _));
        }

        [Test]
        public void TryLoad_ReturnsFalse_WhenTutorialIdEmpty()
        {
            Assert.IsFalse(_store.TryLoad(string.Empty, out _));
            Assert.IsFalse(_store.TryLoad(null, out _));
        }

        [Test]
        public void Save_ThenTryLoad_RoundTripsProgress()
        {
            var progress = new TutorialProgressData
            {
                TutorialId = "intro",
                StepIndex = 2,
                IsCompleted = false,
                TimestampUtcTicks = DateTime.UtcNow.Ticks
            };

            _store.Save("intro", progress);

            Assert.IsTrue(_store.TryLoad("intro", out TutorialProgressData loaded));
            Assert.AreEqual("intro", loaded.TutorialId);
            Assert.AreEqual(2, loaded.StepIndex);
            Assert.IsFalse(loaded.IsCompleted);
            Assert.AreEqual(progress.TimestampUtcTicks, loaded.TimestampUtcTicks);
        }

        [Test]
        public void Save_OverwritesPreviousProgress()
        {
            _store.Save("intro", new TutorialProgressData { StepIndex = 1, TimestampUtcTicks = 1 });
            _store.Save("intro", new TutorialProgressData { StepIndex = 4, IsCompleted = true, TimestampUtcTicks = 2 });

            Assert.IsTrue(_store.TryLoad("intro", out TutorialProgressData loaded));
            Assert.AreEqual(4, loaded.StepIndex);
            Assert.IsTrue(loaded.IsCompleted);
        }

        [Test]
        public void Save_UsesSeparateFilesPerTutorialId()
        {
            _store.Save("intro", new TutorialProgressData { StepIndex = 1, TimestampUtcTicks = 1 });
            _store.Save("advanced", new TutorialProgressData { StepIndex = 5, TimestampUtcTicks = 2 });

            Assert.IsTrue(_store.TryLoad("intro", out TutorialProgressData intro));
            Assert.IsTrue(_store.TryLoad("advanced", out TutorialProgressData advanced));
            Assert.AreEqual(1, intro.StepIndex);
            Assert.AreEqual(5, advanced.StepIndex);
        }

        [Test]
        public void TryLoad_ReturnsFalse_WhenStoredTutorialIdMismatches()
        {
            string path = Path.Combine(_directory, "tutorial_intro.json");
            File.WriteAllText(path,
                "{\"TutorialId\":\"other\",\"StepIndex\":1,\"IsCompleted\":false,\"TimestampUtcTicks\":1}");

            Assert.IsFalse(_store.TryLoad("intro", out _));
        }

        [Test]
        public void Save_Throws_WhenTutorialIdMissing()
        {
            Assert.Throws<ArgumentException>(() =>
                _store.Save(string.Empty, new TutorialProgressData { StepIndex = 0 }));
        }

        [Test]
        public void Save_Throws_WhenDataNull()
        {
            Assert.Throws<ArgumentNullException>(() => _store.Save("intro", null));
        }
    }
}
