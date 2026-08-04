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
