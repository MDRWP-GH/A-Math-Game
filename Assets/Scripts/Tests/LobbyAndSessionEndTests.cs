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

        #region Team roster rules

        [Test]
        public void PlannedSeatCount_FillsIndividualUpToTheMinimum()
        {
            Assert.AreEqual(GameRules.MinPlayers, GameRules.PlannedSeatCount(MatchFormat.Individual, 1));
            Assert.AreEqual(3, GameRules.PlannedSeatCount(MatchFormat.Individual, 3));
        }

        [Test]
        public void PlannedSeatCount_FillsTeamsToAnEvenRoster()
        {
            // A solo host can still pick team mode: AI fills the other seats.
            Assert.AreEqual(GameRules.MinTeamMatchPlayers, GameRules.PlannedSeatCount(MatchFormat.Team, 1));
            Assert.AreEqual(GameRules.MinTeamMatchPlayers, GameRules.PlannedSeatCount(MatchFormat.Team, 4));

            // Five humans cannot be split evenly, so a sixth seat is added.
            Assert.AreEqual(6, GameRules.PlannedSeatCount(MatchFormat.Team, 5));
        }

        [Test]
        public void IsValidTeamRoster_RequiresEvenSeatsAboveTheMinimum()
        {
            Assert.IsFalse(GameRules.IsValidTeamRoster(2));
            Assert.IsFalse(GameRules.IsValidTeamRoster(3));
            Assert.IsTrue(GameRules.IsValidTeamRoster(4));
            Assert.IsFalse(GameRules.IsValidTeamRoster(5));
            Assert.IsTrue(GameRules.IsValidTeamRoster(6));
            Assert.IsFalse(GameRules.IsValidTeamRoster(GameRules.MaxPlayers + 2));
        }

        [Test]
        public void PlannedSeatCount_AgreesWithTheTeamRosterRule()
        {
            // The lobby enables "start" from these two rules combined, so any
            // disagreement would offer a start the host then refuses.
            for (int humans = 1; humans <= GameRules.MaxPlayers; humans++)
            {
                int seats = GameRules.PlannedSeatCount(MatchFormat.Team, humans);
                if (seats > GameRules.MaxPlayers) continue;

                Assert.IsTrue(
                    GameRules.IsValidTeamRoster(seats),
                    $"{humans} humans plan {seats} seats, which team mode rejects.");
            }
        }

        #endregion
    }
}
