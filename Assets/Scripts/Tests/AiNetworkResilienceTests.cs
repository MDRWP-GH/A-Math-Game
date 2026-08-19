using AMath.AI.Chat;
using NUnit.Framework;
using UnityEngine.Networking;

namespace AMath.Tests
{
    public sealed class AiNetworkResilienceTests
    {
        [TestCase(0, UnityWebRequest.Result.ConnectionError, true)]
        [TestCase(408, UnityWebRequest.Result.ProtocolError, true)]
        [TestCase(429, UnityWebRequest.Result.ProtocolError, true)]
        [TestCase(503, UnityWebRequest.Result.ProtocolError, true)]
        [TestCase(400, UnityWebRequest.Result.ProtocolError, false)]
        [TestCase(401, UnityWebRequest.Result.ProtocolError, false)]
        public void IsTransientFailure_RetriesOnlyRecoverableFailures(
            long statusCode,
            UnityWebRequest.Result result,
            bool expected)
        {
            Assert.AreEqual(expected, OpenAiCompatibleClient.IsTransientFailure(statusCode, result));
        }

        [Test]
        public void CalculateRetryDelaySeconds_GrowsAndIsCapped()
        {
            float first = OpenAiCompatibleClient.CalculateRetryDelaySeconds(0, 1f, 0.5f);
            float second = OpenAiCompatibleClient.CalculateRetryDelaySeconds(1, 1f, 0.5f);
            float capped = OpenAiCompatibleClient.CalculateRetryDelaySeconds(10, 10f, 0.5f);

            Assert.That(second, Is.GreaterThan(first));
            Assert.That(capped, Is.LessThanOrEqualTo(15f));
        }

        [Test]
        public void TryExtractAssistantContent_ParsesEscapedContent()
        {
            const string response = "{\"choices\":[{\"message\":{\"content\":\"line 1\\nline 2\"}}]}";

            Assert.AreEqual("line 1\nline 2", OpenAiCompatibleClient.TryExtractAssistantContent(response));
        }
    }
}
