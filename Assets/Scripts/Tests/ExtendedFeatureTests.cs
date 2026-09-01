using AMath.Core;
using AMath.Core.Events;
using AMath.Core.StateMachines;
using AMath.Gameplay.Board;
using AMath.Gameplay.Players;
using AMath.Managers;
using NUnit.Framework;

namespace AMath.Tests
{
    public sealed class ExtendedFeatureTests
    {
        [Test]
        public void EndMatch_TeamMode_AggregatesTeamScores()
        {
            var bus = new EventBus();
            var stateMachine = new GameStateMachine(bus);
            var board = new BoardManager();
            var players = new PlayerManager(bus);
            var turns = new TurnManager(bus);
            var game = new GameManager(bus, stateMachine, board, players, turns) { IsAuthority = true };

            var config = new MatchConfig
            {
                RandomSeed = 42,
                TurnSeconds = 60,
                GameVersion = "test",
                Format = MatchFormat.Team,
                Players =
                {
                    new PlayerIdentity { PlayerId = 0, PersistentGuid = "a", DisplayName = "A", TeamId = 0 },
                    new PlayerIdentity { PlayerId = 1, PersistentGuid = "b", DisplayName = "B", TeamId = 1 },
                    new PlayerIdentity { PlayerId = 2, PersistentGuid = "c", DisplayName = "C", TeamId = 0 },
                    new PlayerIdentity { PlayerId = 3, PersistentGuid = "d", DisplayName = "D", TeamId = 1 }
                }
            };

            game.StartMatch(config);
            foreach (PlayerState player in players.Players)
                player.Rack.Clear();

            players.GetById(0).Score = 10;
            players.GetById(1).Score = 30;
            players.GetById(2).Score = 15;
            players.GetById(3).Score = 5;

            game.EndMatchManually();

            Assert.AreEqual(MatchFormat.Team, game.Result.Format);
            Assert.AreEqual(1, game.Result.WinnerTeamId);
            Assert.Greater(game.Result.DurationSeconds, -1);
            Assert.AreEqual(2, game.Result.TeamStandings.Count);
            Assert.Greater(game.Result.TeamStandings[0].TotalScore, game.Result.TeamStandings[1].TotalScore);
        }
    }
}
