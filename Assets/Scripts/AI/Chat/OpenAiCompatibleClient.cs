using System;
using System.Globalization;
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

            string json = BuildChatCompletionRequestJson(_config.Model, _config.MaxTokens, _config.Temperature, prompt);
            AiBackendException lastFailure = null;
            for (int attempt = 0; attempt <= _config.MaxRetryAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BackendResponse response = await SendOnceAsync(json, token, cancellationToken);
                if (response.IsSuccess)
                {
                    string content = TryExtractAssistantContent(response.Body);
                    if (string.IsNullOrWhiteSpace(content))
                        throw new AiBackendException(response.StatusCode, "AI backend returned no answer.");

                    return content.Trim();
                }

                lastFailure = new AiBackendException(response.StatusCode, response.Error);
                if (!IsTransientFailure(response.StatusCode, response.Result)
                    || attempt == _config.MaxRetryAttempts)
                {
                    throw lastFailure;
                }

                float delay = CalculateRetryDelaySeconds(
                    attempt,
                    _config.RetryBaseDelaySeconds,
                    UnityEngine.Random.value);
                Debug.LogWarning(
                    $"[AI] Transient backend failure ({response.StatusCode}); retrying in {delay:0.##} seconds.");
                await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
            }

            throw lastFailure ?? new AiBackendException(0, "AI backend request failed.");
        }

        /// <summary>Whether a failed request is safe to retry without changing semantics.</summary>
        public static bool IsTransientFailure(long statusCode, UnityWebRequest.Result result)
        {
            return result == UnityWebRequest.Result.ConnectionError
                || result == UnityWebRequest.Result.DataProcessingError
                || statusCode == 0
                || statusCode == 408
                || statusCode == 425
                || statusCode == 429
                || statusCode >= 500;
        }

        /// <summary>Calculates capped exponential backoff with jitter for a retry attempt.</summary>
        public static float CalculateRetryDelaySeconds(int attempt, float baseDelaySeconds, float jitter)
        {
            float exponentialDelay = baseDelaySeconds * (1 << Math.Min(attempt, 4));
            return Mathf.Min(15f, exponentialDelay) * Mathf.Lerp(0.75f, 1.25f, Mathf.Clamp01(jitter));
        }

        internal static string BuildChatCompletionRequestJson(
            string model,
            int maxTokens,
            float temperature,
            string prompt)
        {
            var builder = new StringBuilder(256 + prompt.Length);
            builder.Append("{\"model\":").Append(EscapeJsonString(model));
            builder.Append(",\"max_tokens\":").Append(maxTokens);
            builder.Append(",\"temperature\":")
                .Append(temperature.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"messages\":[{\"role\":\"user\",\"content\":")
                .Append(EscapeJsonString(prompt))
                .Append("}]}");
            return builder.ToString();
        }

        public static string TryExtractAssistantContent(string responseJson)
        {
            if (string.IsNullOrWhiteSpace(responseJson))
                return null;

            const string contentKey = "\"content\":";
            int searchFrom = 0;
            while (true)
            {
                int keyIndex = responseJson.IndexOf(contentKey, searchFrom, StringComparison.Ordinal);
                if (keyIndex < 0)
                    return null;

                int valueStart = keyIndex + contentKey.Length;
                while (valueStart < responseJson.Length && char.IsWhiteSpace(responseJson[valueStart]))
                    valueStart++;

                if (valueStart >= responseJson.Length)
                    return null;

                if (responseJson[valueStart] == '"')
                    return ReadJsonString(responseJson, valueStart + 1, out _);

                searchFrom = valueStart + 1;
            }
        }

        private static string ReadJsonString(string json, int start, out int end)
        {
            var builder = new StringBuilder();
            end = start;
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '\\')
                {
                    if (i + 1 >= json.Length)
                        break;

                    char escape = json[++i];
                    switch (escape)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u' when i + 4 < json.Length:
                            if (ushort.TryParse(json.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort codePoint))
                                builder.Append((char)codePoint);
                            i += 4;
                            break;
                        default:
                            builder.Append(escape);
                            break;
                    }

                    end = i + 1;
                    continue;
                }

                if (c == '"')
                {
                    end = i + 1;
                    return builder.ToString();
                }

                builder.Append(c);
                end = i + 1;
            }

            return builder.Length > 0 ? builder.ToString() : null;
        }

        private static string EscapeJsonString(string value)
        {
            if (value == null)
                return "null";

            var builder = new StringBuilder(value.Length + 8);
            builder.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            builder.Append(c);
                        break;
                }
            }

            builder.Append('"');
            return builder.ToString();
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

        private async Task<BackendResponse> SendOnceAsync(
            string json,
            string token,
            CancellationToken cancellationToken)
        {
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
            return new BackendResponse(
                request.result,
                request.responseCode,
                request.downloadHandler?.text,
                string.IsNullOrWhiteSpace(request.error) ? "AI backend request failed." : request.error);
        }

        private readonly struct BackendResponse
        {
            public UnityWebRequest.Result Result { get; }
            public long StatusCode { get; }
            public string Body { get; }
            public string Error { get; }
            public bool IsSuccess => Result == UnityWebRequest.Result.Success;

            public BackendResponse(UnityWebRequest.Result result, long statusCode, string body, string error)
            {
                Result = result;
                StatusCode = statusCode;
                Body = body;
                Error = error;
            }
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
