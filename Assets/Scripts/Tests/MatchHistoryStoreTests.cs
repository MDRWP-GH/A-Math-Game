using System;
using System.IO;
using AMath.Core;
using AMath.Core.History;
using AMath.Core.Snapshot;
using AMath.Replay;
using AMath.Save;
using NUnit.Framework;

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
                _store.TryArchiveFinishedMatch(file, "ann", "guid-ann", "Ann", out MatchHistoryEntry entry, out string error),
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
                _store.TryArchiveFinishedMatch(file, "ben", "guid-ben", "Ben", out MatchHistoryEntry entry, out string error),
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
                _store.TryArchiveFinishedMatch(file, "a", "guid-a", "A", out MatchHistoryEntry entry, out string error),
                error);
            Assert.AreEqual(MatchFormat.Team, entry.Format);
            Assert.IsTrue(entry.HasLocalPlayer);
            Assert.IsTrue(entry.DidWin);
            Assert.AreEqual("A", entry.LocalPlayerName);
            Assert.AreEqual(30, entry.LocalPlayerScore);
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
