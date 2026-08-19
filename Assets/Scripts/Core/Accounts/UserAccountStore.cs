using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AMath.Core.Accounts
{
    /// <summary>One locally registered player account (device-scoped store).</summary>
    [Serializable]
    public sealed class UserAccountRecord
    {
        public string AccountId;
        public string Username;
        public string DisplayName;
        public string PasswordHash;
        public string Salt;
        public long CreatedUtcTicks;
    }

    [Serializable]
    internal sealed class UserAccountDatabase
    {
        public List<UserAccountRecord> Accounts = new();
    }

    /// <summary>
    /// Local account registry: register, sign in and sign out without a remote
    /// server. Passwords are salted SHA-256 hashes stored on this machine only.
    /// </summary>
    public static class UserAccountStore
    {
        private const string DatabaseFileName = "accounts.json";
        private const string SessionUsernameKey = "amath.account.session";

        private static UserAccountDatabase _database;
        private static string _sessionUsername;

        /// <summary>Username of the signed-in account, or null when logged out.</summary>
        public static string SessionUsername => _sessionUsername ??= LoadSessionUsername();

        /// <summary>True when a local account session is active.</summary>
        public static bool IsSignedIn => !string.IsNullOrEmpty(SessionUsername);

        /// <summary>Display name of the signed-in account, or null.</summary>
        public static string SessionDisplayName
        {
            get
            {
                if (!TryFind(SessionUsername, out UserAccountRecord record))
                    return null;
                return record.DisplayName;
            }
        }

        public static bool Register(string username, string password, string displayName, out string error)
        {
            error = null;
            username = NormalizeUsername(username);
            displayName = NormalizeDisplayName(displayName);

            if (!IsValidUsername(username))
            {
                error = "Invalid username.";
                return false;
            }

            if (string.IsNullOrEmpty(password) || password.Length < 4)
            {
                error = "Password too short.";
                return false;
            }

            UserAccountDatabase db = LoadDatabase();
            if (db.Accounts.Exists(a => string.Equals(a.Username, username, StringComparison.OrdinalIgnoreCase)))
            {
                error = "Username already exists.";
                return false;
            }

            string salt = Guid.NewGuid().ToString("N");
            db.Accounts.Add(new UserAccountRecord
            {
                AccountId = Guid.NewGuid().ToString("N"),
                Username = username,
                DisplayName = displayName,
                Salt = salt,
                PasswordHash = HashPassword(username, password, salt),
                CreatedUtcTicks = DateTime.UtcNow.Ticks
            });

            if (!TrySaveDatabase(db, out error))
                return false;

            SetSession(username);
            return true;
        }

        public static bool SignIn(string username, string password, out string error)
        {
            error = null;
            username = NormalizeUsername(username);

            if (!TryFind(username, out UserAccountRecord record))
            {
                error = "Account not found.";
                return false;
            }

            string expected = HashPassword(record.Username, password, record.Salt);
            if (!FixedTimeEquals(expected, record.PasswordHash))
            {
                error = "Incorrect password.";
                return false;
            }

            SetSession(record.Username);
            return true;
        }

        public static void SignOut()
        {
            _sessionUsername = null;
            PlayerPrefs.DeleteKey(SessionUsernameKey);
            PlayerPrefs.Save();
        }

        public static bool TryGetSessionAccount(out UserAccountRecord account)
        {
            account = null;
            return IsSignedIn && TryFind(SessionUsername, out account);
        }

        private static bool TryFind(string username, out UserAccountRecord record)
        {
            record = null;
            if (string.IsNullOrEmpty(username))
                return false;

            foreach (UserAccountRecord candidate in LoadDatabase().Accounts)
            {
                if (string.Equals(candidate.Username, username, StringComparison.OrdinalIgnoreCase))
                {
                    record = candidate;
                    return true;
                }
            }

            return false;
        }

        private static UserAccountDatabase LoadDatabase()
        {
            if (_database != null)
                return _database;

            string path = DatabasePath();
            if (!File.Exists(path))
            {
                _database = new UserAccountDatabase();
                return _database;
            }

            try
            {
                _database = JsonUtility.FromJson<UserAccountDatabase>(File.ReadAllText(path))
                            ?? new UserAccountDatabase();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Accounts] Could not read database: {ex.Message}");
                _database = new UserAccountDatabase();
            }

            return _database;
        }

        private static bool TrySaveDatabase(UserAccountDatabase db, out string error)
        {
            error = null;
            _database = db;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath()));
                File.WriteAllText(DatabasePath(), JsonUtility.ToJson(db));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void SetSession(string username)
        {
            _sessionUsername = username;
            PlayerPrefs.SetString(SessionUsernameKey, username);
            PlayerPrefs.Save();
        }

        private static string LoadSessionUsername()
        {
            string value = PlayerPrefs.GetString(SessionUsernameKey, string.Empty);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static string DatabasePath() =>
            Path.Combine(Application.persistentDataPath, DatabaseFileName);

        private static string NormalizeUsername(string username) =>
            username?.Trim().ToLowerInvariant();

        private static string NormalizeDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return "Player";
            return displayName.Trim();
        }

        private static bool IsValidUsername(string username) =>
            !string.IsNullOrEmpty(username)
            && username.Length >= 3
            && username.Length <= 20;

        private static string HashPassword(string username, string password, string salt)
        {
            string payload = $"{username}:{password}:{salt}";
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                builder.Append(b.ToString("x2"));
            return builder.ToString();
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;

            int diff = 0;
            for (int i = 0; i < left.Length; i++)
                diff |= left[i] ^ right[i];
            return diff == 0;
        }
    }
}
