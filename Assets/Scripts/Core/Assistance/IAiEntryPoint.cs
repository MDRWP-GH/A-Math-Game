namespace AMath.Core.Assistance
{
    /// <summary>
    /// The only door Tutorial UI is allowed to know about the AI Assistant
    /// through. Declared in Core (not in <c>AMath.AI</c>) so
    /// <c>AMath.Tutorial</c> never references the AI assembly: the
    /// composition root injects the real implementation when the AI module
    /// is present, or leaves the slot null when it is not. Either way,
    /// Tutorial code is identical and keeps working.
    /// </summary>
    public interface IAiEntryPoint
    {
        /// <summary>True once the AI Assistant is allowed to be offered (e.g. scripted hints exhausted, if configured).</summary>
        bool IsAvailable { get; }

        /// <summary>Opens the AI chat window. Never called by Tutorial logic itself — only by a user-driven UI action.</summary>
        void OpenChat();
    }
}
