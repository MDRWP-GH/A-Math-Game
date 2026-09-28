using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Commands;
using AMath.Core.Events;
using AMath.Core.Snapshot;
using AMath.Core.StateMachines;
using AMath.Gameplay.Board;
using AMath.Gameplay.Players;
using AMath.Managers;
using AMath.Replay;
using NUnit.Framework;
using UnityEngine;

namespace AMath.Tests
{
    /// <summary>
    /// End-to-end tests of the deterministic match engine — the exact rig used
    /// live is assembled headlessly (proving the Core assembly truly has no
    /// networking dependency) and driven through commands, then reconstructed
    /// from its own replay log.
    /// </summary>
    public sealed class MatchFlowAndReplayTests
    {
        #region Rig

        private sealed class Rig
        {
            public EventBus Bus;
            public GameStateMachine StateMachine;
            public GameManager Game;
            public PlayerManager Players;
            public TurnManager Turns;
            public ReplayManager Replay;
        }

        private sealed class FakeMatchClock : IMatchClock
        {
            public long UtcNowTicks { get; set; }
            public double MonotonicSeconds { get; set; }
        }

        private static Rig CreateRig(bool authority = true, IMatchClock clock = null)
        {
            var bus = new EventBus();
            var stateMachine = new GameStateMachine(bus);
            var board = new BoardManager();
            var players = new PlayerManager(bus);
            var turns = new TurnManager(bus);
            var game = new GameManager(bus, stateMachine, board, players, turns, clock) { IsAuthority = authority };
            var replay = new ReplayManager(bus);

            return new Rig
            {
                Bus = bus,
                StateMachine = stateMachine,
                Game = game,
                Players = players,
                Turns = turns,
                Replay = replay
            };
        }

        private static MatchConfig TwoPlayerConfig(int seed) => new()
        {
            RandomSeed = seed,
            TurnSeconds = 60,
            GameVersion = "test",
            Players =
            {
                new PlayerIdentity { PlayerId = 0, PersistentGuid = "guid-a", DisplayName = "A" },
                new PlayerIdentity { PlayerId = 1, PersistentGuid = "guid-b", DisplayName = "B" }
            }
        };

        #endregion

        [Test]
        public void StartMatch_DealsFullRacksDeterministically()
        {
            Rig a = CreateRig();
            Rig b = CreateRig();
            a.Game.StartMatch(TwoPlayerConfig(4242));
            b.Game.StartMatch(TwoPlayerConfig(4242));

            for (int p = 0; p < 2; p++)
            {
                Assert.AreEqual(GameRules.RackSize, a.Players.Players[p].Rack.Count);
                CollectionAssert.AreEqual(a.Players.Players[p].Rack, b.Players.Players[p].Rack);
            }

            Assert.AreEqual(MatchPhase.Playing, a.Game.Phase);
            Assert.AreEqual(100 - 2 * GameRules.RackSize, a.Game.BagCount);
        }

        [Test]
        public void CommandFromWrongPlayer_IsRejected()
        {
            Rig rig = CreateRig();
            rig.Game.StartMatch(TwoPlayerConfig(1));

            // It is player 0's turn; player 1 tries to act.
            var outcome = rig.Game.SubmitCommand(1, new PassTurnCommand(), out _);
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(1, rig.Turns.TurnNumber);
        }

        [Test]
        public void ConsecutivePasses_EndTheMatch()
        {
            Rig rig = CreateRig();
            rig.Game.StartMatch(TwoPlayerConfig(2));

            // 2 players x 2 rounds of passes = 4 consecutive passes.
            for (int i = 0; i < 4; i++)
            {
                var outcome = rig.Game.SubmitCommand(rig.Turns.CurrentPlayerId, new PassTurnCommand(), out _);
                Assert.IsTrue(outcome.Success);
            }

            Assert.AreEqual(MatchPhase.Finished, rig.Game.Phase);
            Assert.AreEqual(MatchEndReason.AllPlayersPassed, rig.Game.Result.Reason);
        }

        [Test]
        public void TerminalTurn_ReachesReplayBeforeMatchFinishedObservers()
        {
            Rig rig = CreateRig();
            var terminalOrder = new List<string>();
            int replayCountAtFinish = -1;

            rig.Bus.Subscribe<TurnResolvedEvent>(evt =>
            {
                if (evt.Record.EndedMatch)
                    terminalOrder.Add("turn");
            });
            rig.Bus.Subscribe<MatchFinishedEvent>(_ =>
            {
                terminalOrder.Add("finished");
                replayCountAtFinish = rig.Replay.Log.Events.Count;
            });

            rig.Game.StartMatch(TwoPlayerConfig(31415));
            int submittedTurns = 0;
            while (rig.Game.Phase == MatchPhase.Playing && submittedTurns < 10)
            {
                Assert.IsTrue(rig.Game.SubmitCommand(
                    rig.Turns.CurrentPlayerId,
                    new PassTurnCommand(),
                    out _).Success);
                submittedTurns++;
            }

            CollectionAssert.AreEqual(new[] { "turn", "finished" }, terminalOrder);
            Assert.AreEqual(submittedTurns, replayCountAtFinish);
        }

        [Test]
        public void Replay_ReconstructsIdenticalState()
        {
            Rig live = CreateRig();
            live.Game.StartMatch(TwoPlayerConfig(987654));

            // Exchange (consumes the random stream) followed by passes to finish.
            var exchange = new ExchangeTilesCommand();
            exchange.TileIds.Add(live.Players.Players[0].Rack[0]);
            exchange.TileIds.Add(live.Players.Players[0].Rack[1]);
            Assert.IsTrue(live.Game.SubmitCommand(0, exchange, out _).Success);

            for (int i = 0; i < 3; i++)
                Assert.IsTrue(live.Game.SubmitCommand(live.Turns.CurrentPlayerId, new PassTurnCommand(), out _).Success);

            Assert.AreEqual(MatchPhase.Finished, live.Game.Phase);

            // Rebuild the entire match from seed + event log only.
            bool ok = ReplayReconstructor.TryReconstruct(live.Replay.Log, int.MaxValue,
                out GameStateSnapshot reconstructed, out string error);
            Assert.IsTrue(ok, error);

            GameStateSnapshot original = live.Game.CaptureSnapshot();
            Assert.AreEqual(original.RandomState, reconstructed.RandomState);
            Assert.AreEqual(original.TurnNumber, reconstructed.TurnNumber);
            CollectionAssert.AreEqual(original.BagTiles, reconstructed.BagTiles);
            for (int p = 0; p < 2; p++)
            {
                Assert.AreEqual(original.Players[p].Score, reconstructed.Players[p].Score);
                CollectionAssert.AreEqual(original.Players[p].Rack, reconstructed.Players[p].Rack);
            }
        }

        [Test]
        public void SnapshotRestore_ContinuesIdentically()
        {
            Rig live = CreateRig();
            live.Game.StartMatch(TwoPlayerConfig(555));
            Assert.IsTrue(live.Game.SubmitCommand(0, new PassTurnCommand(), out _).Success);

            // Simulate host migration: a different machine restores the snapshot...
            string json = JsonUtility.ToJson(live.Game.CaptureSnapshot());
            Rig migrated = CreateRig();
            migrated.Game.RestoreSnapshot(JsonUtility.FromJson<GameStateSnapshot>(json));

            // Production flow: restored host pauses, then resumes once players return.
            migrated.StateMachine.TransitionTo(MatchPhase.Paused);
            migrated.StateMachine.TransitionTo(MatchPhase.Playing);

            // ...and both worlds resolve the next turns identically.
            for (int i = 0; i < 3; i++)
            {
                int actor = live.Turns.CurrentPlayerId;
                Assert.IsTrue(live.Game.SubmitCommand(actor, new PassTurnCommand(), out _).Success);
                Assert.IsTrue(migrated.Game.SubmitCommand(actor, new PassTurnCommand(), out _).Success);
            }

            GameStateSnapshot liveSnapshot = live.Game.CaptureSnapshot();
            GameStateSnapshot migratedSnapshot = migrated.Game.CaptureSnapshot();
            Assert.That(migratedSnapshot.MatchElapsedSeconds,
                Is.EqualTo(liveSnapshot.MatchElapsedSeconds).Within(1d));
            // Wall time is intentionally not deterministic match state.
            liveSnapshot.MatchElapsedSeconds = 0d;
            migratedSnapshot.MatchElapsedSeconds = 0d;
            Assert.AreEqual(JsonUtility.ToJson(liveSnapshot), JsonUtility.ToJson(migratedSnapshot));
        }

        /// <summary>
        /// The bug this covers: restore published MatchStartedEvent, which made
        /// ReplayManager throw away the log for a match that was still going.
        /// </summary>
        [Test]
        public void SnapshotRestore_KeepsTheReplayLogItAlreadyHas()
        {
            Rig live = CreateRig();
            live.Game.StartMatch(TwoPlayerConfig(4242));

            for (int i = 0; i < 2; i++)
                Assert.IsTrue(live.Game.SubmitCommand(live.Turns.CurrentPlayerId, new PassTurnCommand(), out _).Success);

            int recordedTurns = live.Replay.Log.Events.Count;
            Assert.AreEqual(2, recordedTurns);

            // A resync restores the same match into the same peer; the turns it
            // already recorded are still part of that match.
            string json = JsonUtility.ToJson(live.Game.CaptureSnapshot());
            live.Game.RestoreSnapshot(JsonUtility.FromJson<GameStateSnapshot>(json));

            Assert.AreEqual(
                recordedTurns,
                live.Replay.Log.Events.Count,
                "Restoring a match must not clear its replay log.");
        }

        [Test]
        public void StartMatch_StillClearsTheReplayLog()
        {
            Rig rig = CreateRig();
            rig.Game.StartMatch(TwoPlayerConfig(11));
            Assert.IsTrue(rig.Game.SubmitCommand(rig.Turns.CurrentPlayerId, new PassTurnCommand(), out _).Success);
            Assert.AreEqual(1, rig.Replay.Log.Events.Count);

            rig.Game.StartMatch(TwoPlayerConfig(12));

            Assert.AreEqual(0, rig.Replay.Log.Events.Count, "A new match starts from an empty log.");
        }

        [Test]
        public void ReplayReconstructor_RejectsNullEventList()
        {
            var log = new ReplayLog { Config = TwoPlayerConfig(1), Events = null };

            Assert.IsFalse(ReplayReconstructor.TryReconstruct(log, int.MaxValue, out _, out string error));
            Assert.AreEqual("Replay has no events.", error);
        }

        [Test]
        public void MatchDuration_UsesMonotonicClock_WhenUtcMovesBackward()
        {
            long started = new System.DateTime(2026, 9, 16, 0, 0, 0, System.DateTimeKind.Utc).Ticks;
            var clock = new FakeMatchClock { UtcNowTicks = started, MonotonicSeconds = 100d };
            Rig rig = CreateRig(clock: clock);
            rig.Game.StartMatch(TwoPlayerConfig(71));

            clock.MonotonicSeconds = 112.9d;
            clock.UtcNowTicks = started - System.TimeSpan.TicksPerHour;
            rig.Game.EndMatchManually();

            Assert.AreEqual(12, rig.Game.Result.DurationSeconds);
            Assert.GreaterOrEqual(rig.Game.Result.EndedUtcTicks, started + 12 * System.TimeSpan.TicksPerSecond);
        }

        [Test]
        public void SnapshotRestore_ContinuesCapturedElapsedTime()
        {
            long started = new System.DateTime(2026, 9, 16, 0, 0, 0, System.DateTimeKind.Utc).Ticks;
            var firstClock = new FakeMatchClock { UtcNowTicks = started, MonotonicSeconds = 10d };
            Rig first = CreateRig(clock: firstClock);
            first.Game.StartMatch(TwoPlayerConfig(72));
            firstClock.MonotonicSeconds = 25.5d;
            GameStateSnapshot snapshot = first.Game.CaptureSnapshot();

            var restoredClock = new FakeMatchClock
            {
                UtcNowTicks = started + System.TimeSpan.TicksPerHour,
                MonotonicSeconds = 100d
            };
            Rig restored = CreateRig(clock: restoredClock);
            restored.Game.RestoreSnapshot(snapshot);
            restoredClock.MonotonicSeconds = 104.75d;
            restored.Game.EndMatchManually();

            Assert.AreEqual(20, restored.Game.Result.DurationSeconds);
        }

        [Test]
        public void AuthoritativeResult_IsPublishedOnce_AndReplacesClientTiming()
        {
            long started = new System.DateTime(2026, 9, 16, 0, 0, 0, System.DateTimeKind.Utc).Ticks;
            var hostClock = new FakeMatchClock { UtcNowTicks = started, MonotonicSeconds = 0d };
            var clientClock = new FakeMatchClock { UtcNowTicks = started + 100, MonotonicSeconds = 50d };
            Rig host = CreateRig(clock: hostClock);
            Rig client = CreateRig(authority: false, clock: clientClock);
            host.Game.StartMatch(TwoPlayerConfig(73));
            client.Game.StartMatch(TwoPlayerConfig(73));

            int clientFinished = 0;
            client.Bus.Subscribe<MatchFinishedEvent>(_ => clientFinished++);
            hostClock.MonotonicSeconds = 42.8d;
            hostClock.UtcNowTicks = started + 42 * System.TimeSpan.TicksPerSecond;
            host.Game.EndMatchManually();

            Assert.IsTrue(client.Game.ApplyAuthoritativeResult(host.Game.Result));
            Assert.IsTrue(client.Game.ApplyAuthoritativeResult(host.Game.Result));
            Assert.AreEqual(1, clientFinished);
            Assert.AreEqual(42, client.Game.Result.DurationSeconds);
            Assert.AreEqual(host.Game.Result.EndedUtcTicks, client.Game.Result.EndedUtcTicks);
            Assert.AreEqual(MatchPhase.Finished, client.Game.Phase);
        }

        [Test]
        public void ReplicatedFinishedPhase_BeforeTerminalTurn_StillRecordsFinalTurn()
        {
            Rig host = CreateRig();
            Rig client = CreateRig(authority: false);
            host.Game.StartMatch(TwoPlayerConfig(74));
            client.Game.StartMatch(TwoPlayerConfig(74));

            TurnRecord terminal = null;
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(host.Game.SubmitCommand(
                    host.Turns.CurrentPlayerId,
                    new PassTurnCommand(),
                    out TurnRecord record).Success);
                if (record.EndedMatch)
                    terminal = record;
                else
                    Assert.IsTrue(client.Game.ApplyReplicatedRecord(record));
            }

            Assert.NotNull(terminal);
            client.StateMachine.RestoreTo(MatchPhase.Finished);
            Assert.IsTrue(client.Game.ApplyReplicatedRecord(terminal));
            Assert.AreEqual(4, client.Replay.Log.Events.Count);

            int finished = 0;
            client.Bus.Subscribe<MatchFinishedEvent>(_ => finished++);
            client.Game.ApplyAuthoritativeResult(host.Game.Result);
            Assert.AreEqual(1, finished);
        }

        [Test]
        public void InvalidTerminalRecord_AfterFinishedPhase_DoesNotReopenTheMatch()
        {
            Rig host = CreateRig();
            Rig client = CreateRig(authority: false);
            host.Game.StartMatch(TwoPlayerConfig(75));
            client.Game.StartMatch(TwoPlayerConfig(75));

            TurnRecord terminal = null;
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(host.Game.SubmitCommand(
                    host.Turns.CurrentPlayerId, new PassTurnCommand(), out TurnRecord record).Success);
                if (record.EndedMatch)
                    terminal = record;
                else
                    Assert.IsTrue(client.Game.ApplyReplicatedRecord(record));
            }

            Assert.NotNull(terminal);
            client.StateMachine.RestoreTo(MatchPhase.Finished);
            var future = new TurnRecord
            {
                TurnNumber = terminal.TurnNumber + 1,
                EndedMatch = true
            };
            Assert.IsFalse(client.Game.ApplyReplicatedRecord(future));
            Assert.AreEqual(MatchPhase.Finished, client.Game.Phase);

            var invalidActor = new TurnRecord
            {
                TurnNumber = terminal.TurnNumber,
                PlayerId = 99,
                CommandType = terminal.CommandType,
                CommandPayload = terminal.CommandPayload,
                EndedMatch = true
            };
            Assert.IsFalse(client.Game.ApplyReplicatedRecord(invalidActor));
            Assert.AreEqual(MatchPhase.Finished, client.Game.Phase);

            Assert.IsTrue(client.Game.ApplyReplicatedRecord(terminal));
            Assert.AreEqual(MatchPhase.Finished, client.Game.Phase);
            Assert.AreEqual(4, client.Replay.Log.Events.Count);
        }
    }
}
