using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AMath.Core;
using AMath.Core.History;
using AMath.Core.Snapshot;
using UnityEngine;

namespace AMath.Save
{
    /// <summary>
    /// Archives finished matches into a browsable history folder. Each entry
    /// keeps a full save/replay snapshot separate from the live recovery file.
    /// </summary>
    public sealed class MatchHistoryStore
    {
        public const int MaxEntries = 50;

        private readonly string _historyDirectory;
        private readonly string _indexPath;
        private readonly SaveMigrator _migrator = new();

        public MatchHistoryStore()
            : this(Path.Combine(Application.persistentDataPath, "History"))
        {
        }

        public MatchHistoryStore(string historyDirectory)
        {
            _historyDirectory = historyDirectory;
            _indexPath = Path.Combine(_historyDirectory, "history_index.json");
            Directory.CreateDirectory(_historyDirectory);
        }

        public IReadOnlyList<MatchHistoryEntry> ListEntries()
        {
            MatchHistoryIndex index = LoadIndex();
            index.Entries ??= new List<MatchHistoryEntry>();
            index.Entries.Sort((a, b) => b.FinishedUtcTicks.CompareTo(a.FinishedUtcTicks));
            return index.Entries;
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

            MatchResult result = file.State.Result;
            PlayerResult winner = result.Standings?.Find(r => r.PlayerId == result.WinnerPlayerId);
            PlayerResult local = FindLocalPlayer(file, localPersistentGuid, localDisplayName);
            entry = new MatchHistoryEntry
            {
                MatchId = matchId,
                StartedUtcTicks = result.StartedUtcTicks,
                FinishedUtcTicks = result.EndedUtcTicks > 0 ? result.EndedUtcTicks : DateTime.UtcNow.Ticks,
                RoomName = file.RoomName,
                RoomCode = file.RoomCode,
                Format = result.Format,
                DurationSeconds = result.DurationSeconds,
                TurnCount = file.Replay.Events?.Count ?? 0,
                WinnerLabel = BuildWinnerLabel(result, winner),
                WinnerScore = winner?.FinalScore ?? 0,
                ReplayFileName = replayFileName,
                LocalPlayerName = local?.DisplayName ?? localDisplayName,
                LocalPlayerScore = local?.FinalScore ?? 0,
                HasLocalPlayer = local != null,
                DidWin = local != null && DidLocalWin(result, local)
            };

            // Copied into the index so the browser can list final standings
            // without loading (and parsing) every archived replay file.
            if (result.Standings != null)
                entry.Players.AddRange(result.Standings);
            if (result.TeamStandings != null)
                entry.Teams.AddRange(result.TeamStandings);

            MatchHistoryIndex index = LoadIndex();
            index.Entries ??= new List<MatchHistoryEntry>();
            index.Entries.Insert(0, entry);
            while (index.Entries.Count > MaxEntries)
            {
                MatchHistoryEntry removed = index.Entries[^1];
                index.Entries.RemoveAt(index.Entries.Count - 1);
                TryDeleteReplay(removed.ReplayFileName);
            }

            try
            {
                WriteAtomic(_indexPath, JsonUtility.ToJson(index));
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

            return true;
        }

        public bool TryLoadReplay(string matchId, out SaveFile file, out string error)
        {
            file = null;
            error = "Replay not found.";

            MatchHistoryEntry entry = ListEntries().FirstOrDefault(e => e.MatchId == matchId);
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

        private MatchHistoryIndex LoadIndex()
        {
            if (!File.Exists(_indexPath))
                return new MatchHistoryIndex();

            try
            {
                MatchHistoryIndex index = JsonUtility.FromJson<MatchHistoryIndex>(File.ReadAllText(_indexPath))
                       ?? new MatchHistoryIndex();
                index.Entries ??= new List<MatchHistoryEntry>();
                return index;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[History] Could not read index: {ex.Message}");
                return new MatchHistoryIndex();
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

            string candidate = Path.GetFullPath(Path.Combine(_historyDirectory, replayFileName));
            string root = Path.GetFullPath(_historyDirectory);
            if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            path = candidate;
            return true;
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
            if (result.Format == MatchFormat.Team)
                return local.TeamId >= 0 && local.TeamId == result.WinnerTeamId;

            return local.PlayerId == result.WinnerPlayerId;
        }

        private static string BuildWinnerLabel(MatchResult result, PlayerResult winner)
        {
            if (result.Format == MatchFormat.Team && result.WinnerTeamId >= 0)
                return $"Team {result.WinnerTeamId + 1}";

            return winner?.DisplayName ?? $"Player #{result.WinnerPlayerId}";
        }
    }
}
