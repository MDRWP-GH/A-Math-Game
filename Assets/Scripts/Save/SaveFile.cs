using System;
using AMath.Core.Snapshot;
using AMath.Replay;

namespace AMath.Save
{
    /// <summary>
    /// The versioned on-disk save format (JSON).
    ///
    /// Every machine writes its own copy after every turn, so any peer holds
    /// everything needed to (a) resume after a crash, (b) become the new host
    /// after migration, or (c) finish a match locally when the network never
    /// comes back. <see cref="SaveVersion"/> is a *schema* version consumed by
    /// <see cref="SaveMigrator"/>; <see cref="GameVersion"/> records which
    /// build produced the file.
    /// </summary>
    [Serializable]
    public sealed class SaveFile
    {
        /// <summary>Current save schema version. Bump when the format changes and add a migration step.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Schema version of this file.</summary>
        public int SaveVersion = CurrentVersion;

        /// <summary>Application.version of the build that wrote the file.</summary>
        public string GameVersion;

        /// <summary>UTC ticks at save time.</summary>
        public long TimestampUtcTicks;

        // Room metadata so a migrated host can re-create the same room.
        public string RoomName;
        public string RoomCode;
        public int MaxPlayers;

        /// <summary>
        /// Game port the room listened on. Saves written before this field
        /// existed deserialize to 0, which callers read as "use the default
        /// port" — so no schema bump is needed.
        /// </summary>
        public int Port;

        /// <summary>Complete game state (board, racks, bag, scores, turn, RNG state).</summary>
        public GameStateSnapshot State;

        /// <summary>Full replay log up to the save point.</summary>
        public ReplayLog Replay;
    }
}
