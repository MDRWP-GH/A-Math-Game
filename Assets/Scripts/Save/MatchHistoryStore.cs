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
                File.WriteAllText(replayPath, JsonUtility.ToJson(file));
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
            index.Entries.Insert(0, entry);
            while (index.Entries.Count > MaxEntries)
            {
                MatchHistoryEntry removed = index.Entries[^1];
                index.Entries.RemoveAt(index.Entries.Count - 1);
                TryDeleteReplay(removed.ReplayFileName);
            }

            try
            {
                File.WriteAllText(_indexPath, JsonUtility.ToJson(index));
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

            string path = Path.Combine(_historyDirectory, entry.ReplayFileName);
            if (!File.Exists(path))
                return false;

            try
            {
                file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(path));
                if (file?.Replay == null)
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
                return JsonUtility.FromJson<MatchHistoryIndex>(File.ReadAllText(_indexPath))
                       ?? new MatchHistoryIndex();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[History] Could not read index: {ex.Message}");
                return new MatchHistoryIndex();
            }
        }

        private void TryDeleteReplay(string replayFileName)
        {
            if (string.IsNullOrEmpty(replayFileName))
                return;

            string path = Path.Combine(_historyDirectory, replayFileName);
            if (File.Exists(path))
            {
                try { File.Delete(path); }
                catch { /* best effort */ }
            }
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
