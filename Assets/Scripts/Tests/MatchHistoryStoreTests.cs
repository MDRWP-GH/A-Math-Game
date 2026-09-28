using System;
using System.IO;
using System.Linq;
using AMath.Core;
using AMath.Core.History;
using AMath.Core.Snapshot;
using AMath.Replay;
using AMath.Save;
using NUnit.Framework;
using UnityEngine;

namespace AMath.Tests
{
    public sealed class MatchHistoryStoreTests
    {
        private string _directory;
        private MatchHistoryStore _store;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "amath-history-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _store = new MatchHistoryStore(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        [Test]
        public void TryArchiveFinishedMatch_StoresLocalPlayerScoreAndWin()
        {
            long started = new DateTime(2026, 8, 26, 5, 30, 0, DateTimeKind.Utc).Ticks;
            long ended = started + TimeSpan.FromMinutes(12).Ticks + TimeSpan.FromSeconds(4).Ticks;
            SaveFile file = CreateFinishedSave(
                MatchFormat.Individual,
                started,
                ended,
                724,
                winnerPlayerId: 0,
                new PlayerResult { PlayerId = 0, DisplayName = "Ann", FinalScore = 88, TeamId = -1 },
                new PlayerResult { PlayerId = 1, DisplayName = "Ben", FinalScore = 41, TeamId = -1 });
            file.State.Players.Add(new PlayerSnapshot { PlayerId = 0, PersistentGuid = "guid-ann", DisplayName = "Ann", Score = 88 });
            file.State.Players.Add(new PlayerSnapshot { PlayerId = 1, PersistentGuid = "guid-ben", DisplayName = "Ben", Score = 41 });

            Assert.IsTrue(
                _store.TryArchiveFinishedMatch(file, "guid-ann", "Ann", out MatchHistoryEntry entry, out string error),
                error);
            Assert.AreEqual(MatchFormat.Individual, entry.Format);
            Assert.AreEqual(724, entry.DurationSeconds);
            Assert.AreEqual(started, entry.StartedUtcTicks);
            Assert.AreEqual(ended, entry.FinishedUtcTicks);
            Assert.IsTrue(entry.HasLocalPlayer);
            Assert.IsTrue(entry.DidWin);
            Assert.AreEqual("Ann", entry.LocalPlayerName);
            Assert.AreEqual(88, entry.LocalPlayerScore);

            var listed = _store.ListEntries();
            Assert.AreEqual(1, listed.Count);
            Assert.AreEqual("Ann", listed[0].LocalPlayerName);
            Assert.AreEqual(88, listed[0].LocalPlayerScore);
            Assert.IsTrue(listed[0].DidWin);
        }

        [Test]
        public void TryArchiveFinishedMatch_StoresLocalLoss()
        {
            SaveFile file = CreateFinishedSave(
                MatchFormat.Individual,
                DateTime.UtcNow.Ticks,
                DateTime.UtcNow.Ticks,
                30,
                winnerPlayerId: 0,
                new PlayerResult { PlayerId = 0, DisplayName = "Ann", FinalScore = 88, TeamId = -1 },
                new PlayerResult { PlayerId = 1, DisplayName = "Ben", FinalScore = 41, TeamId = -1 });
            file.State.Players.Add(new PlayerSnapshot { PlayerId = 1, PersistentGuid = "guid-ben", DisplayName = "Ben" });

            Assert.IsTrue(
                _store.TryArchiveFinishedMatch(file, "guid-ben", "Ben", out MatchHistoryEntry entry, out string error),
                error);
            Assert.IsTrue(entry.HasLocalPlayer);
            Assert.IsFalse(entry.DidWin);
            Assert.AreEqual("Ben", entry.LocalPlayerName);
            Assert.AreEqual(41, entry.LocalPlayerScore);
        }

        [Test]
        public void TryArchiveFinishedMatch_TeamWinUsesLocalTeam()
        {
            long started = DateTime.UtcNow.Ticks;
            SaveFile file = CreateFinishedSave(
                MatchFormat.Team,
                started,
                started + TimeSpan.TicksPerMinute,
                60,
                winnerPlayerId: 0,
                new PlayerResult { PlayerId = 0, DisplayName = "A", FinalScore = 30, TeamId = 0 },
                new PlayerResult { PlayerId = 1, DisplayName = "B", FinalScore = 12, TeamId = 1 });
            file.State.Result.WinnerTeamId = 0;
            file.State.Players.Add(new PlayerSnapshot { PlayerId = 0, PersistentGuid = "guid-a", DisplayName = "A" });

            Assert.IsTrue(
                _store.TryArchiveFinishedMatch(file, "guid-a", "A", out MatchHistoryEntry entry, out string error),
                error);
            Assert.AreEqual(MatchFormat.Team, entry.Format);
            Assert.IsTrue(entry.HasLocalPlayer);
            Assert.IsTrue(entry.DidWin);
            Assert.AreEqual("A", entry.LocalPlayerName);
            Assert.AreEqual(30, entry.LocalPlayerScore);
        }

        [Test]
        public void ListEntries_SurvivesIndexWithNullEntries()
        {
            File.WriteAllText(
                Path.Combine(_directory, "history_index.json"),
                "{\"Version\":1}");

            Assert.AreEqual(0, _store.ListEntries().Count);
        }

        [Test]
        public void TryLoadReplay_RejectsPathTraversalFileName()
        {
            SaveFile file = CreateFinishedSave(
                MatchFormat.Individual,
                DateTime.UtcNow.Ticks,
                DateTime.UtcNow.Ticks,
                30,
                winnerPlayerId: 0,
                new PlayerResult { PlayerId = 0, DisplayName = "Ann", FinalScore = 88, TeamId = -1 });
            file.State.Players.Add(new PlayerSnapshot { PlayerId = 0, PersistentGuid = "guid-ann", DisplayName = "Ann" });

            Assert.IsTrue(
                _store.TryArchiveFinishedMatch(file, "guid-ann", "Ann", out MatchHistoryEntry entry, out string error),
                error);

            string indexPath = Path.Combine(_directory, "history_index.json");
            string indexJson = File.ReadAllText(indexPath);
            indexJson = indexJson.Replace(entry.ReplayFileName, "..\\\\outside.json");
            File.WriteAllText(indexPath, indexJson);

            Assert.IsFalse(_store.TryLoadReplay(entry.MatchId, out _, out error));
            Assert.AreEqual("Replay path is invalid.", error);
        }

        [Test]
        public void TryArchiveFinishedMatch_DrawIsNeitherWinNorLoss()
        {
            SaveFile file = CreateFinishedSave(
                MatchFormat.Individual,
                DateTime.UtcNow.Ticks,
                DateTime.UtcNow.Ticks,
                15,
                winnerPlayerId: -1,
                new PlayerResult { PlayerId = 0, DisplayName = "Ann", FinalScore = 50, TeamId = -1 },
                new PlayerResult { PlayerId = 1, DisplayName = "Ben", FinalScore = 50, TeamId = -1 });
            file.State.Result.IsDraw = true;
            file.State.Players.Add(new PlayerSnapshot { PlayerId = 0, PersistentGuid = "guid-ann", DisplayName = "Ann" });

            Assert.IsTrue(
                _store.TryArchiveFinishedMatch(file, "guid-ann", "Ann", out MatchHistoryEntry entry, out string error),
                error);
            Assert.IsTrue(entry.IsDraw);
            Assert.IsFalse(entry.DidWin);
            Assert.AreEqual(string.Empty, entry.WinnerLabel);
            Assert.AreEqual(50, entry.WinnerScore);
        }

        [Test]
        public void CorruptPrimaryIndex_RecoversFromBackup()
        {
            SaveFile first = CreateFinishedSave(
                MatchFormat.Individual, DateTime.UtcNow.Ticks, DateTime.UtcNow.Ticks, 1, 0,
                new PlayerResult { PlayerId = 0, DisplayName = "A", FinalScore = 1 });
            SaveFile second = CreateFinishedSave(
                MatchFormat.Individual, DateTime.UtcNow.Ticks, DateTime.UtcNow.Ticks, 2, 0,
                new PlayerResult { PlayerId = 0, DisplayName = "A", FinalScore = 2 });
            Assert.IsTrue(_store.TryArchiveFinishedMatch(first, null, "A", out _, out string error), error);
            Assert.IsTrue(_store.TryArchiveFinishedMatch(second, null, "A", out _, out error), error);
            File.WriteAllText(Path.Combine(_directory, "history_index.json"), "{ broken");

            Assert.IsTrue(_store.TryListEntries(out var entries, out MatchHistoryReadStatus status, out error), error);
            Assert.AreEqual(MatchHistoryReadStatus.RecoveredFromBackup, status);
            Assert.AreEqual(2, entries.Count, "A valid replay newer than the backup must remain visible.");
            Assert.IsTrue(Directory.GetFiles(_directory, "history_index.json.corrupt-*").Length > 0);
        }

        [Test]
        public void MissingIndex_RebuildsFromExistingReplay()
        {
            SaveFile file = CreateFinishedSave(
                MatchFormat.Individual, DateTime.UtcNow.Ticks, DateTime.UtcNow.Ticks, 1, 0,
                new PlayerResult { PlayerId = 0, DisplayName = "A", FinalScore = 1 });
            Assert.IsTrue(_store.TryArchiveFinishedMatch(file, null, "A", out _, out string error), error);
            File.Delete(Path.Combine(_directory, "history_index.json"));
            string backup = Path.Combine(_directory, "history_index.json.bak");
            if (File.Exists(backup)) File.Delete(backup);

            Assert.IsTrue(_store.TryListEntries(out var entries, out MatchHistoryReadStatus status, out error), error);
            Assert.AreEqual(MatchHistoryReadStatus.RebuiltFromReplays, status);
            Assert.AreEqual(1, entries.Count);
            Assert.IsTrue(File.Exists(Path.Combine(_directory, "history_index.json")));
        }

        [Test]
        public void ArchiveWhileRecoveringIndex_DoesNotDuplicateNewMatch()
        {
            SaveFile file = CreateFinishedSave(
                MatchFormat.Individual, DateTime.UtcNow.Ticks, DateTime.UtcNow.Ticks, 1, 0,
                new PlayerResult { PlayerId = 0, DisplayName = "A", FinalScore = 1 });
            Assert.IsTrue(_store.TryArchiveFinishedMatch(file, null, "A", out _, out string error), error);
            Assert.IsTrue(_store.TryArchiveFinishedMatch(file, null, "A", out _, out error), error);
            File.WriteAllText(Path.Combine(_directory, "history_index.json"), "{ broken");

            Assert.IsTrue(_store.TryArchiveFinishedMatch(file, null, "A", out MatchHistoryEntry archived, out error), error);
            var entries = _store.ListEntries();
            Assert.AreEqual(3, entries.Count);
            Assert.AreEqual(1, entries.Count(item => item.MatchId == archived.MatchId));
        }

        [Test]
        public void ArchiveAtCapacity_KeepsReplayReferencedByBackupUntilItRotates()
        {
            SaveFile oldSave = CreateFinishedSave(
                MatchFormat.Individual, DateTime.UtcNow.Ticks, DateTime.UtcNow.Ticks, 1, 0,
                new PlayerResult { PlayerId = 0, DisplayName = "A", FinalScore = 1 });
            string oldestReplay = "match_oldest.json";
            File.WriteAllText(Path.Combine(_directory, oldestReplay), JsonUtility.ToJson(oldSave));

            var index = new MatchHistoryIndex();
            for (int i = 0; i < MatchHistoryStore.MaxEntries - 1; i++)
                index.Entries.Add(new MatchHistoryEntry
                {
                    MatchId = "existing-" + i,
                    ReplayFileName = "match_existing-" + i + ".json"
                });
            index.Entries.Add(new MatchHistoryEntry { MatchId = "oldest", ReplayFileName = oldestReplay });
            File.WriteAllText(Path.Combine(_directory, "history_index.json"), JsonUtility.ToJson(index));

            Assert.IsTrue(_store.TryArchiveFinishedMatch(oldSave, null, "A", out _, out string error), error);
            Assert.IsTrue(File.Exists(Path.Combine(_directory, oldestReplay)),
                "The rotated backup still references this replay.");

            Assert.IsTrue(_store.TryArchiveFinishedMatch(oldSave, null, "A", out _, out error), error);
            Assert.IsFalse(File.Exists(Path.Combine(_directory, oldestReplay)),
                "The replay may be pruned once neither index references it.");
        }

        [Test]
        public void CorruptIndex_RebuildsFromValidReplays_AndSkipsBadFiles()
        {
            SaveFile file = CreateFinishedSave(
                MatchFormat.Individual, DateTime.UtcNow.Ticks, DateTime.UtcNow.Ticks, 3, 0,
                new PlayerResult { PlayerId = 0, DisplayName = "A", FinalScore = 3 });
            Assert.IsTrue(_store.TryArchiveFinishedMatch(file, null, "A", out _, out string error), error);
            File.WriteAllText(Path.Combine(_directory, "history_index.json"), "{ broken");
            File.Delete(Path.Combine(_directory, "history_index.json.bak"));
            File.WriteAllText(Path.Combine(_directory, "match_bad.json"), "not-json");

            Assert.IsTrue(_store.TryListEntries(out var entries, out MatchHistoryReadStatus status, out error), error);
            Assert.AreEqual(MatchHistoryReadStatus.RebuiltFromReplays, status);
            Assert.AreEqual(1, entries.Count);
            StringAssert.Contains("skipped 1", error);
        }

        [Test]
        public void StorageUnavailable_DoesNotThrowFromConstructor()
        {
            string blocker = Path.Combine(_directory, "not-a-directory");
            Directory.CreateDirectory(_directory);
            File.WriteAllText(blocker, "file");

            var store = new MatchHistoryStore(blocker);

            Assert.IsFalse(store.TryListEntries(out var entries, out MatchHistoryReadStatus status, out string error));
            Assert.AreEqual(MatchHistoryReadStatus.StorageUnavailable, status);
            Assert.AreEqual(0, entries.Count);
            Assert.IsNotEmpty(error);
        }

        private static SaveFile CreateFinishedSave(
            MatchFormat format,
            long startedUtcTicks,
            long endedUtcTicks,
            int durationSeconds,
            int winnerPlayerId,
            params PlayerResult[] standings)
        {
            var result = new MatchResult
            {
                Format = format,
                StartedUtcTicks = startedUtcTicks,
                EndedUtcTicks = endedUtcTicks,
                DurationSeconds = durationSeconds,
                WinnerPlayerId = winnerPlayerId
            };
            result.Standings.AddRange(standings);

            return new SaveFile
            {
                RoomName = "Practice",
                RoomCode = "ROOM",
                State = new GameStateSnapshot { Result = result },
                Replay = new ReplayLog
                {
                    Events = { new ReplayEvent { Turn = 1, PlayerId = 0 } }
                }
            };
        }
    }
}
