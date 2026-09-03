using AMath.Core;
using AMath.Core.Events;
using AMath.Core.StateMachines;
using AMath.Gameplay.Board;
using AMath.Gameplay.Players;
using AMath.Managers;
using NUnit.Framework;

namespace AMath.Tests
{
    /// <summary>
    /// Guards the two rules that decide what players see when a session ends
    /// early: a lobby must never produce a scoreboard, and the lobby's "start"
    /// gate must agree with the roster the host will actually seat.
    /// </summary>
    public sealed class LobbyAndSessionEndTests
    {
        #region Rig

        private sealed class Rig
        {
            public EventBus Bus;
            public GameStateMachine StateMachine;
            public GameManager Game;
            public int FinishedEvents;
        }

        private static Rig CreateRig()
        {
            var bus = new EventBus();
            var rig = new Rig
            {
                Bus = bus,
                StateMachine = new GameStateMachine(bus)
            };

            rig.Game = new GameManager(
                bus,
                rig.StateMachine,
                new BoardManager(),
                new PlayerManager(bus),
                new TurnManager(bus)) { IsAuthority = true };

            bus.Subscribe<MatchFinishedEvent>(_ => rig.FinishedEvents++);
            return rig;
        }

        private static MatchConfig TwoPlayerConfig() => new()
        {
            RandomSeed = 7,
            TurnSeconds = 60,
            GameVersion = "test",
            Players =
            {
                new PlayerIdentity { PlayerId = 0, PersistentGuid = "guid-a", DisplayName = "A" },
                new PlayerIdentity { PlayerId = 1, PersistentGuid = "guid-b", DisplayName = "B" }
            }
        };

        #endregion

        #region Ending a session that never started

        [Test]
        public void EndMatchManually_InLobby_PublishesNoResult()
        {
            Rig rig = CreateRig();
            Assert.AreEqual(MatchPhase.Lobby, rig.Game.Phase);

            rig.Game.EndMatchManually();

            Assert.AreEqual(0, rig.FinishedEvents, "A lobby must not announce a match result.");
            Assert.IsNull(rig.Game.Result);
            Assert.AreEqual(MatchPhase.Lobby, rig.Game.Phase);
        }

        [Test]
        public void EndMatchManually_DuringMatch_PublishesResult()
        {
            Rig rig = CreateRig();
            rig.Game.StartMatch(TwoPlayerConfig());

            rig.Game.EndMatchManually();

            Assert.AreEqual(1, rig.FinishedEvents);
            Assert.IsNotNull(rig.Game.Result);
            Assert.AreEqual(MatchPhase.Finished, rig.Game.Phase);
        }

        [Test]
        public void EndMatchManually_AfterAbandon_StaysSilent()
        {
            Rig rig = CreateRig();
            rig.Game.StartMatch(TwoPlayerConfig());
            rig.Game.AbandonSession();

            rig.Game.EndMatchManually();

            Assert.AreEqual(0, rig.FinishedEvents);
            Assert.IsNull(rig.Game.Result);
        }

        #endregion

        #region Human roster rules

        [Test]
        public void IsValidHumanRoster_AcceptsTwoToFourPlayers()
        {
            Assert.IsFalse(GameRules.IsValidHumanRoster(0));
            Assert.IsFalse(GameRules.IsValidHumanRoster(1));
            Assert.IsTrue(GameRules.IsValidHumanRoster(2));
            Assert.IsTrue(GameRules.IsValidHumanRoster(3));
            Assert.IsTrue(GameRules.IsValidHumanRoster(4));
            Assert.IsFalse(GameRules.IsValidHumanRoster(5));
        }

        [Test]
        public void PlannedSeatCount_DoesNotPadHumans()
        {
            Assert.AreEqual(1, GameRules.PlannedSeatCount(MatchFormat.Individual, 1));
            Assert.AreEqual(3, GameRules.PlannedSeatCount(MatchFormat.Individual, 3));
            Assert.AreEqual(2, GameRules.PlannedSeatCount(MatchFormat.Team, 2));
            Assert.AreEqual(4, GameRules.PlannedSeatCount(MatchFormat.Team, 4));
        }

        [Test]
        public void PlannedSeatCount_AddsOnlyExplicitAi()
        {
            Assert.AreEqual(3, GameRules.PlannedSeatCount(MatchFormat.Individual, 2, extraAiPlayers: 1));
            Assert.AreEqual(2, GameRules.PlannedSeatCount(MatchFormat.Team, 2, extraAiPlayers: 0));
        }

        [Test]
        public void IsValidTeamSplit_RequiresBothTeams()
        {
            Assert.IsFalse(GameRules.IsValidTeamSplit(0, 2));
            Assert.IsFalse(GameRules.IsValidTeamSplit(3, 0));
            Assert.IsTrue(GameRules.IsValidTeamSplit(1, 1));
            Assert.IsTrue(GameRules.IsValidTeamSplit(1, 3));
            Assert.IsTrue(GameRules.IsValidTeamSplit(2, 2));
        }

        #endregion
    }
}
