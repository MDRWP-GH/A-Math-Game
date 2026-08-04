namespace AMath.Tutorial.Interfaces
{
    /// <summary>
    /// One scripted hint entry: "after this many seconds of inactivity on
    /// this step, show this text." Pure data — authored as part of an
    /// <see cref="ITutorialStepDefinition"/>, interpreted later by the
    /// concrete <c>TutorialHint</c> class (Hints/).
    /// </summary>
    public readonly struct TutorialHintDefinition
    {
        /// <summary>Seconds of inactivity on the step before this hint appears.</summary>
        public readonly float DelaySeconds;

        /// <summary>Localization key for the hint text.</summary>
        public readonly string HintTextKey;

        public TutorialHintDefinition(float delaySeconds, string hintTextKey)
        {
            DelaySeconds = delaySeconds;
            HintTextKey = hintTextKey;
        }
    }
}
