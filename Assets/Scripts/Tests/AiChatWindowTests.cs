using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AMath.AI.Chat;
using AMath.AI.Interfaces;
using AMath.AI.UI;
using AMath.Core.Assistance;
using AMath.Core.Assistance.Context;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AMath.Tests
{
    public sealed class AiChatWindowTests
    {
        [Test]
        public void RuntimeModeButtons_SelectTheirRegisteredModes()
        {
            AiChatWindow window = CreateConfiguredWindow(out GameObject parent, out AiAssistantController controller);
            try
            {
                Button strategyButton = window.transform.Find("Panel/Strategy Mode").GetComponent<Button>();
                strategyButton.onClick.Invoke();
                Assert.AreEqual("strategy_coach", controller.SelectedModeId);

                Button rulesButton = window.transform.Find("Panel/Rules Mode").GetComponent<Button>();
                rulesButton.onClick.Invoke();
                Assert.AreEqual("rule_assistant", controller.SelectedModeId);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void RuntimeSendAndCloseButtons_AreBoundAfterCreation()
        {
            AiChatWindow window = CreateConfiguredWindow(out GameObject parent, out _);
            try
            {
                window.Open();
                var input = window.transform.Find("Panel/Question").GetComponent<InputField>();
                input.text = "What can I do?";
                window.transform.Find("Panel/Send").GetComponent<Button>().onClick.Invoke();

                Text transcript = window.transform.Find("Panel/Transcript").GetComponent<Text>();
                Assert.That(transcript.text, Does.Contain("What can I do?"));
                Assert.That(transcript.text, Does.Contain("answer"));
                Assert.AreEqual(string.Empty, input.text);

                window.transform.Find("Panel/Close").GetComponent<Button>().onClick.Invoke();
                Assert.IsFalse(window.gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        private static AiChatWindow CreateConfiguredWindow(
            out GameObject parent,
            out AiAssistantController controller)
        {
            parent = new GameObject("Chat Parent");
            AiChatWindow window = AiChatWindow.CreateRuntime(parent.transform);
            controller = new AiAssistantController(
                new IAiAssistantMode[]
                {
                    new FakeMode("rule_assistant"),
                    new FakeMode("strategy_coach")
                },
                new FakeContextProvider(),
                new AllowAllRestrictionGuard(),
                window,
                new FakeTextProvider());
            window.Configure(controller);
            return window;
        }

        private sealed class FakeMode : IAiAssistantMode
        {
            public FakeMode(string modeId) => ModeId = modeId;

            public string ModeId { get; }

            public Task<string> RespondAsync(string question, GameContextSnapshot context, CancellationToken cancellationToken) =>
                Task.FromResult("answer");
        }

        private sealed class FakeContextProvider : IGameContextProvider
        {
            public GameContextSnapshot Capture() => new GameContextSnapshot();
        }

        private sealed class AllowAllRestrictionGuard : IAiRestrictionGuard
        {
            public bool TryFilter(string rawResponse, out string safeResponse)
            {
                safeResponse = rawResponse;
                return true;
            }
        }

        private sealed class FakeTextProvider : ILocalizedTextProvider
        {
            public string GetText(string key) => key;
        }
    }
}
