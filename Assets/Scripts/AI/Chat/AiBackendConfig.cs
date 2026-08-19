using System;
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

        // Runtime overrides are deliberately not serialized: the endpoint and
        // model are per-deployment values, so writing them into the asset would
        // both dirty it in the Editor and ship one environment's settings to
        // every build.
        [NonSerialized] private string _runtimeEndpoint;
        [NonSerialized] private string _runtimeModel;

        /// <summary>Full HTTPS chat-completions URL exposed by the backend proxy.</summary>
        public string Endpoint => string.IsNullOrWhiteSpace(_runtimeEndpoint) ? _endpoint : _runtimeEndpoint;

        /// <summary>Backend model/deployment identifier.</summary>
        public string Model => string.IsNullOrWhiteSpace(_runtimeModel) ? _model : _runtimeModel;

        /// <summary>
        /// Applies per-deployment values supplied at startup. Blank arguments
        /// leave the asset's own values in place.
        /// </summary>
        public void ApplyRuntimeOverrides(string endpoint, string model)
        {
            if (!string.IsNullOrWhiteSpace(endpoint))
                _runtimeEndpoint = endpoint.Trim();
            if (!string.IsNullOrWhiteSpace(model))
                _runtimeModel = model.Trim();
        }

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
            if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out Uri endpoint) ||
                endpoint.Scheme != Uri.UriSchemeHttps)
            {
                reason = "AI backend endpoint must be an absolute HTTPS URL.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Model))
            {
                reason = "AI backend model is not configured.";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
