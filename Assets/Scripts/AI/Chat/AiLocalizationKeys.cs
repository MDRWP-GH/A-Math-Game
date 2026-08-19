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

        /// <summary>Chat window title.</summary>
        public const string WindowTitle = "ai.window.title";

        /// <summary>Rule Assistant mode button.</summary>
        public const string ModeRules = "ai.window.mode_rules";

        /// <summary>Strategy Coach mode button.</summary>
        public const string ModeStrategy = "ai.window.mode_strategy";

        /// <summary>Submit button.</summary>
        public const string Send = "ai.window.send";

        /// <summary>Close button.</summary>
        public const string Close = "ai.window.close";

        /// <summary>Shown while a request is in flight.</summary>
        public const string Busy = "ai.window.busy";

        /// <summary>Placeholder inside the question field.</summary>
        public const string QuestionPlaceholder = "ai.window.placeholder";
    }
}
