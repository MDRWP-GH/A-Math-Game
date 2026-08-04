using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AMath.AI.Interfaces;
using UnityEngine;
using UnityEngine.Networking;

namespace AMath.AI.Chat
{
    /// <summary>
    /// Real HTTP transport for an OpenAI-compatible backend proxy. The proxy
    /// owns the provider credential; this client sends only a runtime
    /// application session token and never stores a provider API key.
    /// </summary>
    public sealed class OpenAiCompatibleClient : IAiClient
    {
        private readonly AiBackendConfig _config;
        private readonly IAccessTokenProvider _tokenProvider;

        /// <summary>Creates the backend client.</summary>
        public OpenAiCompatibleClient(
            AiBackendConfig config,
            IAccessTokenProvider tokenProvider)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        }

        /// <inheritdoc />
        public async Task<string> SendAsync(string prompt, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("An AI prompt is required.", nameof(prompt));
            if (!_config.IsValid(out string configError))
                throw new InvalidOperationException(configError);

            string token = _tokenProvider.GetAccessToken();
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("No AI backend session token is available.");

            var body = new ChatCompletionRequest
            {
                model = _config.Model,
                max_tokens = _config.MaxTokens,
                temperature = _config.Temperature,
                messages = new List<ChatMessage>
                {
                    new() { role = "user", content = prompt }
                }
            };

            string json = JsonUtility.ToJson(body);
            using var request = new UnityWebRequest(_config.Endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = _config.TimeoutSeconds
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {token}");

            await SendRequestAsync(request, cancellationToken);
            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new AiBackendException(
                    request.responseCode,
                    string.IsNullOrWhiteSpace(request.error) ? "AI backend request failed." : request.error);
            }

            ChatCompletionResponse response = JsonUtility.FromJson<ChatCompletionResponse>(
                request.downloadHandler.text);
            string content = response?.choices != null && response.choices.Count > 0
                ? response.choices[0]?.message?.content
                : null;

            if (string.IsNullOrWhiteSpace(content))
                throw new AiBackendException(request.responseCode, "AI backend returned no answer.");

            return content.Trim();
        }

        private static Task SendRequestAsync(
            UnityWebRequest request,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<bool>();
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            CancellationTokenRegistration registration = cancellationToken.Register(request.Abort);

            operation.completed += _ =>
            {
                registration.Dispose();
                if (cancellationToken.IsCancellationRequested)
                    completion.TrySetCanceled(cancellationToken);
                else
                    completion.TrySetResult(true);
            };

            return completion.Task;
        }

        [Serializable]
        private sealed class ChatCompletionRequest
        {
            public string model;
            public List<ChatMessage> messages;
            public int max_tokens;
            public float temperature;
        }

        [Serializable]
        private sealed class ChatMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
        private sealed class ChatCompletionResponse
        {
            public List<ChatChoice> choices;
        }

        [Serializable]
        private sealed class ChatChoice
        {
            public ChatMessage message;
        }
    }

    /// <summary>Failure returned by the configured AI backend proxy.</summary>
    public sealed class AiBackendException : Exception
    {
        /// <summary>HTTP response status, or zero when no response was received.</summary>
        public long StatusCode { get; }

        /// <summary>Creates a backend exception without exposing response bodies or credentials.</summary>
        public AiBackendException(long statusCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
