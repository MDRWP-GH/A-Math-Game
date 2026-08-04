namespace AMath.Core.Events
{
    // ------------------------------------------------------------------
    // Published by AMath.Tutorial, declared here in AMath.Core.
    //
    // AMath.Core has zero references (see AMath.Core.asmdef) and must stay
    // that way, so it cannot depend on AMath.Tutorial to get these struct
    // definitions. AMath.Tutorial already references AMath.Core, so the
    // struct lives on this side and Tutorial publishes it through the
    // shared IEventBus. This lets a Core-resident
    // ITutorialProgressReader implementation react to tutorial progress
    // without Core ever knowing AMath.Tutorial exists — the dependency
    // arrow only ever points one way: Tutorial -> Core.
    // ------------------------------------------------------------------

    /// <summary>The active tutorial advanced to a new step (or started/ended).</summary>
    public struct TutorialStepChangedEvent
    {
        /// <summary>Id of the active tutorial sequence, or null when the tutorial ended.</summary>
        public string TutorialId;

        /// <summary>0-based index of the new current step.</summary>
        public int StepIndex;

        /// <summary>Total number of steps in the active tutorial.</summary>
        public int TotalSteps;

        /// <summary>Localized objective text for the new current step.</summary>
        public string ObjectiveText;

        /// <summary>True while a tutorial is active; false when it just ended/was skipped.</summary>
        public bool IsActive;
    }

    /// <summary>
    /// The scripted hint sequence for the current step reached the point
    /// configured to reveal the "Ask AI" affordance. Tutorial UI reacts to
    /// this to enable the button; it never calls into the AI module itself.
    /// </summary>
    public struct AiButtonUnlockedEvent
    {
        /// <summary>Id of the tutorial step that unlocked the button.</summary>
        public string StepId;
    }
}
