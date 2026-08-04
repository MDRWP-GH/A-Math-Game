using System;
using System.IO;
using System.Text;
using AMath.Tutorial.Interfaces;
using UnityEngine;

namespace AMath.Tutorial.Save
{
    /// <summary>
    /// Persists tutorial progress as one JSON file per tutorial id under
    /// <c>Application.persistentDataPath/Progress</c>. Writes are atomic so
    /// a crash mid-save cannot corrupt the previous good file.
    /// </summary>
    public sealed class TutorialProgressSaveStore : ITutorialSaveStore
    {
        private const string DefaultFolderName = "Progress";

        private readonly string _directory;

        /// <summary>
        /// Creates a store rooted at the default progress folder, or at
        /// <paramref name="directoryOverride"/> when supplied (for tests).
        /// </summary>
        public TutorialProgressSaveStore(string directoryOverride = null)
        {
            _directory = string.IsNullOrWhiteSpace(directoryOverride)
                ? Path.Combine(Application.persistentDataPath, DefaultFolderName)
                : directoryOverride;

            Directory.CreateDirectory(_directory);
        }

        /// <inheritdoc />
        public bool TryLoad(string tutorialId, out TutorialProgressData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(tutorialId))
                return false;

            string path = PathForTutorial(tutorialId);
            if (!File.Exists(path))
                return false;

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[TutorialSave] Could not read '{path}': {ex.Message}");
                return false;
            }

            TutorialProgressData loaded;
            try
            {
                loaded = JsonUtility.FromJson<TutorialProgressData>(json);
            }
            catch (ArgumentException ex)
            {
                Debug.LogWarning($"[TutorialSave] Could not parse '{path}': {ex.Message}");
                return false;
            }

            if (loaded == null ||
                string.IsNullOrWhiteSpace(loaded.TutorialId) ||
                !string.Equals(loaded.TutorialId, tutorialId, StringComparison.Ordinal) ||
                loaded.StepIndex < 0)
            {
                Debug.LogWarning($"[TutorialSave] Progress file '{path}' is corrupt or mismatched.");
                return false;
            }

            data = loaded;
            return true;
        }

        /// <inheritdoc />
        public void Save(string tutorialId, TutorialProgressData data)
        {
            if (string.IsNullOrWhiteSpace(tutorialId))
                throw new ArgumentException("A tutorial id is required.", nameof(tutorialId));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            data.TutorialId = tutorialId;
            if (data.TimestampUtcTicks <= 0)
                data.TimestampUtcTicks = DateTime.UtcNow.Ticks;

            string path = PathForTutorial(tutorialId);

            try
            {
                WriteAtomic(path, JsonUtility.ToJson(data));
            }
            catch (IOException ex)
            {
                Debug.LogError($"[TutorialSave] Could not write '{path}': {ex.Message}");
                throw;
            }
        }

        private string PathForTutorial(string tutorialId) =>
            Path.Combine(_directory, $"tutorial_{SanitizeFileName(tutorialId)}.json");

        private static void WriteAtomic(string path, string contents)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, contents, Encoding.UTF8);

            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }

        private static string SanitizeFileName(string tutorialId)
        {
            var builder = new StringBuilder(tutorialId.Length);
            foreach (char character in tutorialId)
            {
                builder.Append(IsInvalidFileNameChar(character) ? '_' : character);
            }

            string sanitized = builder.ToString().Trim();
            return string.IsNullOrEmpty(sanitized) ? "unknown" : sanitized;
        }

        private static bool IsInvalidFileNameChar(char character)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                if (invalid == character)
                    return true;
            }

            return false;
        }
    }
}
