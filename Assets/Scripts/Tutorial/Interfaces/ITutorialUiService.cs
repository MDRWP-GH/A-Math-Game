namespace AMath.Tutorial.Interfaces
{
    /// <summary>
    /// Drives the tutorial's own HUD: objective text, step progress and the
    /// scripted hint display. Deliberately does not expose anything about
    /// the AI chat window itself — that lives behind <c>IAiEntryPoint</c>
    /// (Core), which this service's owner may hold separately.
    /// </summary>
    public interface ITutorialUiService
    {
        /// <summary>Sets the current objective line shown to the player.</summary>
        void SetObjective(string objectiveText);

        /// <summary>Updates the step progress indicator.</summary>
        void SetProgress(int currentStep, int totalSteps);

        /// <summary>Shows a scripted hint (see <see cref="TutorialHintDefinition"/>).</summary>
        void ShowHint(string hintText);

        /// <summary>Clears any currently shown hint.</summary>
        void ClearHint();

        /// <summary>Enables/disables the "Ask AI" affordance. Has no effect if no <c>IAiEntryPoint</c> was ever injected.</summary>
        void SetAiButtonAvailable(bool available);
    }
}
