using System;
using System.IO;
using UnityEngine;

namespace AMath.Accounts
{
    /// <summary>
    /// The locally authenticated profile. Only the account id and display name
    /// live in memory; passwords are never retained after a login attempt.
    /// </summary>
    public static class AccountSession
    {
        public static bool IsAuthenticated => !string.IsNullOrEmpty(AccountId);

        public static string AccountId { get; private set; }

        public static string Username { get; private set; }

        public static string ProfileRoot { get; private set; }

        internal static void Activate(string accountId, string username, string profileRoot)
        {
            if (string.IsNullOrWhiteSpace(accountId))
                throw new ArgumentException("An account id is required.", nameof(accountId));

            Directory.CreateDirectory(profileRoot);
            AccountId = accountId;
            Username = username ?? string.Empty;
            ProfileRoot = profileRoot;
        }

        internal static void Clear()
        {
            AccountId = null;
            Username = null;
            ProfileRoot = null;
        }
    }

    /// <summary>Resolves persistence folders inside the active local profile.</summary>
    public static class ProfileStorage
    {
        public static string GetDirectory(string folderName)
        {
            if (TryGetDirectory(folderName, out string directory, out string error))
                return directory;

            throw new IOException(error ?? "Profile storage is unavailable.");
        }

        /// <summary>Resolves and creates a profile folder without leaking storage exceptions to UI.</summary>
        public static bool TryGetDirectory(string folderName, out string directory, out string error)
        {
            directory = null;
            error = null;

            if (string.IsNullOrWhiteSpace(folderName) ||
                folderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                folderName.Contains("/") || folderName.Contains("\\"))
            {
                error = "A single safe folder name is required.";
                return false;
            }

            try
            {
                // Editor tools can still use the test data path. A Windows player
                // must never write unaffiliated saves outside save/users.
                if (!AccountSession.IsAuthenticated && !Application.isEditor &&
                    Application.platform == RuntimePlatform.WindowsPlayer)
                {
                    error = "Sign in before accessing player storage.";
                    return false;
                }
                string root = AccountSession.IsAuthenticated
                    ? AccountSession.ProfileRoot
                    : PortableSaveStorage.Root;
                directory = Path.Combine(root, folderName);
                Directory.CreateDirectory(directory);
                return true;
            }
            catch (Exception ex) when (ex is IOException
                                       || ex is UnauthorizedAccessException
                                       || ex is NotSupportedException
                                       || ex is ArgumentException)
            {
                directory = null;
                error = ex.Message;
                return false;
            }
        }
    }
}
