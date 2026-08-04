namespace AMath.Tutorial.StateMachine
{
    /// <summary>
    /// Coarse lifecycle states for a scripted tutorial. Individual authored
    /// steps are data selected by <c>TutorialManager</c>, not enum states.
    /// </summary>
    public enum TutorialPhase
    {
        /// <summary>No tutorial sequence has been started.</summary>
        NotStarted = 0,

        /// <summary>The current step is presenting actions or waiting for its condition.</summary>
        Running = 1,

        /// <summary>Progress is suspended without losing the current step.</summary>
        Paused = 2,

        /// <summary>Every step completed normally.</summary>
        Completed = 3,

        /// <summary>The player explicitly ended the tutorial early.</summary>
        Skipped = 4
    }
}
