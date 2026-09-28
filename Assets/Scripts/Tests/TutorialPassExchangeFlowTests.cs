using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Assistance;
using AMath.Core.Commands;
using AMath.Core.Events;
using AMath.Tutorial.Definitions;
using AMath.Tutorial.Scripted;
using NUnit.Framework;

namespace AMath.Tests
{
    public sealed class TutorialPassExchangeFlowTests
    {
        private sealed class KeyText : ILocalizedTextProvider
        {
            public string GetText(string key) => key;
        }

        [Test]
        public void PremiumPassStep_ResolvesOnePassAndAdvancesTurn()
        {
            var bus = new EventBus();
            ScriptedTutorialMatchScript script = ScriptedTutorialMatchScript.PremiumSkills();
            using var host = new TutorialMatchHost(bus, new KeyText(), script);
            var resolved = new List<TurnResolvedEvent>();
            var rejected = new List<string>();
            bus.Subscribe<TurnResolvedEvent>(evt => resolved.Add(evt));
            bus.Subscribe<CommandRejectedEvent>(evt => rejected.Add(evt.Reason));

            bus.Publish(new TutorialStepChangedEvent
            {
                TutorialId = PremiumTutorialSequence.PremiumTutorialId,
                StepId = PremiumTutorialSequence.PassStepId,
                StepIndex = 12,
                TotalSteps = 16,
                IsActive = true
            });

            Assert.IsTrue(host.IsPassActionEnabled);
            Assert.AreEqual(1, host.Turns.TurnNumber);
            CollectionAssert.AreEqual(script.PassLessonRack, host.Players.GetById(0).Rack);

            host.Input.RequestPass();

            Assert.IsEmpty(rejected);
            Assert.AreEqual(1, resolved.Count);
            Assert.AreEqual((byte)CommandType.PassTurn, resolved[0].Record.CommandType);
            Assert.AreEqual(2, host.Turns.TurnNumber);
            Assert.AreEqual(1, host.Turns.CurrentPlayerId);
            CollectionAssert.AreEqual(script.PassLessonRack, host.Players.GetById(0).Rack);
        }

        [Test]
        public void PremiumExchangeStep_ExchangesGuidedTilesAndAdvancesTurn()
        {
            var bus = new EventBus();
            ScriptedTutorialMatchScript script = ScriptedTutorialMatchScript.PremiumSkills();
            using var host = new TutorialMatchHost(bus, new KeyText(), script);
            var resolved = new List<TurnResolvedEvent>();
            var rejected = new List<string>();
            bus.Subscribe<TurnResolvedEvent>(evt => resolved.Add(evt));
            bus.Subscribe<CommandRejectedEvent>(evt => rejected.Add(evt.Reason));

            bus.Publish(new TutorialStepChangedEvent
            {
                TutorialId = PremiumTutorialSequence.PremiumTutorialId,
                StepId = PremiumTutorialSequence.ExchangeStepId,
                StepIndex = 14,
                TotalSteps = 16,
                IsActive = true
            });

            Assert.IsTrue(host.IsExchangeActionEnabled);
            Assert.AreEqual(1, host.Turns.TurnNumber);
            CollectionAssert.AreEqual(script.ExchangeLessonRack, host.Players.GetById(0).Rack);
            int bagCount = host.Game.BagCount;

            Assert.IsTrue(host.Input.TryRequestExchange(host.GuidedExchangeIndices, out string error), error);

            Assert.IsEmpty(rejected);
            Assert.AreEqual(1, resolved.Count);
            Assert.AreEqual((byte)CommandType.ExchangeTiles, resolved[0].Record.CommandType);
            Assert.AreEqual(2, host.Turns.TurnNumber);
            Assert.AreEqual(1, host.Turns.CurrentPlayerId);
            Assert.AreEqual(GameRules.RackSize, host.Players.GetById(0).Rack.Count);
            Assert.AreEqual(bagCount, host.Game.BagCount);
        }
    }
}
