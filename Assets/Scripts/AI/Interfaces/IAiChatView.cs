namespace AMath.AI.Interfaces
{
    /// <summary>
    /// UI contract for the AI Chat Window. Keeps the controller that talks
    /// to <see cref="IAiClient"/>/<see cref="IAiAssistantMode"/> ignorant of
    /// any concrete MonoBehaviour/UGUI type, so it can be unit-tested with a
    /// fake view and the real view can be reskinned freely.
    /// </summary>
    public interface IAiChatView
    {
        /// <summary>Opens/focuses the chat window.</summary>
        void Open();

        /// <summary>Closes the chat window without affecting gameplay or tutorial state.</summary>
        void Close();

        /// <summary>Appends a message the player typed/selected.</summary>
        void ShowUserMessage(string text);

        /// <summary>Appends a message from the AI Assistant.</summary>
        void ShowAssistantMessage(string text);

        /// <summary>Appends an error/fallback message (e.g. network failure).</summary>
        void ShowError(string text);

        /// <summary>Toggles a waiting-for-response indicator.</summary>
        void SetBusy(bool isBusy);
    }
}
