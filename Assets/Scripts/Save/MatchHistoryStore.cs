using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AMath.Core;
using AMath.Core.History;
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
        {
            _historyDirectory = Path.Combine(Application.persistentDataPath, "History");
            _indexPath = Path.Combine(_historyDirectory, "history_index.json");
            Directory.CreateDirectory(_historyDirectory);
        }

        public IReadOnlyList<MatchHistoryEntry> ListEntries()
        {
            MatchHistoryIndex index = LoadIndex();
            index.Entries.Sort((a, b) => b.FinishedUtcTicks.CompareTo(a.FinishedUtcTicks));
            return index.Entries;
        }

        public bool TryArchiveFinishedMatch(SaveFile file, string accountUsername, out MatchHistoryEntry entry, out string error)
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
            PlayerResult winner = result.Standings.Find(r => r.PlayerId == result.WinnerPlayerId);
            entry = new MatchHistoryEntry
            {
                MatchId = matchId,
                FinishedUtcTicks = result.EndedUtcTicks > 0 ? result.EndedUtcTicks : DateTime.UtcNow.Ticks,
                RoomName = file.RoomName,
                RoomCode = file.RoomCode,
                Format = result.Format,
                DurationSeconds = result.DurationSeconds,
                TurnCount = file.Replay.Events?.Count ?? 0,
                WinnerLabel = BuildWinnerLabel(result, winner),
                WinnerScore = winner?.FinalScore ?? 0,
                AccountUsername = accountUsername,
                ReplayFileName = replayFileName,
                StandingsSummary = BuildStandingsSummary(result)
            };

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

        private static string BuildWinnerLabel(MatchResult result, PlayerResult winner)
        {
            if (result.Format == MatchFormat.Team && result.WinnerTeamId >= 0)
                return $"Team {result.WinnerTeamId + 1}";

            return winner?.DisplayName ?? $"Player #{result.WinnerPlayerId}";
        }

        private static string BuildStandingsSummary(MatchResult result)
        {
            var builder = new StringBuilder();
            if (result.Format == MatchFormat.Team && result.TeamStandings.Count > 0)
            {
                foreach (TeamResult team in result.TeamStandings.OrderByDescending(t => t.TotalScore))
                    builder.AppendLine($"Team {team.TeamId + 1}: {team.TotalScore}");
            }

            foreach (PlayerResult row in result.Standings.OrderByDescending(r => r.FinalScore))
            {
                string teamSuffix = row.TeamId >= 0 ? $" (T{row.TeamId + 1})" : string.Empty;
                builder.AppendLine($"{row.DisplayName}{teamSuffix}: {row.FinalScore}");
            }

            return builder.ToString().TrimEnd();
        }
    }
}
