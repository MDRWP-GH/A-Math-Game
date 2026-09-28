using System;
using System.IO;
using AMath.Accounts;
using AMath.UI;
using AMath.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AMath.Tests
{
    public sealed class LocalAccountServiceTests
    {
        private string _root;
        private LocalAccountService _service;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "amath-accounts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _service = new LocalAccountService(_root);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Logout();
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        [Test]
        public void StartupWithoutSession_CreatesVisibleSaveFolders()
        {
            Assert.IsFalse(_service.TryAutoLogin(out AccountError error));
            Assert.AreEqual(AccountError.None, error);
            Assert.IsTrue(Directory.Exists(Path.Combine(_root, "users")));
        }

        [Test]
        public void Register_CreatesHashedAccount_AndAutoLoginWorks()
        {
            Assert.IsTrue(_service.TryRegister("Player One", "secret42", out AccountError registerError), registerError.ToString());
            Assert.IsTrue(AccountSession.IsAuthenticated);
            Assert.AreEqual("Player One", AccountSession.Username);

            string database = File.ReadAllText(Path.Combine(AccountSession.ProfileRoot, "account.json"));
            StringAssert.DoesNotContain("secret42", database);
            StringAssert.Contains("PasswordHash", database);

            var restartedService = new LocalAccountService(_root);
            Assert.IsTrue(restartedService.TryAutoLogin(out AccountError autoLoginError), autoLoginError.ToString());
            Assert.AreEqual("Player One", AccountSession.Username);
        }

        [Test]
        public void Login_IsCaseInsensitive_ButPasswordIsNot()
        {
            Assert.IsTrue(_service.TryRegister("MathKid", "Abcd", out _));
            _service.Logout();

            Assert.IsFalse(_service.TryLogin("mathkid", "abcd", out AccountError wrongPassword));
            Assert.AreEqual(AccountError.InvalidCredentials, wrongPassword);
            Assert.IsTrue(_service.TryLogin("MATHKID", "Abcd", out AccountError success), success.ToString());
        }

        [Test]
        public void InterruptedRegistration_DoesNotLockOutExistingAccount()
        {
            Assert.IsTrue(_service.TryRegister("Existing", "abcd", out _));
            _service.Logout();
            string staging = Path.Combine(_root, "users", "Interrupted--1234567890abcdef1234567890abcdef.creating");
            Directory.CreateDirectory(staging);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Incomplete account folder was left untouched"));
            Assert.IsTrue(_service.TryLogin("Existing", "abcd", out AccountError error), error.ToString());
            Assert.IsTrue(Directory.Exists(staging), "The unfinished folder must remain recoverable.");
            Directory.Delete(staging, recursive: true);
        }

        [Test]
        public void Register_RequiresConfirmationFriendlyValidation_AndUniqueUsername()
        {
            Assert.IsFalse(_service.ValidateRegistrationInput("", "abcd", out AccountError missingName));
            Assert.AreEqual(AccountError.UsernameRequired, missingName);
            Assert.IsFalse(_service.ValidateRegistrationInput("Player", "abc", out AccountError shortPassword));
            Assert.AreEqual(AccountError.PasswordTooShort, shortPassword);

            Assert.IsTrue(_service.TryRegister("Player", "abcd", out _));
            _service.Logout();
            Assert.IsFalse(_service.TryRegister(" player ", "different", out AccountError duplicate));
            Assert.AreEqual(AccountError.UsernameTaken, duplicate);
        }

        [Test]
        public void Profiles_UseDifferentFolders_AndLegacyDataIsClaimedOnlyOnce()
        {
            string legacy = Path.Combine(_root, "Progress");
            Directory.CreateDirectory(legacy);
            File.WriteAllText(Path.Combine(legacy, "tutorial_intro.json"), "legacy");

            Assert.IsTrue(_service.TryRegister("First", "abcd", out _));
            string firstRoot = AccountSession.ProfileRoot;
            Assert.IsTrue(File.Exists(Path.Combine(firstRoot, "Progress", "tutorial_intro.json")));
            Assert.IsTrue(ProfileStorage.GetDirectory("Saves").StartsWith(firstRoot, StringComparison.OrdinalIgnoreCase));

            _service.Logout();
            Assert.IsTrue(_service.TryRegister("Second", "abcd", out _));
            string secondRoot = AccountSession.ProfileRoot;
            Assert.AreNotEqual(firstRoot, secondRoot);
            Assert.IsFalse(File.Exists(Path.Combine(secondRoot, "Progress", "tutorial_intro.json")));
        }

        [Test]
        public void AccountUsername_IsNotUsedAsTheInGamePlayerName()
        {
            Assert.IsTrue(_service.TryRegister("LoginUsername", "abcd", out _));
            string accountId = AccountSession.AccountId;
            string playerNameKey = "amath.general.playerName.profile." + accountId;
            PlayerPrefs.SetString(playerNameKey, string.Empty);

            try
            {
                GameSettings.UsePlayerProfile(accountId);
                Assert.AreEqual(string.Empty, GameSettings.PlayerName);
                Assert.IsTrue(GameSettings.TrySetPlayerName("Board Name", out _));
                Assert.AreEqual("LoginUsername", AccountSession.Username);
                Assert.AreEqual("Board Name", GameSettings.PlayerName);
            }
            finally
            {
                GameSettings.ClearPlayerProfile();
                PlayerPrefs.DeleteKey(playerNameKey);
            }
        }

        [Test]
        public void CorruptDatabase_FailsClosedWithoutThrowing()
        {
            string accounts = Path.Combine(_root, "Accounts");
            Directory.CreateDirectory(accounts);
            File.WriteAllText(Path.Combine(accounts, "accounts.json"), "{ truncated");

            Assert.DoesNotThrow(() =>
            {
                Assert.IsFalse(_service.TryLogin("Player", "abcd", out AccountError error));
                Assert.AreEqual(AccountError.CorruptData, error);
            });
        }

        [Test]
        public void AccountGate_MasksPassword_AndAsksBeforeRegistration()
        {
            var owner = new GameObject("Account Gate Test");
            try
            {
                var gate = new AccountGateView(new UiFactory(), owner.transform, _service);
                gate.Open();
                gate.UsernameField.text = "NewPlayer";
                gate.PasswordField.text = "abcd";

                Assert.AreEqual(InputField.ContentType.Password, gate.PasswordField.contentType);
                gate.RegisterTab.onClick.Invoke();
                gate.PrimaryButton.onClick.Invoke();

                Assert.IsTrue(gate.Confirmation.activeSelf);
            Assert.IsFalse(File.Exists(Path.Combine(_root, "Accounts", "accounts.json")),
                    "Registration must not happen before the user confirms it.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ProfileStorage_InvalidFolder_ReturnsErrorWithoutThrowing()
        {
            Assert.IsFalse(ProfileStorage.TryGetDirectory("../outside", out string directory, out string error));
            Assert.IsNull(directory);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void ProfileDirectoryUnavailable_DoesNotActivateAccount()
        {
            string blocker = Path.Combine(_root, "users");
            File.WriteAllText(blocker, "not a directory");

            Assert.IsFalse(_service.TryRegister("Blocked", "abcd", out AccountError error));
            Assert.AreEqual(AccountError.StorageUnavailable, error);
            Assert.IsFalse(AccountSession.IsAuthenticated);

            File.Delete(blocker);
            Assert.IsTrue(_service.TryRegister("Blocked", "abcd", out error), error.ToString());
            Assert.IsTrue(AccountSession.IsAuthenticated);
        }

        [Test]
        public void CopyOneAccount_RequiresPasswordOnNewMachine_ThenAutoLogsIn()
        {
            Assert.IsTrue(_service.TryRegister("First", "first-pass", out _));
            string first = AccountSession.ProfileRoot;
            string firstId = AccountSession.AccountId;
            GameSettings.UsePlayerProfile(firstId);
            Assert.IsTrue(GameSettings.TrySetPlayerName("Forest Fox", out _));
            string firstGuid = AMath.Utilities.LocalIdentity.PersistentGuid;
            File.WriteAllText(Path.Combine(ProfileStorage.GetDirectory("Progress"), "tutorial_intro.json"), "complete");
            File.WriteAllText(Path.Combine(ProfileStorage.GetDirectory("Saves"), "match.json"), "saved match");
            File.WriteAllText(Path.Combine(ProfileStorage.GetDirectory("History"), "replay.json"), "archived replay");
            _service.Logout();
            GameSettings.ClearPlayerProfile();

            Assert.IsTrue(_service.TryRegister("Second", "second-pass", out _));
            _service.Logout();

            string destination = Path.Combine(Path.GetTempPath(), "amath-portable-" + Guid.NewGuid().ToString("N"));
            string machine = Path.Combine(destination, "machine");
            try
            {
                string imported = Path.Combine(destination, "save", "users", Path.GetFileName(first));
                CopyDirectory(first, imported);
                var moved = new LocalAccountService(Path.Combine(destination, "save"), machine);
                Assert.IsFalse(moved.TryAutoLogin(out _));
                Assert.IsFalse(moved.TryLogin("Second", "second-pass", out _));
                Assert.IsTrue(moved.TryLogin("First", "first-pass", out AccountError error), error.ToString());
                GameSettings.UsePlayerProfile(AccountSession.AccountId);
                Assert.AreEqual("Forest Fox", GameSettings.PlayerName);
                Assert.AreEqual(firstGuid, AMath.Utilities.LocalIdentity.PersistentGuid);
                Assert.IsTrue(File.Exists(Path.Combine(AccountSession.ProfileRoot, "Progress", "tutorial_intro.json")));
                Assert.IsTrue(File.Exists(Path.Combine(AccountSession.ProfileRoot, "Saves", "match.json")));
                Assert.IsTrue(File.Exists(Path.Combine(AccountSession.ProfileRoot, "History", "replay.json")));
                Assert.IsTrue(new LocalAccountService(Path.Combine(destination, "save"), machine).TryAutoLogin(out _));
                moved.Logout();
            }
            finally
            {
                GameSettings.ClearPlayerProfile();
                if (Directory.Exists(destination)) Directory.Delete(destination, true);
            }
        }

        [Test]
        public void DuplicateImportedUsername_FailsClosed()
        {
            Assert.IsTrue(_service.TryRegister("Duplicate", "abcd", out _));
            string source = AccountSession.ProfileRoot;
            _service.Logout();
            string copy = Path.Combine(_root, "users", "copy--" + Guid.NewGuid().ToString("N"));
            CopyDirectory(source, copy);
            Assert.IsFalse(_service.TryLogin("Duplicate", "abcd", out AccountError error));
            Assert.AreEqual(AccountError.AccountConflict, error);
        }

        [Test]
        public void DifferentIdWithSameUsername_AlsoFailsClosed()
        {
            Assert.IsTrue(_service.TryRegister("Duplicate", "abcd", out _));
            string source = AccountSession.ProfileRoot;
            string oldId = AccountSession.AccountId;
            _service.Logout();
            string newId = Guid.NewGuid().ToString("N");
            string copy = Path.Combine(_root, "users", "Duplicate--" + newId);
            CopyDirectory(source, copy);
            string accountPath = Path.Combine(copy, "account.json");
            File.WriteAllText(accountPath, File.ReadAllText(accountPath).Replace(oldId, newId));
            Assert.IsFalse(_service.TryLogin("Duplicate", "abcd", out AccountError error));
            Assert.AreEqual(AccountError.AccountConflict, error);
        }

        [Test]
        public void LegacyAccount_IsCopiedWithoutDeletingOriginal()
        {
            Assert.IsTrue(_service.TryRegister("Old Player", "abcd", out _));
            string id = AccountSession.AccountId;
            string accountJson = File.ReadAllText(Path.Combine(AccountSession.ProfileRoot, "account.json"));
            _service.Logout();

            string destination = Path.Combine(Path.GetTempPath(), "amath-migration-" + Guid.NewGuid().ToString("N"));
            string oldRoot = Path.Combine(destination, "old-machine");
            string saveRoot = Path.Combine(destination, "save");
            string oldAccounts = Path.Combine(oldRoot, "Accounts");
            string oldProfile = Path.Combine(oldRoot, "Profiles", id);
            string nameKey = "amath.general.playerName.profile." + id;
            string oldGuid = Guid.NewGuid().ToString("N");
            bool hadGuid = PlayerPrefs.HasKey("amath.player.guid");
            string originalGuid = PlayerPrefs.GetString("amath.player.guid", string.Empty);
            try
            {
                Directory.CreateDirectory(oldAccounts);
                Directory.CreateDirectory(oldProfile);
                File.WriteAllText(Path.Combine(oldAccounts, "accounts.json"),
                    "{\"Schema\":1,\"Accounts\":[" + accountJson + "]}");
                File.WriteAllText(Path.Combine(oldProfile, "old-save.txt"), "preserved");
                PlayerPrefs.SetString(nameKey, "Old Board Name");
                PlayerPrefs.SetString("amath.player.guid", oldGuid);

                var migrated = new LocalAccountService(saveRoot, oldRoot);
                Assert.IsTrue(migrated.TryLogin("Old Player", "abcd", out AccountError error), error.ToString());
                Assert.IsTrue(File.Exists(Path.Combine(AccountSession.ProfileRoot, "old-save.txt")));
                Assert.IsTrue(File.Exists(Path.Combine(oldProfile, "old-save.txt")));
                Assert.IsTrue(File.Exists(Path.Combine(oldAccounts, "accounts.json")));
                Assert.IsTrue(PortableProfile.TryLoad(AccountSession.ProfileRoot, out PortableProfileData profile, out _));
                Assert.AreEqual("Old Board Name", profile.PlayerName);
                Assert.AreEqual(oldGuid, profile.PersistentGuid);
                migrated.Logout();
            }
            finally
            {
                PlayerPrefs.DeleteKey(nameKey);
                if (hadGuid) PlayerPrefs.SetString("amath.player.guid", originalGuid);
                else PlayerPrefs.DeleteKey("amath.player.guid");
                if (Directory.Exists(destination)) Directory.Delete(destination, true);
            }
        }

        [Test]
        public void UnwritableSaveRoot_ReturnsStorageErrorWithoutLocalLowFallback()
        {
            string blocker = Path.Combine(_root, "save-blocked");
            File.WriteAllText(blocker, "file, not directory");
            var service = new LocalAccountService(blocker, Path.Combine(_root, "machine"));
            Assert.IsFalse(service.TryRegister("Player", "abcd", out AccountError error));
            Assert.AreEqual(AccountError.StorageUnavailable, error);
            Assert.IsFalse(Directory.Exists(Path.Combine(_root, "machine", "Profiles")));
        }

        [Test]
        public void CorruptPortableAccount_DoesNotGetOverwritten()
        {
            Assert.IsTrue(_service.TryRegister("Player", "abcd", out _));
            string path = Path.Combine(AccountSession.ProfileRoot, "account.json");
            _service.Logout();
            File.WriteAllText(path, "{ broken");
            Assert.IsFalse(_service.TryLogin("Player", "abcd", out AccountError error));
            Assert.AreEqual(AccountError.CorruptData, error);
            Assert.AreEqual("{ broken", File.ReadAllText(path));
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            foreach (string child in Directory.GetDirectories(source))
                CopyDirectory(child, Path.Combine(destination, Path.GetFileName(child)));
        }
    }
}
