using System.IO;
using AMath.Core.Snapshot;
using AMath.Core.Events;
using AMath.Save;
using NUnit.Framework;
using UnityEngine;

namespace AMath.Tests
{
    public sealed class SaveMigratorTests
    {
        [Test]
        public void CurrentVersion_PassesThroughUnchanged()
        {
            string json = JsonUtility.ToJson(new SaveFile { GameVersion = "1.0" });
            var migrator = new SaveMigrator();

            Assert.IsTrue(migrator.TryMigrate(json, out string migrated, out string error), error);
            Assert.AreEqual(json, migrated);
        }

        [Test]
        public void NewerSchema_IsRejectedSafely()
        {
            string json = "{\"SaveVersion\":9999}";
            Assert.IsFalse(new SaveMigrator().TryMigrate(json, out _, out string error));
            StringAssert.Contains("newer", error);
        }

        [Test]
        public void MissingVersion_IsRejected()
        {
            Assert.IsFalse(new SaveMigrator().TryMigrate("{}", out _, out _));
        }

        [TestCase("")]
        [TestCase("{")]
        [TestCase("not-json")]
        public void MalformedOrTruncatedJson_IsRejectedWithoutThrowing(string json)
        {
            var migrator = new SaveMigrator();
            Assert.DoesNotThrow(() =>
                Assert.IsFalse(migrator.TryMigrate(json, out _, out string error), error));
        }

        [TestCase("{")]
        [TestCase("not-json")]
        [TestCase("{\"SaveVersion\":9999}")]
        public void SaveManager_RejectsInvalidFilesWithoutThrowing(string json)
        {
            string path = Path.GetTempFileName();
            File.WriteAllText(path, json);
            var manager = new SaveManager(new EventBus(), null, null);

            try
            {
                bool loaded = true;
                string error = null;
                Assert.DoesNotThrow(() => loaded = manager.TryLoad(path, out _, out error));
                Assert.IsFalse(loaded);
                Assert.IsNotEmpty(error);
            }
            finally
            {
                manager.Dispose();
                File.Delete(path);
            }
        }

        [Test]
        public void SaveManager_RejectsIncompleteSnapshotBeforeRecovery()
        {
            string path = Path.GetTempFileName();
            File.WriteAllText(path, JsonUtility.ToJson(new SaveFile
            {
                State = new GameStateSnapshot { TurnNumber = 1 }
            }));
            var manager = new SaveManager(new EventBus(), null, null);

            try
            {
                Assert.IsFalse(manager.TryLoad(path, out SaveFile file, out string error));
                Assert.IsNull(file);
                StringAssert.Contains("corrupt", error);
            }
            finally
            {
                manager.Dispose();
                File.Delete(path);
            }
        }

        [Test]
        public void DuplicateMigrationStep_Throws()
        {
            var migrator = new SaveMigrator();
            migrator.Register(new FakeStep());
            Assert.Throws<System.InvalidOperationException>(() => migrator.Register(new FakeStep()));
        }

        private sealed class FakeStep : ISaveMigrationStep
        {
            public int FromVersion => 0;
            public string Apply(string json) => json;
        }
    }
}
