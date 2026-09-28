using System;
using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Assistance;
using AMath.Core.Assistance.Context;
using AMath.Core.Commands;
using AMath.Core.Events;
using AMath.Core.Snapshot;
using AMath.Core.StateMachines;
using AMath.Gameplay.Board;
using AMath.Gameplay.Players;
using AMath.Managers;
using NUnit.Framework;
using UnityEngine;

namespace AMath.Tests
{
    /// <summary>
    /// Guards the invariants that keep a client's mirror of the match honest:
    /// records apply exactly once and in order, seats resolve by id, and a
    /// restored snapshot brings its phase with it.
    /// </summary>
    public sealed class MatchStateIntegrityTests
    {
        private sealed class Rig
        {
            public EventBus Bus;
            public GameStateMachine StateMachine;
            public GameManager Game;
            public PlayerManager Players;
            public TurnManager Turns;
        }

        private static Rig CreateRig(bool authority)
        {
            var bus = new EventBus();
            var stateMachine = new GameStateMachine(bus);
            var board = new BoardManager();
            var players = new PlayerManager(bus);
            var turns = new TurnManager(bus);
            var game = new GameManager(bus, stateMachine, board, players, turns) { IsAuthority = authority };

            return new Rig
            {
                Bus = bus,
                StateMachine = stateMachine,
                Game = game,
                Players = players,
                Turns = turns
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

        [Test]
        public void RestoreSnapshot_RejectsIncompleteRosterWithoutChangingLiveMatch()
        {
            Rig rig = CreateRig(authority: true);
            rig.Game.StartMatch(TwoPlayerConfig(22));
            GameStateSnapshot damaged = rig.Game.CaptureSnapshot();
            damaged.Players = null;

            Assert.Throws<ArgumentException>(() => rig.Game.RestoreSnapshot(damaged));
            Assert.AreEqual(MatchPhase.Playing, rig.Game.Phase);
            Assert.AreEqual(2, rig.Players.Players.Count);
            Assert.AreEqual(1, rig.Turns.TurnNumber);
        }

        [Test]
        public void RestoreSnapshot_RejectsDuplicateBoardCellsBeforeMutation()
        {
            Rig rig = CreateRig(authority: true);
            rig.Game.StartMatch(TwoPlayerConfig(23));
            GameStateSnapshot damaged = rig.Game.CaptureSnapshot();
            var cell = new TilePlacement { X = 7, Y = 7, TileId = 0 };
            damaged.BoardCells.Add(cell);
            damaged.BoardCells.Add(cell);

            Assert.Throws<ArgumentException>(() => rig.Game.RestoreSnapshot(damaged));
            Assert.AreEqual(0, rig.Game.CaptureSnapshot().BoardCells.Count);
        }

        [Test]
        public void RestoreSnapshot_RejectsUnknownTilesBeforeMutation()
        {
            Rig rig = CreateRig(authority: true);
            rig.Game.StartMatch(TwoPlayerConfig(24));
            GameStateSnapshot damaged = rig.Game.CaptureSnapshot();
            damaged.BagTiles[0] = byte.MaxValue;

            Assert.Throws<ArgumentException>(() => rig.Game.RestoreSnapshot(damaged));
            Assert.AreEqual(MatchPhase.Playing, rig.Game.Phase);
            Assert.AreEqual(1, rig.Turns.TurnNumber);

            damaged = rig.Game.CaptureSnapshot();
            damaged.BoardCells.Add(new TilePlacement
            {
                X = 7,
                Y = 7,
                TileId = AMathTileSet.Blank,
                DeclaredAs = TilePlacement.NoDeclaration
            });
            Assert.Throws<ArgumentException>(() => rig.Game.RestoreSnapshot(damaged));
            Assert.AreEqual(0, rig.Game.CaptureSnapshot().BoardCells.Count);
        }

        [Test]
        public void ApplyRecord_IgnoresARecordItAlreadyExecuted()
        {
            Rig host = CreateRig(authority: true);
            Rig client = CreateRig(authority: false);
            host.Game.StartMatch(TwoPlayerConfig(31337));
            client.Game.StartMatch(TwoPlayerConfig(31337));

            Assert.IsTrue(host.Game.SubmitCommand(0, new PassTurnCommand(), out TurnRecord record).Success);
            Assert.IsTrue(client.Game.ApplyRecord(record));

            var afterFirstApply = client.Game.CaptureSnapshot();

            // A duplicated broadcast must not advance the turn a second time.
            Assert.IsFalse(client.Game.ApplyRecord(record));
            var afterDuplicate = client.Game.CaptureSnapshot();
            Assert.That(afterDuplicate.MatchElapsedSeconds,
                Is.GreaterThanOrEqualTo(afterFirstApply.MatchElapsedSeconds));
            afterDuplicate.MatchElapsedSeconds = afterFirstApply.MatchElapsedSeconds;
            Assert.AreEqual(JsonUtility.ToJson(afterFirstApply), JsonUtility.ToJson(afterDuplicate));
        }

        [Test]
        public void ApplyRecord_RejectsRecordsAfterMatchFinished()
        {
            Rig host = CreateRig(authority: true);
            Rig client = CreateRig(authority: false);
            host.Game.StartMatch(TwoPlayerConfig(4242));
            client.Game.StartMatch(TwoPlayerConfig(4242));

            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(
                    host.Game.SubmitCommand(host.Turns.CurrentPlayerId, new PassTurnCommand(), out TurnRecord record).Success);
                Assert.IsTrue(client.Game.ApplyRecord(record));
            }

            Assert.AreEqual(MatchPhase.Finished, host.Game.Phase);
            Assert.AreEqual(MatchPhase.Finished, client.Game.Phase);

            var lateRecord = new TurnRecord
            {
                TurnNumber = client.Turns.TurnNumber,
                PlayerId = 0,
                CommandType = (byte)CommandType.PassTurn,
                CommandPayload = CommandSerializer.Serialize(new PassTurnCommand())
            };

            Assert.IsFalse(client.Game.ApplyRecord(lateRecord));
        }

        [Test]
        public void TurnManager_RejectsZeroSeatRoster()
        {
            var turns = new TurnManager(new EventBus());
            Assert.Throws<System.ArgumentOutOfRangeException>(() => turns.StartMatch(0, 60));
        }

        [Test]
        public void BoardGrid_Restore_IgnoresDuplicateCells()
        {
            var grid = new BoardGrid();
            grid.Restore(new[]
            {
                new TilePlacement { X = 7, Y = 7, TileId = 1 },
                new TilePlacement { X = 7, Y = 7, TileId = 2 }
            });

            Assert.AreEqual(1, grid.PlacedCount);
            Assert.AreEqual((byte)1, grid.CellAt(7, 7).TileId);
        }

        [Test]
        public void ApplyRecord_ReportsDesyncWhenARecordIsMissing()
        {
            Rig host = CreateRig(authority: true);
            Rig client = CreateRig(authority: false);
            host.Game.StartMatch(TwoPlayerConfig(999));
            client.Game.StartMatch(TwoPlayerConfig(999));

            string desyncReason = null;
            client.Bus.Subscribe<DesyncDetectedEvent>(evt => desyncReason = evt.Reason);

            Assert.IsTrue(host.Game.SubmitCommand(0, new PassTurnCommand(), out _).Success);
            Assert.IsTrue(host.Game.SubmitCommand(1, new PassTurnCommand(), out TurnRecord second).Success);

            // The client never saw turn 1, so turn 2 must not be applied blind.
            Assert.IsFalse(client.Game.ApplyRecord(second));
            Assert.IsNotNull(desyncReason);
            Assert.AreEqual(1, client.Turns.TurnNumber);
        }

        [Test]
        public void RestoreSnapshot_BringsThePhaseWithIt()
        {
            Rig live = CreateRig(authority: true);
            live.Game.StartMatch(TwoPlayerConfig(2024));
            Assert.AreEqual(MatchPhase.Playing, live.Game.Phase);

            string json = JsonUtility.ToJson(live.Game.CaptureSnapshot());

            // A reconnecting client sits in Lobby, which has no legal live
            // transition to Playing — the snapshot's phase must still apply.
            Rig joining = CreateRig(authority: false);
            Assert.AreEqual(MatchPhase.Lobby, joining.Game.Phase);
            joining.Game.RestoreSnapshot(JsonUtility.FromJson<GameStateSnapshot>(json));

            Assert.AreEqual(MatchPhase.Playing, joining.Game.Phase);
        }

        [Test]
        public void RestoreSnapshot_HonoursAnExplicitPhaseOverride()
        {
            Rig live = CreateRig(authority: true);
            live.Game.StartMatch(TwoPlayerConfig(77));
            string json = JsonUtility.ToJson(live.Game.CaptureSnapshot());

            Rig migratedHost = CreateRig(authority: true);
            migratedHost.Game.RestoreSnapshot(
                JsonUtility.FromJson<GameStateSnapshot>(json), MatchPhase.Paused);

            Assert.AreEqual(MatchPhase.Paused, migratedHost.Game.Phase);
        }

        [Test]
        public void GetById_ResolvesSeatsThatAreNotListIndexes()
        {
            var players = new PlayerManager(new EventBus());
            players.Setup(new MatchConfig
            {
                RandomSeed = 1,
                TurnSeconds = 60,
                GameVersion = "test",
                Players =
                {
                    new PlayerIdentity { PlayerId = 5, PersistentGuid = "g5", DisplayName = "Five" },
                    new PlayerIdentity { PlayerId = 2, PersistentGuid = "g2", DisplayName = "Two" }
                }
            });

            Assert.AreEqual("Five", players.GetById(5).DisplayName);
            Assert.AreEqual("Two", players.GetById(2).DisplayName);
            Assert.IsNull(players.GetById(0));
            Assert.IsNull(players.GetById(-1));
        }

        /// <summary>
        /// Rack writes feed the lockstep engine, so a seat that does not exist is
        /// a bug in this process. Absorbing it silently is what lets peers drift
        /// apart without anything reporting a problem.
        /// </summary>
        [Test]
        public void RackOperationsForAnUnknownSeat_AreRejected()
        {
            var players = new PlayerManager(new EventBus());
            players.Setup(TwoPlayerConfig(1));

            Assert.Throws<InvalidOperationException>(() => players.AddToRack(99, new List<byte> { 1 }));
            Assert.Throws<InvalidOperationException>(() => players.RemoveFromRack(99, new List<byte> { 1 }));
        }

        [Test]
        public void RemovingATileTheRackDoesNotHold_IsRejected()
        {
            var players = new PlayerManager(new EventBus());
            players.Setup(TwoPlayerConfig(1));
            players.AddToRack(0, new List<byte> { 1, 2, 3 });

            // Silently ignoring this would leave the tile on the rack while the
            // command that "spent" it went on to place it on the board.
            Assert.Throws<InvalidOperationException>(
                () => players.RemoveFromRack(0, new List<byte> { 1, 9 }));
        }

        [Test]
        public void ReplacingAService_StopsTickingTheOldInstance()
        {
            var registry = new ServiceRegistry();
            var first = new CountingTickable();
            var second = new CountingTickable();

            registry.Register(first);
            registry.RegisterOrReplace(second);
            registry.TickAll(0.016f);

            Assert.AreEqual(0, first.Ticks, "A replaced service must stop receiving Update.");
            Assert.AreEqual(1, second.Ticks);
        }

        [Test]
        public void CommandRejectionTracker_ClearsOnceATurnResolves()
        {
            var bus = new EventBus();
            using var tracker = new CommandRejectionTracker(bus);

            bus.Publish(new CommandRejectedEvent { Reason = "Both sides of '=' are not equal." });
            Assert.AreEqual("Both sides of '=' are not equal.", tracker.LastRejectionReason);

            bus.Publish(new TurnResolvedEvent { Record = new TurnRecord(), IsAuthority = false });
            Assert.IsNull(tracker.LastRejectionReason);
        }

        [Test]
        public void TutorialProgressTracker_FollowsTheActiveStep()
        {
            var bus = new EventBus();
            using var tracker = new TutorialProgressTracker(bus);

            Assert.IsFalse(tracker.IsTutorialActive);

            bus.Publish(new TutorialStepChangedEvent
            {
                TutorialId = "intro",
                StepId = "intro.welcome",
                StepIndex = 1,
                TotalSteps = 4,
                ObjectiveText = "Place a tile",
                IsActive = true
            });

            Assert.IsTrue(tracker.IsTutorialActive);
            Assert.AreEqual("intro", tracker.ActiveTutorialId);
            Assert.AreEqual("intro.welcome", tracker.CurrentStepId);
            Assert.AreEqual(1, tracker.CurrentStepIndex);
            Assert.AreEqual(4, tracker.TotalStepCount);
            Assert.AreEqual("Place a tile", tracker.CurrentObjectiveText);

            bus.Publish(new TutorialStepChangedEvent { IsActive = false });

            Assert.IsFalse(tracker.IsTutorialActive);
            Assert.IsNull(tracker.CurrentStepId);
            Assert.AreEqual(-1, tracker.CurrentStepIndex);
        }

        private sealed class CountingTickable : ITickable
        {
            public int Ticks { get; private set; }

            public void Tick(float deltaTime) => Ticks++;
        }
    }
}
