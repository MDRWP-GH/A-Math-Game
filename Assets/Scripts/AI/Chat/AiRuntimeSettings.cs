using System;

namespace AMath.AI.Chat
{
    /// <summary>
    /// Backend settings that belong to a deployment rather than to the
    /// project. The proxy URL, the model name and the session token differ per
    /// environment and the token is a credential, so none of them are stored
    /// in a committed asset — they are read from the process environment at
    /// startup instead.
    ///
    /// Set these before launching the game (or the Unity Editor) to enable the
    /// Rule Assistant:
    /// <code>
    /// AMATH_AI_ENDPOINT=https://your-proxy.example.com/v1/chat/completions
    /// AMATH_AI_MODEL=gpt-4o-mini
    /// AMATH_AI_TOKEN=&lt;short-lived application session token&gt;
    /// </code>
    /// When they are absent the Rule Assistant simply stays unavailable and
    /// the offline Strategy Coach continues to work.
    /// </summary>
    public static class AiRuntimeSettings
    {
        /// <summary>Environment variable holding the chat-completions URL.</summary>
        public const string EndpointVariable = "AMATH_AI_ENDPOINT";

        /// <summary>Environment variable holding the model/deployment id.</summary>
        public const string ModelVariable = "AMATH_AI_MODEL";

        /// <summary>Environment variable holding the runtime session token.</summary>
        public const string TokenVariable = "AMATH_AI_TOKEN";

        /// <summary>Configured endpoint, or null when unset.</summary>
        public static string Endpoint => Read(EndpointVariable);

        /// <summary>Configured model, or null when unset.</summary>
        public static string Model => Read(ModelVariable);

        /// <summary>Configured session token, or null when unset.</summary>
        public static string AccessToken => Read(TokenVariable);

        private static string Read(string variable)
        {
            try
            {
                string value = Environment.GetEnvironmentVariable(variable);
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
            catch (Exception)
            {
                // Environment access is denied on some player platforms; an
                // unconfigured backend is a supported state, not an error.
                return null;
            }
        }
    }
}
