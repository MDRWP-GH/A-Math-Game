using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace AMath.Accounts
{
    /// <summary>Player data lives beside the Windows executable, not in Unity's hidden LocalLow folder.</summary>
    public static class PortableSaveStorage
    {
        public static string Root => Application.isEditor || Application.platform != RuntimePlatform.WindowsPlayer
            ? Application.persistentDataPath
            : Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, "save");

        public static bool TryEnsureWritable(string root, out string error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(root);
                string probe = Path.Combine(root, ".write-test-" + Guid.NewGuid().ToString("N"));
                using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException
                                       || ex is ArgumentException || ex is NotSupportedException)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    [Serializable]
    public sealed class PortableProfileData
    {
        public string PlayerName;
        public string PersistentGuid;
    }

    public static class PortableProfile
    {
        private const string FileName = "profile.json";

        public static bool TryLoad(string profileRoot, out PortableProfileData data, out string error)
        {
            data = null;
            error = null;
            try
            {
                string path = Path.Combine(profileRoot, FileName);
                if (!File.Exists(path))
                {
                    error = "Profile metadata is missing.";
                    return false;
                }
                data = JsonUtility.FromJson<PortableProfileData>(File.ReadAllText(path));
                if (data == null || !Guid.TryParseExact(data.PersistentGuid, "N", out _))
                {
                    data = null;
                    error = "Profile metadata is corrupt.";
                    return false;
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException
                                       || ex is ArgumentException || ex is NotSupportedException)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TrySave(string profileRoot, PortableProfileData data, out string error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(profileRoot);
                string path = Path.Combine(profileRoot, FileName);
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data), Encoding.UTF8);
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException
                                       || ex is ArgumentException || ex is NotSupportedException)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
