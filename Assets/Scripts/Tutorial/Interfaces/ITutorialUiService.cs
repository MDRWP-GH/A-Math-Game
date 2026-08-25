namespace AMath.Tutorial.Interfaces
{
    /// <summary>
    /// Drives the tutorial's own HUD: objective text, step progress and the
    /// scripted hint display.
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
    }
}
