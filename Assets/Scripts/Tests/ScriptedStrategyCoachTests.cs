using System.Collections.Generic;
using AMath.AI.Modes.StrategyCoach;
using AMath.Core.Assistance;
using AMath.Core.Assistance.Context;
using AMath.Core.StateMachines;
using AMath.Gameplay.Board;
using NUnit.Framework;
using static AMath.Gameplay.Board.AMathTileSet;

namespace AMath.Tests
{
    public sealed class ScriptedStrategyCoachTests
    {
        /// <summary>
        /// Echoes keys back so the assertions describe which tip fired rather
        /// than which language the project happens to be set to.
        /// </summary>
        private sealed class KeyEchoTextProvider : ILocalizedTextProvider
        {
            public string GetText(string key) => key;
        }

        [Test]
        public void StrategyCoach_UsesScripts_NotEmptyOnFirstMove()
        {
            var coach = new StrategyCoachMode(new KeyEchoTextProvider());
            var context = new GameContextSnapshot
            {
                MatchPhase = MatchPhase.Playing,
                BoardCells = new List<TilePlacement>(),
                LocalPlayerHand = new List<byte> { 1, Plus, 2, EqualsSign, 3 },
                TilesRemainingInBag = 80
            };

            string answer = coach.RespondAsync("จะเริ่มยังไง", context, default).Result;
            StringAssert.Contains(StrategyTipKeys.FirstMoveCenter, answer);
            StringAssert.Contains(StrategyTipKeys.Header, answer);
        }

        [Test]
        public void StrategyCoach_SurfacesRejectionReason()
        {
            var coach = new StrategyCoachMode(new KeyEchoTextProvider());
            var context = new GameContextSnapshot
            {
                MatchPhase = MatchPhase.Playing,
                BoardCells = new List<TilePlacement> { new() { TileId = 1, X = 7, Y = 7 } },
                LocalPlayerHand = new List<byte> { 1, EqualsSign, 1 },
                LastCommandRejectionReason = "Both sides of '=' are not equal.",
                TilesRemainingInBag = 40
            };

            string answer = coach.RespondAsync("ทำไมไม่ได้", context, default).Result;
            StringAssert.Contains(StrategyTipKeys.Rejection, answer);
        }

        [Test]
        public void StrategyCoach_FormatsRejectionReasonIntoTheTip()
        {
            var coach = new StrategyCoachMode(new FormatTextProvider());
            var context = new GameContextSnapshot
            {
                MatchPhase = MatchPhase.Playing,
                BoardCells = new List<TilePlacement> { new() { TileId = 1, X = 7, Y = 7 } },
                LocalPlayerHand = new List<byte> { 1, EqualsSign, 1 },
                LastCommandRejectionReason = "Both sides of '=' are not equal.",
                TilesRemainingInBag = 40
            };

            string answer = coach.RespondAsync(null, context, default).Result;
            StringAssert.Contains("Both sides of '=' are not equal.", answer);
        }

        /// <summary>Supplies a real format string so the {0} substitution is exercised.</summary>
        private sealed class FormatTextProvider : ILocalizedTextProvider
        {
            public string GetText(string key) =>
                key == StrategyTipKeys.Rejection ? "rejected: {0}" : key;
        }
    }
}
