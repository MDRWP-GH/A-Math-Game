namespace AMath.Tutorial.Interfaces
{
    /// <summary>Presents one dialogue line and waits for the player to dismiss it.</summary>
    public interface ITutorialDialogueService
    {
        /// <summary>
        /// Shows <paramref name="localizedText"/>. Calls <paramref name="onFinished"/>
        /// when the player dismisses the line (Continue or skip).
        /// </summary>
        void Play(string localizedText, System.Action onFinished);

        /// <summary>Cuts the current line short (skip button) and immediately raises its onFinished.</summary>
        void Skip();
    }
}
