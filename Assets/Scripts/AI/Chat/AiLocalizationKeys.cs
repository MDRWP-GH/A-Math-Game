namespace AMath.AI.Chat
{
    /// <summary>Stable localization keys used by AI chat failures and validation.</summary>
    public static class AiLocalizationKeys
    {
        /// <summary>No mode is selected/configured.</summary>
        public const string ModeUnavailable = "ai.error.mode_unavailable";

        /// <summary>The question is empty.</summary>
        public const string EmptyQuestion = "ai.error.empty_question";

        /// <summary>The request was rejected by the response safety guard.</summary>
        public const string UnsafeResponse = "ai.error.unsafe_response";

        /// <summary>The backend request failed.</summary>
        public const string RequestFailed = "ai.error.request_failed";

        /// <summary>The current mode cannot answer in this match phase.</summary>
        public const string ModeNotReady = "ai.error.mode_not_ready";
    }
}
