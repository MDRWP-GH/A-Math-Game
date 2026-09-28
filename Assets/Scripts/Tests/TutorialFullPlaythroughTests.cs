using System;
using System.Collections.Generic;
using AMath.Core.Assistance;
using AMath.Core.Events;
using AMath.Tutorial;
using AMath.Tutorial.Bootstrap;
using AMath.Tutorial.Definitions;
using AMath.Tutorial.Interfaces;
using AMath.Tutorial.Save;
using AMath.Tutorial.Scripted;
using AMath.Tutorial.StateMachine;
using NUnit.Framework;

namespace AMath.Tests
{
    /// <summary>Exercises the authored steps against the real tutorial match and input session.</summary>
    public sealed class TutorialFullPlaythroughTests
    {
        private sealed class KeyText : ILocalizedTextProvider
        {
            public string GetText(string key) => key ?? string.Empty;
        }

        private sealed class MemorySave : ITutorialSaveStore
        {
            private readonly Dictionary<string, TutorialProgressData> _values = new();
            public bool TryLoad(string id, out TutorialProgressData data) => _values.TryGetValue(id, out data);
            public void Save(string id, TutorialProgressData data) => _values[id] = data;
        }

        private sealed class Dialogue : ITutorialDialogueService
        {
            private Action _finished;
            public void Play(string text, Action onFinished) => _finished = onFinished;
            public void Skip()
            {
                Action finished = _finished;
                _finished = null;
                finished?.Invoke();
            }
        }

        private sealed class Ui : ITutorialUiService
        {
            public void SetMilestone(string text) { }
            public void SetObjective(string text) { }
            public void SetProgress(int current, int total) { }
            public void ShowHint(string text) { }
            public void ClearHint() { }
        }

        private sealed class Highlighter : ITutorialHighlightService
        {
            public void Highlight(string id) { }
            public void ClearHighlight() { }
        }

        [TestCase("intro")]
        [TestCase("connect")]
        [TestCase("premium")]
        public void Chapter_AllAuthoredStepsCanBePlayedThrough(string chapter)
            => RunChapter(chapter, 0, false);

        [TestCase("intro")]
        [TestCase("connect")]
        [TestCase("premium")]
        public void Chapter_CanResumeAndFinishFromEverySavedStep(string chapter)
        {
            ITutorialSequenceDefinition sequence = SequenceFor(chapter, new KeyText());
            for (int startIndex = 1; startIndex < sequence.Steps.Count; startIndex++)
                RunChapter(chapter, startIndex, true);
        }

        [TestCase("intro")]
        [TestCase("connect")]
        [TestCase("premium")]
        public void Chapter_CanRestartAndReplayEveryStep(string chapter)
            => RunChapter(chapter, 0, false, replayEveryStep: true);

        private static void RunChapter(string chapter, int startIndex, bool resume, bool replayEveryStep = false)
        {
            var text = new KeyText();
            var bus = new EventBus();
            var save = new MemorySave();
            var dialogue = new Dialogue();
            var rejected = new List<string>();
            bus.Subscribe<CommandRejectedEvent>(evt => rejected.Add(evt.Reason));
            ScriptedTutorialMatchScript script = ScriptFor(chapter);
            using var host = new TutorialMatchHost(bus, text, script);
            var context = new TutorialRuntimeContext(
                bus, host.Readers, host.Readers, host.Readers,
                new Highlighter(), dialogue, new Ui());
            using var manager = new TutorialManager(bus, context, text, save);
            ITutorialSequenceDefinition sequence = SequenceFor(chapter, text);
            if (resume)
            {
                save.Save(sequence.TutorialId, new TutorialProgressData
                {
                    TutorialId = sequence.TutorialId,
                    StepIndex = startIndex,
                    IsCompleted = false
                });
            }
            manager.Start(sequence, resumeProgress: resume);
            if (replayEveryStep)
            {
                manager.Restart();
                Assert.AreEqual(0, manager.CurrentStepIndex, chapter + " restart");
            }

            for (int expected = startIndex; expected < sequence.Steps.Count; expected++)
            {
                Assert.AreEqual(TutorialPhase.Running, manager.Phase, chapter);
                Assert.AreEqual(expected, manager.CurrentStepIndex, chapter);
                if (replayEveryStep)
                {
                    manager.ReplayCurrentStep();
                    Assert.AreEqual(expected, manager.CurrentStepIndex, chapter + " replay");
                }
                string step = sequence.Steps[expected].StepId;
                if (TutorialStepRouting.TryGetSelectTile(step, out byte tile))
                {
                    Assert.IsTrue(host.IsPlayerInputEnabled, step);
                    SelectTile(host, tile, step);
                }
                else if (TutorialStepRouting.TryGetPlaceCell(step, out int x, out int y))
                {
                    Assert.IsTrue(host.IsPlayerInputEnabled, step);
                    if (!host.Input.SelectedRackIndex.HasValue)
                    {
                        Assert.IsTrue(host.GuidedRackTileId.HasValue, step);
                        SelectTile(host, host.GuidedRackTileId.Value, step);
                    }
                    Assert.IsTrue(host.Input.TryPlaceOnCell(x, y, out string error), step + ": " + error);
                }
                else if (TutorialStepRouting.IsConfirmStep(step))
                {
                    Assert.IsTrue(host.Input.TryConfirmPlace(out string error), step + ": " + error);
                }
                else if (TutorialStepRouting.IsOpponentStep(step))
                {
                    host.Tick(1f);
                }
                else if (TutorialStepRouting.IsPassStep(step))
                {
                    Assert.IsTrue(host.IsPassActionEnabled, step);
                    host.Input.RequestPass();
                }
                else if (TutorialStepRouting.IsExchangeStep(step))
                {
                    Assert.IsTrue(host.IsExchangeActionEnabled, step);
                    Assert.IsTrue(host.Input.TryRequestExchange(host.GuidedExchangeIndices, out string error),
                        step + ": " + error);
                }
                else
                {
                    dialogue.Skip();
                    bus.Publish(new ButtonPressedEvent { ButtonId = IntroTutorialSequence.ContinueButtonId });
                }

                Assert.IsEmpty(rejected, step + " rejected a command");
                Assert.AreEqual(expected + 1, manager.CurrentStepIndex, step + " did not advance");
            }

            Assert.AreEqual(TutorialPhase.Completed, manager.Phase, chapter);
            Assert.IsTrue(save.TryLoad(sequence.TutorialId, out TutorialProgressData progress));
            Assert.IsTrue(progress.IsCompleted, chapter);
        }

        private static void SelectTile(TutorialMatchHost host, byte tileId, string step)
        {
            var rack = host.Players.GetById(host.Players.LocalPlayerId).Rack;
            for (int i = 0; i < rack.Count; i++)
            {
                if (rack[i] != tileId) continue;
                host.Input.SelectFromRack(i);
                Assert.AreEqual(tileId, host.Input.SelectedTileId, step);
                return;
            }
            Assert.Fail(step + ": guided tile missing from rack");
        }

        private static ScriptedTutorialMatchScript ScriptFor(string chapter) => chapter switch
        {
            "intro" => ScriptedTutorialMatchScript.Intro(),
            "connect" => ScriptedTutorialMatchScript.Connect(),
            "premium" => ScriptedTutorialMatchScript.PremiumSkills(),
            _ => throw new ArgumentOutOfRangeException(nameof(chapter))
        };

        private static ITutorialSequenceDefinition SequenceFor(string chapter, ILocalizedTextProvider text) => chapter switch
        {
            "intro" => new IntroTutorialSequence(text),
            "connect" => new ConnectTutorialSequence(text),
            "premium" => new PremiumTutorialSequence(text),
            _ => throw new ArgumentOutOfRangeException(nameof(chapter))
        };
    }
}
