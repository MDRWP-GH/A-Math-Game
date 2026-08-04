using System;
using System.Collections.Generic;
using UnityEngine;

namespace AMath.Save
{
    /// <summary>
    /// One upgrade step from schema version <see cref="FromVersion"/> to
    /// <see cref="FromVersion"/> + 1, operating on the raw JSON text so it can
    /// rename/add/remove fields freely regardless of the current C# model.
    /// </summary>
    public interface ISaveMigrationStep
    {
        /// <summary>Schema version this step upgrades from.</summary>
        int FromVersion { get; }

        /// <summary>Transforms the raw JSON into the next schema version.</summary>
        string Apply(string json);
    }

    /// <summary>
    /// Upgrades old save files to the current schema so a game update never
    /// invalidates existing saves. Steps are chained: a v1 file loaded by a
    /// build with schema v4 runs 1→2, 2→3, 3→4 in order.
    /// </summary>
    public sealed class SaveMigrator
    {
        #region Version probe

        [Serializable]
        private sealed class VersionProbe
        {
            // Field name must match SaveFile.SaveVersion.
            public int SaveVersion = -1;
        }

        #endregion

        #region Fields

        private readonly Dictionary<int, ISaveMigrationStep> _steps = new();

        #endregion

        #region API

        /// <summary>Registers a migration step (one per source version).</summary>
        public void Register(ISaveMigrationStep step)
        {
            if (_steps.ContainsKey(step.FromVersion))
                throw new InvalidOperationException($"Duplicate migration from v{step.FromVersion}.");
            _steps.Add(step.FromVersion, step);
        }

        /// <summary>
        /// Upgrades raw save JSON to <see cref="SaveFile.CurrentVersion"/>.
        /// Returns false when the file is newer than this build or a step is missing.
        /// </summary>
        public bool TryMigrate(string json, out string migratedJson, out string error)
        {
            migratedJson = json;
            error = null;

            var probe = JsonUtility.FromJson<VersionProbe>(json);
            int version = probe?.SaveVersion ?? -1;

            if (version < 1)
            {
                error = "Save file has no valid version.";
                return false;
            }

            if (version > SaveFile.CurrentVersion)
            {
                error = $"Save was written by a newer game version (schema v{version}).";
                return false;
            }

            while (version < SaveFile.CurrentVersion)
            {
                if (!_steps.TryGetValue(version, out ISaveMigrationStep step))
                {
                    error = $"No migration path from schema v{version}.";
                    return false;
                }

                migratedJson = step.Apply(migratedJson);
                version++;
            }

            return true;
        }

        #endregion
    }
}
