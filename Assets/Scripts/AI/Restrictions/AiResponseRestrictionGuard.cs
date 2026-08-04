using System;
using AMath.AI.Interfaces;

namespace AMath.AI.Restrictions
{
    /// <summary>
    /// Rejects model output that contains common tool/function-call protocol
    /// markers and caps response size before display. This is intentionally
    /// a final safety net; the primary restriction remains structural—the AI
    /// receives no gameplay write interface and no hidden-information fields.
    /// </summary>
    public sealed class AiResponseRestrictionGuard : IAiRestrictionGuard
    {
        private static readonly string[] BlockedMarkers =
        {
            "<tool_call",
            "\"function_call\"",
            "\"tool_calls\"",
            "\"command_payload\""
        };

        private readonly int _maximumCharacters;

        /// <summary>Creates a guard with a display-size limit.</summary>
        public AiResponseRestrictionGuard(int maximumCharacters = 4000)
        {
            if (maximumCharacters <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
            _maximumCharacters = maximumCharacters;
        }

        /// <inheritdoc />
        public bool TryFilter(string rawResponse, out string safeResponse)
        {
            safeResponse = null;
            if (string.IsNullOrWhiteSpace(rawResponse))
                return false;

            foreach (string marker in BlockedMarkers)
            {
                if (rawResponse.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }

            string trimmed = rawResponse.Trim();
            safeResponse = trimmed.Length <= _maximumCharacters
                ? trimmed
                : trimmed.Substring(0, _maximumCharacters);
            return true;
        }
    }
}
