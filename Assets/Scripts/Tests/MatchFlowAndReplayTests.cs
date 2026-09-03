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

        private static Rig CreateRig(bool authority = true)
        {
            var bus = new EventBus();
            var stateMachine = new GameStateMachine(bus);
            var board = new BoardManager();
            var players = new PlayerManager(bus);
            var turns = new TurnManager(bus);
            var game = new GameManager(bus, stateMachine, board, players, turns) { IsAuthority = authority };
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

            Assert.AreEqual(
                JsonUtility.ToJson(live.Game.CaptureSnapshot()),
                JsonUtility.ToJson(migrated.Game.CaptureSnapshot()));
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
    }
}
