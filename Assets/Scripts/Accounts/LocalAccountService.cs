using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AMath.Accounts
{
    public enum AccountError
    {
        None,
        UsernameRequired,
        UsernameTooLong,
        PasswordRequired,
        PasswordTooShort,
        UsernameTaken,
        InvalidCredentials,
        StorageUnavailable,
        CorruptData,
        AutoLoginExpired,
        AccountConflict,
        MigrationIncomplete
    }

    /// <summary>
    /// Offline account storage used to isolate saves on a shared installation.
    /// Passwords use salted PBKDF2 hashes and auto-login uses a revocable random
    /// token; neither the password nor a reusable password equivalent is stored.
    /// </summary>
    public sealed class LocalAccountService
    {
        private const int SchemaVersion = 1;
        private const int PasswordIterations = 100000;
        private const int MaxUsernameLength = 24;
        private const int MinPasswordLength = 4;

        private readonly string _dataRoot;
        private readonly string _legacyRoot;
        private readonly string _usersRoot;
        private readonly string _accountRoot;
        private readonly string _databasePath;
        private readonly string _sessionPath;
        private readonly string _migrationMarkerPath;

        public LocalAccountService(string dataRootOverride = null, string machineRootOverride = null)
        {
            _dataRoot = string.IsNullOrWhiteSpace(dataRootOverride)
                ? PortableSaveStorage.Root
                : Path.GetFullPath(dataRootOverride);
            _legacyRoot = !string.IsNullOrWhiteSpace(machineRootOverride)
                ? Path.GetFullPath(machineRootOverride)
                : string.IsNullOrWhiteSpace(dataRootOverride) ? Application.persistentDataPath : _dataRoot;
            _usersRoot = Path.Combine(_dataRoot, "users");
            _accountRoot = Path.Combine(_legacyRoot, "Accounts");
            _databasePath = Path.Combine(_accountRoot, "accounts.json");
            _sessionPath = Path.Combine(_accountRoot, "auto_login.json");
            _migrationMarkerPath = Path.Combine(_dataRoot, ".legacy-migration-incomplete");
        }

        public bool ValidateRegistrationInput(string username, string password, out AccountError error)
        {
            if (!ValidateCredentials(username, password, out error))
                return false;

            if (!TryLoadDatabase(out AccountDatabase database, out error))
                return false;

            string normalized = NormalizeUsername(username);
            if (FindAccount(database, normalized) != null)
            {
                error = AccountError.UsernameTaken;
                return false;
            }

            error = AccountError.None;
            return true;
        }

        public bool TryRegister(string username, string password, out AccountError error)
        {
            if (!ValidateRegistrationInput(username, password, out error))
                return false;

            if (!TryLoadDatabase(out AccountDatabase database, out error))
                return false;

            byte[] salt = RandomBytes(16);
            var account = new AccountRecord
            {
                AccountId = Guid.NewGuid().ToString("N"),
                Username = username.Trim(),
                NormalizedUsername = NormalizeUsername(username),
                PasswordSalt = Convert.ToBase64String(salt),
                PasswordHash = Convert.ToBase64String(HashPassword(password, salt, PasswordIterations)),
                PasswordIterations = PasswordIterations,
                CreatedUtcTicks = DateTime.UtcNow.Ticks
            };
            if (!TryCreateAccount(account, out error))
                return false;

            return TryStartSession(database, account, out error);
        }

        public bool TryLogin(string username, string password, out AccountError error)
        {
            if (!ValidateCredentials(username, password, out error))
                return false;

            if (!TryLoadDatabase(out AccountDatabase database, out error))
                return false;

            AccountRecord account = FindAccount(database, NormalizeUsername(username));
            if (account == null || !PasswordMatches(account, password))
            {
                error = AccountError.InvalidCredentials;
                return false;
            }

            return TryStartSession(database, account, out error);
        }

        public bool TryAutoLogin(out AccountError error)
        {
            error = AccountError.None;
            if (!File.Exists(_sessionPath))
            {
                // The account gate calls this on startup even for a new player:
                // create/check the visible save folder before presenting login.
                TryLoadDatabase(out _, out error);
                return false;
            }

            try
            {
                AutoLoginFile session = JsonUtility.FromJson<AutoLoginFile>(File.ReadAllText(_sessionPath));
                if (session == null || string.IsNullOrWhiteSpace(session.AccountId) || string.IsNullOrWhiteSpace(session.Token))
                {
                    DeleteSessionFile();
                    error = AccountError.AutoLoginExpired;
                    return false;
                }

                if (!TryLoadDatabase(out AccountDatabase database, out error))
                    return false;

                AccountRecord account = database.Accounts.Find(row => row != null && row.AccountId == session.AccountId);
                byte[] token;
                byte[] expected;
                if (account == null || string.IsNullOrEmpty(account.SessionTokenHash) ||
                    !TryFromBase64(session.Token, out token) ||
                    !TryFromBase64(account.SessionTokenHash, out expected) ||
                    !FixedTimeEquals(Sha256(token), expected))
                {
                    DeleteSessionFile();
                    error = AccountError.AutoLoginExpired;
                    return false;
                }

                Activate(account);
                return true;
            }
            catch (Exception ex) when (IsStorageOrDataException(ex))
            {
                Debug.LogWarning($"[Accounts] Auto-login could not be read: {ex.Message}");
                DeleteSessionFile();
                error = ex is IOException || ex is UnauthorizedAccessException
                    ? AccountError.StorageUnavailable
                    : AccountError.CorruptData;
                return false;
            }
        }

        public void Logout()
        {
            try
            {
                if (TryLoadDatabase(out AccountDatabase database, out _))
                {
                    AccountRecord account = database.Accounts.Find(row => row != null && row.AccountId == AccountSession.AccountId);
                    if (account != null)
                    {
                        account.SessionTokenHash = null;
                        TrySaveAccount(account, out _);
                    }
                }
            }
            finally
            {
                DeleteSessionFile();
                AccountSession.Clear();
            }
        }

        private bool TryStartSession(AccountDatabase database, AccountRecord account, out AccountError error)
        {
            byte[] token = RandomBytes(32);
            account.SessionTokenHash = Convert.ToBase64String(Sha256(token));
            if (!TrySaveAccount(account, out error))
                return false;

            try
            {
                Directory.CreateDirectory(_accountRoot);
                WriteAtomic(_sessionPath, JsonUtility.ToJson(new AutoLoginFile
                {
                    AccountId = account.AccountId,
                    Token = Convert.ToBase64String(token)
                }));
                Activate(account);
                error = AccountError.None;
                return true;
            }
            catch (Exception ex) when (IsStorageOrDataException(ex))
            {
                Debug.LogWarning($"[Accounts] Auto-login session could not be written: {ex.Message}");
                account.SessionTokenHash = null;
                TrySaveAccount(account, out _);
                DeleteSessionFile();
                error = AccountError.StorageUnavailable;
                return false;
            }
        }

        private void Activate(AccountRecord account)
        {
            string profileRoot = account.FolderPath;
            AccountSession.Activate(account.AccountId, account.Username, profileRoot);
        }

        private bool TryMigrateLegacyData(out AccountError error)
        {
            error = AccountError.None;
            if (File.Exists(_migrationMarkerPath))
            {
                error = AccountError.MigrationIncomplete;
                return false;
            }
            if (!File.Exists(_databasePath) || Directory.GetDirectories(_usersRoot).Length > 0)
                return true;

            try
            {
                AccountDatabase legacy = JsonUtility.FromJson<AccountDatabase>(File.ReadAllText(_databasePath));
                if (legacy == null || legacy.Schema != SchemaVersion || legacy.Accounts == null)
                {
                    error = AccountError.CorruptData;
                    return false;
                }

                var legacyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var legacyNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (AccountRecord account in legacy.Accounts)
                {
                    if (account == null || !Guid.TryParseExact(account.AccountId, "N", out _) ||
                        string.IsNullOrWhiteSpace(account.Username) ||
                        !string.Equals(account.NormalizedUsername, NormalizeUsername(account.Username), StringComparison.Ordinal) ||
                        !TryFromBase64(account.PasswordSalt, out _) ||
                        !TryFromBase64(account.PasswordHash, out _) ||
                        account.PasswordIterations < 10000)
                    {
                        error = AccountError.CorruptData;
                        return false;
                    }
                    if (!legacyIds.Add(account.AccountId) ||
                        !legacyNames.Add(NormalizeUsername(account.Username)))
                    {
                        error = AccountError.AccountConflict;
                        return false;
                    }
                }

                File.WriteAllText(_migrationMarkerPath, "Legacy account import was interrupted. Keep the original LocalLow data and contact support before changing save/users.");
                foreach (AccountRecord account in legacy.Accounts)
                {
                    if (account == null || !Guid.TryParseExact(account.AccountId, "N", out _))
                    {
                        error = AccountError.CorruptData;
                        return false;
                    }
                    string destination = AccountFolder(account);
                    if (Directory.Exists(destination))
                    {
                        error = AccountError.AccountConflict;
                        return false;
                    }
                    string staging = destination + ".importing";
                    if (Directory.Exists(staging))
                    {
                        error = AccountError.StorageUnavailable;
                        return false;
                    }
                    Directory.CreateDirectory(staging);
                    string source = Path.Combine(_legacyRoot, "Profiles", account.AccountId);
                    CopyDirectoryWithoutOverwrite(source, staging);
                    string oldName = PlayerPrefs.GetString("amath.general.playerName.profile." + account.AccountId, string.Empty);
                    string oldGuid = PlayerPrefs.GetString("amath.player.guid", string.Empty);
                    if (!Guid.TryParseExact(oldGuid, "N", out _))
                        oldGuid = Guid.NewGuid().ToString("N");
                    if (!PortableProfile.TrySave(staging, new PortableProfileData
                        { PlayerName = oldName, PersistentGuid = oldGuid }, out _))
                    {
                        error = AccountError.StorageUnavailable;
                        return false;
                    }
                    File.WriteAllText(Path.Combine(staging, "account.json"), JsonUtility.ToJson(account), Encoding.UTF8);
                    Directory.Move(staging, destination);
                }
                File.Delete(_migrationMarkerPath);
                return true;
            }
            catch (Exception ex) when (IsStorageOrDataException(ex))
            {
                Debug.LogWarning($"[Accounts] Legacy data could not be copied: {ex.Message}");
                error = ex is ArgumentException ? AccountError.CorruptData : AccountError.StorageUnavailable;
                return false;
            }
        }

        private bool TryLoadDatabase(out AccountDatabase database, out AccountError error)
        {
            database = null;
            error = AccountError.None;
            try
            {
                if (!PortableSaveStorage.TryEnsureWritable(_dataRoot, out string storageError))
                {
                    Debug.LogWarning("[Accounts] Save folder is not writable: " + storageError);
                    error = AccountError.StorageUnavailable;
                    return false;
                }
                Directory.CreateDirectory(_usersRoot);
                if (!TryMigrateLegacyData(out error))
                    return false;

                database = new AccountDatabase { Schema = SchemaVersion };
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var names = new HashSet<string>(StringComparer.Ordinal);
                bool hasMismatchedFolder = false;
                foreach (string folder in Directory.GetDirectories(_usersRoot))
                {
                    // A process can terminate between creating the staging
                    // folder and atomically moving it into place. It is not
                    // an account yet, and must not lock out existing users.
                    if (folder.EndsWith(".creating", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.LogWarning($"[Accounts] Incomplete account folder was left untouched: {folder}");
                        continue;
                    }

                    string path = Path.Combine(folder, "account.json");
                    if (!File.Exists(path))
                    {
                        database = null;
                        error = AccountError.CorruptData;
                        return false;
                    }
                    AccountRecord account = JsonUtility.FromJson<AccountRecord>(File.ReadAllText(path));
                    if (account == null || !Guid.TryParseExact(account.AccountId, "N", out _)
                        || string.IsNullOrWhiteSpace(account.Username)
                        || string.IsNullOrWhiteSpace(account.NormalizedUsername)
                        || !string.Equals(account.NormalizedUsername, NormalizeUsername(account.Username), StringComparison.Ordinal)
                        || !TryFromBase64(account.PasswordSalt, out _)
                        || !TryFromBase64(account.PasswordHash, out _)
                        || !PortableProfile.TryLoad(folder, out _, out _))
                    {
                        database = null;
                        error = AccountError.CorruptData;
                        return false;
                    }
                    if (!ids.Add(account.AccountId) || !names.Add(account.NormalizedUsername))
                    {
                        database = null;
                        error = AccountError.AccountConflict;
                        return false;
                    }
                    if (!string.Equals(folder, AccountFolder(account), StringComparison.OrdinalIgnoreCase))
                        hasMismatchedFolder = true;
                    account.FolderPath = folder;
                    database.Accounts.Add(account);
                }
                if (hasMismatchedFolder)
                {
                    database = null;
                    error = AccountError.CorruptData;
                    return false;
                }
                return true;
            }
            catch (Exception ex) when (IsStorageOrDataException(ex))
            {
                Debug.LogWarning($"[Accounts] Account data could not be read: {ex.Message}");
                error = ex is IOException || ex is UnauthorizedAccessException
                    ? AccountError.StorageUnavailable
                    : AccountError.CorruptData;
                return false;
            }
        }

        private bool TryCreateAccount(AccountRecord account, out AccountError error)
        {
            error = AccountError.None;
            string folder = AccountFolder(account);
            string staging = folder + ".creating";
            bool ownsStaging = false;
            try
            {
                Directory.CreateDirectory(_usersRoot);
                if (Directory.Exists(folder) || Directory.Exists(staging))
                {
                    error = AccountError.AccountConflict;
                    return false;
                }
                Directory.CreateDirectory(staging);
                ownsStaging = true;
                // Earlier builds placed unaffiliated saves in LocalLow before accounts existed.
                // Claim them for the first account only; never delete the original files.
                if (Directory.GetDirectories(_usersRoot).Length == 1 && !File.Exists(_databasePath))
                {
                    foreach (string legacyFolder in new[] { "Saves", "History", "Progress" })
                        CopyDirectoryWithoutOverwrite(Path.Combine(_legacyRoot, legacyFolder),
                            Path.Combine(staging, legacyFolder));
                }
                if (!PortableProfile.TrySave(staging, new PortableProfileData
                    { PlayerName = string.Empty, PersistentGuid = Guid.NewGuid().ToString("N") }, out _))
                {
                    error = AccountError.StorageUnavailable;
                    return false;
                }
                File.WriteAllText(Path.Combine(staging, "account.json"), JsonUtility.ToJson(account), Encoding.UTF8);
                Directory.Move(staging, folder);
                ownsStaging = false;
                account.FolderPath = folder;
                error = AccountError.None;
                return true;
            }
            catch (Exception ex) when (IsStorageOrDataException(ex))
            {
                Debug.LogWarning($"[Accounts] Account could not be created: {ex.Message}");
                error = AccountError.StorageUnavailable;
                return false;
            }
            finally
            {
                if (ownsStaging && Directory.Exists(staging))
                {
                    try { Directory.Delete(staging, true); }
                    catch (Exception ex) when (IsStorageOrDataException(ex))
                    {
                        Debug.LogWarning($"[Accounts] Incomplete new account folder remains: {staging}: {ex.Message}");
                    }
                }
            }
        }

        private bool TrySaveAccount(AccountRecord account, out AccountError error)
        {
            try
            {
                WriteAtomic(Path.Combine(account.FolderPath, "account.json"), JsonUtility.ToJson(account));
                error = AccountError.None;
                return true;
            }
            catch (Exception ex) when (IsStorageOrDataException(ex))
            {
                Debug.LogWarning($"[Accounts] Account data could not be written: {ex.Message}");
                error = AccountError.StorageUnavailable;
                return false;
            }
        }

        private string AccountFolder(AccountRecord account)
        {
            var slug = new StringBuilder();
            foreach (char c in account.Username.Trim())
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') slug.Append(c);
                else if (char.IsWhiteSpace(c)) slug.Append('-');
            }
            if (slug.Length == 0) slug.Append("player");
            return Path.Combine(_usersRoot, slug + "--" + account.AccountId);
        }

        private static bool ValidateCredentials(string username, string password, out AccountError error)
        {
            string trimmed = username?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                error = AccountError.UsernameRequired;
            else if (trimmed.Length > MaxUsernameLength)
                error = AccountError.UsernameTooLong;
            else if (string.IsNullOrEmpty(password))
                error = AccountError.PasswordRequired;
            else if (password.Length < MinPasswordLength)
                error = AccountError.PasswordTooShort;
            else
                error = AccountError.None;
            return error == AccountError.None;
        }

        private static string NormalizeUsername(string username) =>
            username.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();

        private static AccountRecord FindAccount(AccountDatabase database, string normalizedUsername) =>
            database.Accounts.Find(row => row != null &&
                string.Equals(row.NormalizedUsername, normalizedUsername, StringComparison.Ordinal));

        private static bool PasswordMatches(AccountRecord account, string password)
        {
            if (!TryFromBase64(account.PasswordSalt, out byte[] salt) ||
                !TryFromBase64(account.PasswordHash, out byte[] expected) ||
                account.PasswordIterations < 10000)
                return false;

            byte[] actual = HashPassword(password, salt, account.PasswordIterations);
            return FixedTimeEquals(actual, expected);
        }

        private static byte[] HashPassword(string password, byte[] salt, int iterations)
        {
            using var derive = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            return derive.GetBytes(32);
        }

        private static byte[] RandomBytes(int length)
        {
            var bytes = new byte[length];
            using RandomNumberGenerator random = RandomNumberGenerator.Create();
            random.GetBytes(bytes);
            return bytes;
        }

        private static byte[] Sha256(byte[] bytes)
        {
            using SHA256 sha = SHA256.Create();
            return sha.ComputeHash(bytes);
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;

            int difference = 0;
            for (int i = 0; i < left.Length; i++)
                difference |= left[i] ^ right[i];
            return difference == 0;
        }

        private static bool TryFromBase64(string value, out byte[] bytes)
        {
            try
            {
                bytes = Convert.FromBase64String(value ?? string.Empty);
                return bytes.Length > 0;
            }
            catch (FormatException)
            {
                bytes = null;
                return false;
            }
        }

        private static void CopyDirectoryWithoutOverwrite(string source, string destination)
        {
            if (File.Exists(source))
                throw new IOException("Expected a legacy folder but found a file: " + source);
            if (!Directory.Exists(source))
                return;

            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
            {
                string target = Path.Combine(destination, Path.GetFileName(file));
                if (!File.Exists(target))
                    File.Copy(file, target, overwrite: false);
            }

            foreach (string child in Directory.GetDirectories(source))
            {
                CopyDirectoryWithoutOverwrite(child, Path.Combine(destination, Path.GetFileName(child)));
            }
        }

        private void DeleteSessionFile()
        {
            try
            {
                if (File.Exists(_sessionPath))
                    File.Delete(_sessionPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Accounts] Auto-login session could not be removed: {ex.Message}");
            }
        }

        private static void WriteAtomic(string path, string contents)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, contents, Encoding.UTF8);
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }

        private static bool IsStorageOrDataException(Exception ex) =>
            ex is IOException || ex is UnauthorizedAccessException ||
            ex is ArgumentException || ex is CryptographicException ||
            ex is NotSupportedException;

        [Serializable]
        private sealed class AccountDatabase
        {
            public int Schema = SchemaVersion;
            public List<AccountRecord> Accounts = new();
        }

        [Serializable]
        private sealed class AccountRecord
        {
            [NonSerialized] public string FolderPath;
            public string AccountId;
            public string Username;
            public string NormalizedUsername;
            public string PasswordSalt;
            public string PasswordHash;
            public int PasswordIterations;
            public long CreatedUtcTicks;
            public string SessionTokenHash;
        }

        [Serializable]
        private sealed class AutoLoginFile
        {
            public string AccountId;
            public string Token;
        }
    }
}
