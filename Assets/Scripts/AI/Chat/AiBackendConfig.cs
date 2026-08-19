using UnityEngine;

namespace AMath.AI.Chat
{
    /// <summary>
    /// Non-secret configuration for an OpenAI-compatible backend proxy.
    /// The endpoint should accept the standard chat-completions request and
    /// return the standard <c>choices[].message.content</c> response shape.
    /// Authentication is injected separately at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "AiBackendConfig", menuName = "A-Math/AI/Backend Configuration")]
    public sealed class AiBackendConfig : ScriptableObject
    {
        [SerializeField] private string _endpoint;
        [SerializeField] private string _model;
        [SerializeField, Range(1, 120)] private int _timeoutSeconds = 30;
        [SerializeField, Range(64, 4096)] private int _maxTokens = 700;
        [SerializeField, Range(0f, 2f)] private float _temperature = 0.3f;
        [SerializeField, Range(0, 5)] private int _maxRetryAttempts = 2;
        [SerializeField, Range(0.1f, 10f)] private float _retryBaseDelaySeconds = 0.75f;

        /// <summary>Full HTTPS chat-completions URL exposed by the backend proxy.</summary>
        public string Endpoint => _endpoint;

        /// <summary>Backend model/deployment identifier.</summary>
        public string Model => _model;

        /// <summary>Request timeout in seconds.</summary>
        public int TimeoutSeconds => _timeoutSeconds;

        /// <summary>Maximum response tokens requested from the backend.</summary>
        public int MaxTokens => _maxTokens;

        /// <summary>Sampling temperature requested from the backend.</summary>
        public float Temperature => _temperature;

        /// <summary>Maximum retries after a transient backend failure.</summary>
        public int MaxRetryAttempts => _maxRetryAttempts;

        /// <summary>Initial delay for exponential retry backoff.</summary>
        public float RetryBaseDelaySeconds => _retryBaseDelaySeconds;

        /// <summary>Checks required non-secret configuration before a request is attempted.</summary>
        public bool IsValid(out string reason)
        {
            if (!System.Uri.TryCreate(_endpoint, System.UriKind.Absolute, out System.Uri endpoint) ||
                endpoint.Scheme != System.Uri.UriSchemeHttps)
            {
                reason = "AI backend endpoint must be an absolute HTTPS URL.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_model))
            {
                reason = "AI backend model is not configured.";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
