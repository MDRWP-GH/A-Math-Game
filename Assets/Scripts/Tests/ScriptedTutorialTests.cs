using System;
using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Assistance;
using AMath.Core.Commands;
using AMath.Core.Events;
using AMath.Core.RandomNumbers;
using AMath.Core.StateMachines;
using AMath.Gameplay.Board;
using AMath.Gameplay.Interaction;
using AMath.Gameplay.Players;
using AMath.Managers;
using AMath.Tutorial.Definitions;
using AMath.Tutorial.Scripted;
using NUnit.Framework;
using static AMath.Gameplay.Board.AMathTileSet;

namespace AMath.Tests
{
    public sealed class ScriptedTutorialTests
    {
        private sealed class KeysAsText : ILocalizedTextProvider
        {
            public string GetText(string key) => key ?? string.Empty;
        }

        [Test]
        public void IntroScript_BothTurns_AreAcceptedByTheMatchEngine()
        {
            var bus = new EventBus();
            var game = new GameManager(
                bus, new GameStateMachine(bus), new BoardManager(),
                new PlayerManager(bus), new TurnManager(bus))
            {
                IsAuthority = true
            };
            ScriptedTutorialMatchScript script = ScriptedTutorialMatchScript.Intro();
            game.StartMatch(script.Config, script.OpeningRacks);

            var first = new PlaceTilesCommand();
            first.Placements.AddRange(script.Turns[0].Placements);
            CommandOutcome playerMove = game.SubmitCommand(0, first, out _);
            Assert.IsTrue(playerMove.Success, playerMove.Error);

            var second = new PlaceTilesCommand();
            second.Placements.AddRange(script.Turns[1].Placements);
            CommandOutcome botMove = game.SubmitCommand(1, second, out _);
            Assert.IsTrue(botMove.Success, botMove.Error);
        }

        [Test]
        public void StartMatch_DealsPredeterminedOpeningRacks()
        {
            var bus = new EventBus();
            var players = new PlayerManager(bus);
            var game = new GameManager(
                bus, new GameStateMachine(bus), new BoardManager(),
                players, new TurnManager(bus))
            {
                IsAuthority = true
            };
            ScriptedTutorialMatchScript script = ScriptedTutorialMatchScript.Intro();
            game.StartMatch(script.Config, script.OpeningRacks);

            CollectionAssert.AreEqual(script.OpeningRacks[0], players.Players[0].Rack);
            CollectionAssert.AreEqual(script.OpeningRacks[1], players.Players[1].Rack);
            Assert.AreEqual(100 - 2 * GameRules.RackSize, game.BagCount);
        }

        [Test]
        public void Constraint_RejectsOffScriptCell_AndAcceptsAuthoredEquation()
        {
            var text = new KeysAsText();
            var constraint = new ScriptedTurnInputConstraint(text)
            {
                InputEnabled = true,
                Expected = ScriptedTutorialMatchScript.Intro().Turns[0].Placements
            };
            var pending = new List<TilePlacement>();

            Assert.IsFalse(constraint.AllowsPlaceOnCell(1, TilePlacement.NoDeclaration, 0, 0, pending, out string cellError));
            Assert.AreEqual("tutorial.error.off_script_cell", cellError);

            Assert.IsFalse(constraint.AllowsPlaceOnCell(8, TilePlacement.NoDeclaration, 5, 7, pending, out string tileError));
            Assert.AreEqual("tutorial.error.off_script_tile", tileError);

            Assert.IsFalse(constraint.AllowsPass(out string passError));
            Assert.AreEqual("tutorial.error.pass_disabled", passError);

            Assert.IsFalse(constraint.AllowsExchange(out string exchangeError));
            Assert.AreEqual("tutorial.error.exchange_disabled", exchangeError);

            IReadOnlyList<TilePlacement> expected = constraint.Expected;
            for (int i = 0; i < expected.Count; i++)
            {
                TilePlacement placement = expected[i];
                Assert.IsTrue(
                    constraint.AllowsPlaceOnCell(
                        placement.TileId, placement.DeclaredAs, placement.X, placement.Y, pending, out string error),
                    error);
                pending.Add(placement);
            }

            Assert.IsTrue(constraint.AllowsConfirmPlace(pending, out string confirmError), confirmError);
        }

        [Test]
        public void TurnInputSession_HonoursScriptedConstraint()
        {
            var bus = new EventBus();
            var board = new BoardManager();
            var players = new PlayerManager(bus);
            players.Setup(new MatchConfig
            {
                Players =
                {
                    new PlayerIdentity { PlayerId = 0, PersistentGuid = "a", DisplayName = "A" },
                    new PlayerIdentity { PlayerId = 1, PersistentGuid = "b", DisplayName = "B" }
                }
            });
            players.LocalPlayerId = 0;
            players.AddToRack(0, new List<byte> { 1, Plus, 2, EqualsSign, 3, 8, 9, 0 });

            var constraint = new ScriptedTurnInputConstraint(new KeysAsText())
            {
                InputEnabled = true,
                Expected = ScriptedTutorialMatchScript.Intro().Turns[0].Placements
            };
            var session = new TurnInputSession(bus, board, players, constraint);

            session.SelectFromRack(0);
            Assert.IsFalse(session.TryPlaceOnCell(0, 0, out _));

            PlaceAll(session, constraint.Expected, players.GetById(0).Rack);
            Assert.IsTrue(session.TryConfirmPlace(out string error), error);
        }

        [Test]
        public void TutorialMatchHost_PlayerThenBotFollowTheScript()
        {
            var bus = new EventBus();
            using var host = new TutorialMatchHost(bus, new KeysAsText(), ScriptedTutorialMatchScript.Intro());

            bus.Publish(new TutorialStepChangedEvent
            {
                TutorialId = "intro",
                StepId = IntroTutorialSequence.PlayerPlaceStepId,
                StepIndex = 2,
                TotalSteps = 5,
                IsActive = true
            });

            Assert.IsTrue(host.IsPlayerInputEnabled);
            PlaceAll(host.Input, host.RemainingGuidePlacements, host.Players.GetById(0).Rack);
            Assert.IsTrue(host.Input.TryConfirmPlace(out string confirmError), confirmError);

            bus.Publish(new TutorialStepChangedEvent
            {
                TutorialId = "intro",
                StepId = IntroTutorialSequence.OpponentStepId,
                StepIndex = 3,
                TotalSteps = 5,
                IsActive = true
            });

            Assert.IsFalse(host.IsPlayerInputEnabled);
            host.Tick(1f);
            Assert.AreEqual(ScriptedTutorialMatchScript.HumanPlayerId, host.Turns.CurrentPlayerId);
            Assert.Greater(host.Players.GetById(1).Score, 0);
        }

        [Test]
        public void TileBag_TryTakeSpecific_LeavesBagUnchangedWhenATileIsMissing()
        {
            var bag = new TileBag();
            bag.Reset(new DeterministicRandom(1));
            int before = bag.Count;
            var taken = new List<byte>();

            Assert.IsFalse(bag.TryTakeSpecific(new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 }, taken));
            Assert.AreEqual(before, bag.Count);
            Assert.AreEqual(0, taken.Count);

            Assert.IsTrue(bag.TryTakeSpecific(new byte[] { 1, Plus, 2 }, taken));
            Assert.AreEqual(before - 3, bag.Count);
            CollectionAssert.AreEqual(new byte[] { 1, Plus, 2 }, taken);
        }

        private static void PlaceAll(
            TurnInputSession session,
            IReadOnlyList<TilePlacement> placements,
            IReadOnlyList<byte> rack)
        {
            var used = new bool[rack.Count];
            for (int p = 0; p < placements.Count; p++)
            {
                TilePlacement placement = placements[p];
                int rackIndex = -1;
                for (int i = 0; i < rack.Count; i++)
                {
                    if (used[i] || rack[i] != placement.TileId) continue;
                    used[i] = true;
                    rackIndex = i;
                    break;
                }

                Assert.GreaterOrEqual(rackIndex, 0, $"Rack is missing tile {placement.TileId}");
                session.SelectFromRack(rackIndex);
                Assert.IsTrue(session.TryPlaceOnCell(placement.X, placement.Y, out string error), error);
            }
        }
    }
}
