using System;
using System.IO;
using AMath.Core.Events;
using AMath.Accounts;
using AMath.Managers;
using AMath.Replay;
using AMath.Utilities;
using UnityEngine;

namespace AMath.Save
{
    /// <summary>
    /// Owns local persistence. Every machine keeps its own save file:
    ///  - autosaves at the end of every turn (subscribes to
    ///    <see cref="TurnResolvedEvent"/>, which fires on host AND clients);
    ///  - saves immediately when the connection drops (backup requirement);
    ///  - loads the most recent save for host migration or crash recovery.
    ///
    /// Writes are atomic (temp file + replace) so a crash mid-write can never
    /// corrupt the previous good save, and old schemas are upgraded through
    /// <see cref="SaveMigrator"/>.
    /// </summary>
    public sealed class SaveManager : IDisposable
    {
        #region Fields

        private readonly IEventBus _eventBus;
        private readonly GameManager _gameManager;
        private readonly ReplayManager _replayManager;
        private readonly SaveMigrator _migrator;
        private readonly MatchHistoryStore _historyStore;
        private readonly string _saveDirectory;
        private readonly string _initialStorageError;

        #endregion

        #region Session metadata (set by the room layer)

        /// <summary>Room name for the running session (written into saves).</summary>
        public string RoomName { get; set; }

        /// <summary>Room code for the running session (used to re-host after migration).</summary>
        public string RoomCode { get; set; }

        /// <summary>Max players of the running session.</summary>
        public int MaxPlayers { get; set; }

        /// <summary>Game port of the running session (so a migrated host re-hosts on the same port).</summary>
        public int Port { get; set; }

        #endregion

        #region Construction

        public SaveManager(IEventBus eventBus, GameManager gameManager, ReplayManager replayManager)
        {
            _eventBus = eventBus;
            _gameManager = gameManager;
            _replayManager = replayManager;
            _migrator = new SaveMigrator();
            _historyStore = new MatchHistoryStore();
            // Register future ISaveMigrationStep implementations here as the schema evolves.

            if (!ProfileStorage.TryGetDirectory("Saves", out _saveDirectory, out _initialStorageError))
                _saveDirectory = Path.Combine(PortableSaveStorage.Root, "Saves");

            _eventBus.Subscribe<TurnResolvedEvent>(OnTurnResolved);
            _eventBus.Subscribe<MatchFinishedEvent>(OnMatchFinished);
        }

        #endregion

        #region Autosave triggers

        private void OnTurnResolved(TurnResolvedEvent evt) => SaveNow();

        private void OnMatchFinished(MatchFinishedEvent evt)
        {
            SaveNow();
            ArchiveHistoryIfFinished();
        }

        private void ArchiveHistoryIfFinished()
        {
            if (_gameManager.Result == null || _gameManager.Config == null)
                return;

            var file = new SaveFile
            {
                GameVersion = Application.version,
                TimestampUtcTicks = DateTime.UtcNow.Ticks,
                RoomName = RoomName,
                RoomCode = RoomCode,
                MaxPlayers = MaxPlayers,
                Port = Port,
                State = _gameManager.CaptureSnapshot(),
                Replay = _replayManager.Log
            };

            if (!_historyStore.TryArchiveFinishedMatch(
                    file,
                    LocalIdentity.PersistentGuid,
                    LocalIdentity.DisplayName,
                    out _,
                    out string error))
                Debug.LogWarning($"[History] Archive failed: {error}");
        }

        #endregion

        #region Saving

        /// <summary>
        /// Writes the current match to disk immediately. Also called by the
        /// reconnection pipeline the moment the connection is lost (backup).
        /// </summary>
        public void SaveNow()
            => TrySaveNow(out _);

        /// <summary>Writes a save and returns the storage error to callers that need UI feedback.</summary>
        public bool TrySaveNow(out string error)
        {
            error = null;
            if (_gameManager.Config == null)
            {
                error = "No match is active.";
                return false;
            }

            if (!TryEnsureSaveDirectory(out string storageError))
            {
                Debug.LogError($"[Save] Autosave unavailable: {storageError}");
                error = storageError;
                return false;
            }

            var file = new SaveFile
            {
                GameVersion = Application.version,
                TimestampUtcTicks = DateTime.UtcNow.Ticks,
                RoomName = RoomName,
                RoomCode = RoomCode,
                MaxPlayers = MaxPlayers,
                Port = Port,
                State = _gameManager.CaptureSnapshot(),
                Replay = _replayManager.Log
            };

            string path = PathForRoom(RoomCode);

            try
            {
                WriteAtomic(path, JsonUtility.ToJson(file));
                _eventBus.Publish(new SaveCompletedEvent { FilePath = path, TurnNumber = file.State.TurnNumber });
                return true;
            }
            catch (IOException ex)
            {
                Debug.LogError($"[Save] Autosave failed: {ex.Message}");
                error = ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.LogError($"[Save] Autosave was denied: {ex.Message}");
                error = ex.Message;
                return false;
            }
        }

        private static void WriteAtomic(string path, string contents)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, contents);

            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }

        #endregion

        #region Loading

        /// <summary>Loads and migrates a specific save file.</summary>
        public bool TryLoad(string path, out SaveFile file, out string error)
        {
            file = null;
            error = null;

            if (!File.Exists(path))
            {
                error = "Save file not found.";
                return false;
            }

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (IOException ex)
            {
                error = ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                error = ex.Message;
                return false;
            }

            if (!_migrator.TryMigrate(json, out string migrated, out error))
                return false;

            try
            {
                file = JsonUtility.FromJson<SaveFile>(migrated);
            }
            catch (ArgumentException ex)
            {
                error = $"Save file is corrupt: {ex.Message}";
                return false;
            }

            string snapshotError = null;
            if (file?.State == null || !file.State.TryValidate(out snapshotError))
            {
                error = $"Save file is corrupt: {snapshotError ?? "snapshot is missing"}.";
                file = null;
                return false;
            }

            return true;
        }

        /// <summary>Loads the save for a specific room code (reconnect / migration path).</summary>
        public bool TryLoadForRoom(string roomCode, out SaveFile file, out string error) =>
            TryLoad(PathForRoom(roomCode), out file, out error);

        /// <summary>Checks whether an autosave exists without parsing or changing it.</summary>

        /// <summary>Loads the most recently written save on this machine.</summary>
        public bool TryLoadLatest(out SaveFile file, out string error)
        {
            file = null;
            error = "No saves found.";

            if (!TryEnsureSaveDirectory(out error))
                return false;

            try
            {
                string bestPath = null;
                DateTime bestTime = DateTime.MinValue;
                foreach (string path in Directory.GetFiles(_saveDirectory, "match_*.json"))
                {
                    DateTime writeTime = File.GetLastWriteTimeUtc(path);
                    if (writeTime > bestTime)
                    {
                        bestTime = writeTime;
                        bestPath = path;
                    }
                }

                return bestPath != null && TryLoad(bestPath, out file, out error);
            }
            catch (IOException ex)
            {
                error = ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>Exposes archived match history for the history browser UI.</summary>
        public MatchHistoryStore History => _historyStore;

        private string PathForRoom(string roomCode) =>
            Path.Combine(_saveDirectory, $"match_{(string.IsNullOrEmpty(roomCode) ? "local" : roomCode)}.json");

        private bool TryEnsureSaveDirectory(out string error)
        {
            error = _initialStorageError;
            if (!string.IsNullOrEmpty(_initialStorageError))
                return false;

            try
            {
                Directory.CreateDirectory(_saveDirectory);
                error = null;
                return true;
            }
            catch (Exception ex) when (ex is IOException
                                       || ex is UnauthorizedAccessException
                                       || ex is NotSupportedException
                                       || ex is ArgumentException)
            {
                error = ex.Message;
                return false;
            }
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            _eventBus.Unsubscribe<TurnResolvedEvent>(OnTurnResolved);
            _eventBus.Unsubscribe<MatchFinishedEvent>(OnMatchFinished);
        }

        #endregion
    }
}
