using System.Collections.Generic;
using AMath.AI.Modes.StrategyCoach;
using AMath.Core.Assistance.Context;
using AMath.Core.StateMachines;
using AMath.Gameplay.Board;
using NUnit.Framework;
using static AMath.Gameplay.Board.AMathTileSet;

namespace AMath.Tests
{
    public sealed class ScriptedStrategyCoachTests
    {
        [Test]
        public void StrategyCoach_UsesScripts_NotEmptyOnFirstMove()
        {
            var coach = new StrategyCoachMode();
            var context = new GameContextSnapshot
            {
                MatchPhase = MatchPhase.Playing,
                BoardCells = new List<TilePlacement>(),
                LocalPlayerHand = new List<byte> { 1, Plus, 2, EqualsSign, 3 },
                TilesRemainingInBag = 80
            };

            string answer = coach.RespondAsync("จะเริ่มยังไง", context, default).Result;
            StringAssert.Contains("ช่องกลาง", answer);
            StringAssert.Contains("สคริปต์", answer);
        }

        [Test]
        public void StrategyCoach_SurfacesRejectionReason()
        {
            var coach = new StrategyCoachMode();
            var context = new GameContextSnapshot
            {
                MatchPhase = MatchPhase.Playing,
                BoardCells = new List<TilePlacement> { new() { TileId = 1, X = 7, Y = 7 } },
                LocalPlayerHand = new List<byte> { 1, EqualsSign, 1 },
                LastCommandRejectionReason = "Both sides of '=' are not equal.",
                TilesRemainingInBag = 40
            };

            string answer = coach.RespondAsync("ทำไมไม่ได้", context, default).Result;
            StringAssert.Contains("ไม่ผ่าน", answer);
            StringAssert.Contains("Both sides", answer);
        }
    }
}
