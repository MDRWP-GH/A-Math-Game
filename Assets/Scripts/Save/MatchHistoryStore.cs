using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AMath.Core;
using AMath.Accounts;
using AMath.Core.History;
using AMath.Core.Snapshot;
using AMath.Utilities;
using UnityEngine;

namespace AMath.Save
{
    public enum MatchHistoryReadStatus
    {
        Ok,
        RecoveredFromBackup,
        RebuiltFromReplays,
        StorageUnavailable
    }

    /// <summary>
    /// Archives finished matches into a browsable history folder. Each entry
    /// keeps a full save/replay snapshot separate from the live recovery file.
    /// </summary>
    public sealed class MatchHistoryStore
    {
        public const int MaxEntries = 50;

        private readonly string _historyDirectory;
        private readonly string _indexPath;
        private readonly string _backupIndexPath;
        private readonly string _initialStorageError;
        private readonly SaveMigrator _migrator = new();

        public MatchHistoryStore()
        {
            if (!ProfileStorage.TryGetDirectory("History", out _historyDirectory, out _initialStorageError))
                _historyDirectory = Path.Combine(PortableSaveStorage.Root, "History");

            _indexPath = Path.Combine(_historyDirectory, "history_index.json");
            _backupIndexPath = _indexPath + ".bak";
        }

        public MatchHistoryStore(string historyDirectory)
        {
            _historyDirectory = historyDirectory ?? string.Empty;
            _indexPath = Path.Combine(_historyDirectory, "history_index.json");
            _backupIndexPath = _indexPath + ".bak";
        }

        public IReadOnlyList<MatchHistoryEntry> ListEntries()
        {
            TryListEntries(out IReadOnlyList<MatchHistoryEntry> entries, out _, out _);
            return entries;
        }

        public bool TryListEntries(
            out IReadOnlyList<MatchHistoryEntry> entries,
            out MatchHistoryReadStatus status,
            out string error)
        {
            entries = Array.Empty<MatchHistoryEntry>();
            if (!TryLoadIndexWithRecovery(out MatchHistoryIndex index, out status, out error))
                return false;

            index.Entries ??= new List<MatchHistoryEntry>();
            index.Entries.RemoveAll(item => item == null);
            index.Entries.Sort((a, b) => b.FinishedUtcTicks.CompareTo(a.FinishedUtcTicks));
            entries = index.Entries;
            return true;
        }

        public bool TryArchiveFinishedMatch(
            SaveFile file,
            string localPersistentGuid,
            string localDisplayName,
            out MatchHistoryEntry entry,
            out string error)
        {
            entry = null;
            error = null;

            if (file?.State?.Result == null || file.Replay == null)
            {
                error = "Match is not finished.";
                return false;
            }

            if (!TryEnsureDirectory(out error))
                return false;

            string matchId = Guid.NewGuid().ToString("N");
            string replayFileName = $"match_{matchId}.json";
            string replayPath = Path.Combine(_historyDirectory, replayFileName);

            try
            {
                WriteAtomic(replayPath, JsonUtility.ToJson(file));
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            entry = BuildEntry(file, matchId, replayFileName, localPersistentGuid, localDisplayName);

            if (!TryLoadIndexWithRecovery(out MatchHistoryIndex index, out _, out error))
            {
                TryDeleteReplay(replayFileName);
                entry = null;
                return false;
            }
            index.Entries ??= new List<MatchHistoryEntry>();
            // Recovery can discover the replay just written above. Replace that
            // provisional row instead of showing the same match twice.
            index.Entries.RemoveAll(item => item != null && item.MatchId == matchId);
            // File.Replace rotates the current index into .bak. Keep replay
            // files referenced by either copy until that backup rotates again.
            var previousPrimaryFiles = new HashSet<string>(
                index.Entries.Where(item => item != null).Select(item => item.ReplayFileName));
            HashSet<string> previousBackupFiles = null;
            if (File.Exists(_backupIndexPath)
                && TryReadIndex(_backupIndexPath, out MatchHistoryIndex previousBackup, out _))
            {
                previousBackupFiles = new HashSet<string>(
                    previousBackup.Entries.Select(item => item.ReplayFileName));
            }
            index.Entries.Insert(0, entry);
            while (index.Entries.Count > MaxEntries)
            {
                index.Entries.RemoveAt(index.Entries.Count - 1);
            }

            try
            {
                WriteAtomic(_indexPath, JsonUtility.ToJson(index), _backupIndexPath);
            }
            catch (Exception ex)
            {
                // Without an index entry the replay is unreachable, so it is
                // dropped rather than left behind as an orphan file.
                TryDeleteReplay(replayFileName);
                entry = null;
                error = ex.Message;
                return false;
            }

            if (previousBackupFiles != null)
            {
                var retainedFiles = new HashSet<string>(previousPrimaryFiles);
                foreach (MatchHistoryEntry retained in index.Entries)
                    retainedFiles.Add(retained.ReplayFileName);
                foreach (string oldReplay in previousBackupFiles)
                {
                    if (!retainedFiles.Contains(oldReplay))
                        TryDeleteReplay(oldReplay);
                }
            }

            return true;
        }

        public bool TryLoadReplay(string matchId, out SaveFile file, out string error)
        {
            file = null;
            error = "Replay not found.";

            if (!TryListEntries(out IReadOnlyList<MatchHistoryEntry> entries, out _, out error))
                return false;

            MatchHistoryEntry entry = entries.FirstOrDefault(e => e.MatchId == matchId);
            if (entry == null)
                return false;

            if (!TryResolveReplayPath(entry.ReplayFileName, out string path))
            {
                error = "Replay path is invalid.";
                return false;
            }

            if (!File.Exists(path))
                return false;

            try
            {
                string json = File.ReadAllText(path);
                if (!_migrator.TryMigrate(json, out string migratedJson, out string migrateError))
                {
                    error = migrateError;
                    return false;
                }

                file = JsonUtility.FromJson<SaveFile>(migratedJson);
                if (file?.Replay == null || file.Replay.Events == null)
                {
                    error = "Replay file is corrupt.";
                    file = null;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool TryLoadIndexWithRecovery(
            out MatchHistoryIndex index,
            out MatchHistoryReadStatus status,
            out string error)
        {
            index = new MatchHistoryIndex();
            status = MatchHistoryReadStatus.Ok;
            error = null;

            if (!TryEnsureDirectory(out error))
            {
                status = MatchHistoryReadStatus.StorageUnavailable;
                return false;
            }

            bool primaryExists = File.Exists(_indexPath);
            if (primaryExists && TryReadIndex(_indexPath, out index, out _))
                return true;

            if (!primaryExists && !File.Exists(_backupIndexPath))
            {
                // A removed index is recoverable too: replay archives are the
                // source of truth and must not silently disappear from History.
                if (!TryRebuildIndex(out index, out int missingSkipped, out error))
                {
                    status = MatchHistoryReadStatus.StorageUnavailable;
                    return false;
                }

                if (index.Entries.Count > 0 || missingSkipped > 0)
                {
                    status = MatchHistoryReadStatus.RebuiltFromReplays;
                    if (missingSkipped > 0)
                        error = $"Recovered history; skipped {missingSkipped} unreadable replay file(s).";
                    TryWriteRecoveredIndex(index);
                }
                return true;
            }

            string primaryError = null;
            if (primaryExists)
            {
                TryReadIndex(_indexPath, out _, out primaryError);
                PreserveCorruptIndex();
            }

            if (File.Exists(_backupIndexPath)
                && TryReadIndex(_backupIndexPath, out index, out string backupError))
            {
                status = MatchHistoryReadStatus.RecoveredFromBackup;
                error = primaryError;
                // The backup is one successful archive behind the primary.
                // Replays written after it must still appear in History.
                if (TryRebuildIndex(out MatchHistoryIndex replays, out int backupSkipped, out _))
                {
                    var knownIds = new HashSet<string>(
                        index.Entries.Select(item => item.MatchId), StringComparer.OrdinalIgnoreCase);
                    foreach (MatchHistoryEntry replay in replays.Entries)
                    {
                        if (knownIds.Add(replay.MatchId))
                            index.Entries.Add(replay);
                    }
                    index.Entries.Sort((a, b) => b.FinishedUtcTicks.CompareTo(a.FinishedUtcTicks));
                    if (index.Entries.Count > MaxEntries)
                        index.Entries.RemoveRange(MaxEntries, index.Entries.Count - MaxEntries);
                    if (backupSkipped > 0)
                        error = $"Recovered history; skipped {backupSkipped} unreadable replay file(s).";
                }
                TryWriteRecoveredIndex(index);
                return true;
            }

            if (TryRebuildIndex(out index, out int skipped, out string rebuildError))
            {
                status = MatchHistoryReadStatus.RebuiltFromReplays;
                error = skipped > 0
                    ? $"Recovered history; skipped {skipped} unreadable replay file(s)."
                    : primaryError ?? rebuildError;
                TryWriteRecoveredIndex(index);
                return true;
            }

            status = MatchHistoryReadStatus.StorageUnavailable;
            error = rebuildError ?? primaryError ?? "Match history could not be recovered.";
            return false;
        }

        private static bool TryReadIndex(string path, out MatchHistoryIndex index, out string error)
        {
            index = null;
            error = null;
            try
            {
                index = JsonUtility.FromJson<MatchHistoryIndex>(File.ReadAllText(path));
                if (index == null)
                {
                    error = "History index is empty or malformed.";
                    return false;
                }

                index.Entries ??= new List<MatchHistoryEntry>();
                index.Entries.RemoveAll(item => item == null);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool TryRebuildIndex(out MatchHistoryIndex index, out int skipped, out string error)
        {
            index = new MatchHistoryIndex();
            skipped = 0;
            error = null;

            try
            {
                foreach (string replayPath in Directory.GetFiles(_historyDirectory, "match_*.json"))
                {
                    string replayFileName = Path.GetFileName(replayPath);
                    try
                    {
                        string json = File.ReadAllText(replayPath);
                        if (!_migrator.TryMigrate(json, out string migratedJson, out _))
                        {
                            skipped++;
                            continue;
                        }

                        SaveFile file = JsonUtility.FromJson<SaveFile>(migratedJson);
                        if (file?.State?.Result == null || file.Replay?.Events == null)
                        {
                            skipped++;
                            continue;
                        }

                        string matchId = Path.GetFileNameWithoutExtension(replayFileName)
                            .Substring("match_".Length);
                        index.Entries.Add(BuildEntry(
                            file,
                            matchId,
                            replayFileName,
                            LocalIdentity.PersistentGuid,
                            LocalIdentity.DisplayName));
                    }
                    catch
                    {
                        skipped++;
                    }
                }

                index.Entries.Sort((a, b) => b.FinishedUtcTicks.CompareTo(a.FinishedUtcTicks));
                if (index.Entries.Count > MaxEntries)
                    index.Entries.RemoveRange(MaxEntries, index.Entries.Count - MaxEntries);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private void PreserveCorruptIndex()
        {
            try
            {
                string corruptPath = _indexPath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                File.Copy(_indexPath, corruptPath, overwrite: false);
            }
            catch
            {
                // Recovery must still proceed when the diagnostic copy cannot be written.
            }
        }

        private void TryWriteRecoveredIndex(MatchHistoryIndex index)
        {
            try
            {
                // Do not rotate a corrupt primary over the known-good backup.
                WriteAtomic(_indexPath, JsonUtility.ToJson(index));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[History] Recovered entries in memory but could not repair the index: {ex.Message}");
            }
        }

        private bool TryEnsureDirectory(out string error)
        {
            error = _initialStorageError;
            if (!string.IsNullOrEmpty(_initialStorageError))
                return false;

            try
            {
                Directory.CreateDirectory(_historyDirectory);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private void TryDeleteReplay(string replayFileName)
        {
            if (!TryResolveReplayPath(replayFileName, out string path))
                return;

            if (File.Exists(path))
            {
                try { File.Delete(path); }
                catch { /* best effort */ }
            }
        }

        private bool TryResolveReplayPath(string replayFileName, out string path)
        {
            path = null;
            if (string.IsNullOrWhiteSpace(replayFileName))
                return false;

            // Reject path segments so a tampered index cannot escape the history
            // folder and read arbitrary files the game process can open.
            if (replayFileName.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0)
                return false;

            try
            {
                string candidate = Path.GetFullPath(Path.Combine(_historyDirectory, replayFileName));
                string root = Path.GetFullPath(_historyDirectory);
                if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase))
                    return false;

                path = candidate;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void WriteAtomic(string path, string contents, string backupPath = null)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, contents);

            if (File.Exists(path))
                File.Replace(temp, path, backupPath);
            else
                File.Move(temp, path);
        }

        private static MatchHistoryEntry BuildEntry(
            SaveFile file,
            string matchId,
            string replayFileName,
            string localPersistentGuid,
            string localDisplayName)
        {
            MatchResult result = file.State.Result;
            PlayerResult winner = result.Standings?.Find(r => r.PlayerId == result.WinnerPlayerId);
            PlayerResult local = FindLocalPlayer(file, localPersistentGuid, localDisplayName);
            var entry = new MatchHistoryEntry
            {
                MatchId = matchId,
                StartedUtcTicks = result.StartedUtcTicks,
                FinishedUtcTicks = result.EndedUtcTicks > 0 ? result.EndedUtcTicks : DateTime.UtcNow.Ticks,
                RoomName = file.RoomName,
                RoomCode = file.RoomCode,
                Format = result.Format,
                DurationSeconds = Math.Max(0, result.DurationSeconds),
                TurnCount = file.Replay.Events?.Count ?? 0,
                WinnerLabel = BuildWinnerLabel(result, winner),
                WinnerScore = result.IsDraw && result.Standings != null && result.Standings.Count > 0
                    ? result.Standings.Max(r => r.FinalScore)
                    : winner?.FinalScore ?? 0,
                ReplayFileName = replayFileName,
                LocalPlayerName = local?.DisplayName ?? localDisplayName,
                LocalPlayerScore = local?.FinalScore ?? 0,
                HasLocalPlayer = local != null,
                DidWin = local != null && DidLocalWin(result, local),
                IsDraw = result.IsDraw
            };

            // Copied into the index so the browser can list final standings
            // without loading (and parsing) every archived replay file.
            if (result.Standings != null)
                entry.Players.AddRange(result.Standings);
            if (result.TeamStandings != null)
                entry.Teams.AddRange(result.TeamStandings);
            return entry;
        }

        private static PlayerResult FindLocalPlayer(SaveFile file, string persistentGuid, string displayName)
        {
            List<PlayerResult> standings = file.State?.Result?.Standings;
            if (standings == null || standings.Count == 0)
                return null;

            int playerId = FindLocalPlayerId(file, persistentGuid);
            if (playerId >= 0)
            {
                foreach (PlayerResult row in standings)
                {
                    if (row.PlayerId == playerId)
                        return row;
                }
            }

            if (string.IsNullOrEmpty(displayName))
                return null;

            foreach (PlayerResult row in standings)
            {
                if (string.Equals(row.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
                    return row;
            }

            return null;
        }

        private static int FindLocalPlayerId(SaveFile file, string persistentGuid)
        {
            if (string.IsNullOrEmpty(persistentGuid))
                return -1;

            if (file.State?.Players != null)
            {
                foreach (PlayerSnapshot player in file.State.Players)
                {
                    if (player.PersistentGuid == persistentGuid)
                        return player.PlayerId;
                }
            }

            if (file.State?.Config?.Players != null)
            {
                foreach (PlayerIdentity player in file.State.Config.Players)
                {
                    if (player.PersistentGuid == persistentGuid)
                        return player.PlayerId;
                }
            }

            return -1;
        }

        private static bool DidLocalWin(MatchResult result, PlayerResult local)
        {
            if (result.IsDraw)
                return false;

            if (result.Format == MatchFormat.Team)
                return local.TeamId >= 0 && local.TeamId == result.WinnerTeamId;

            return local.PlayerId == result.WinnerPlayerId;
        }

        private static string BuildWinnerLabel(MatchResult result, PlayerResult winner)
        {
            if (result.IsDraw)
                return string.Empty;

            if (result.Format == MatchFormat.Team && result.WinnerTeamId >= 0)
                return $"Team {result.WinnerTeamId + 1}";

            return winner?.DisplayName ?? $"Player #{result.WinnerPlayerId}";
        }
    }
}
