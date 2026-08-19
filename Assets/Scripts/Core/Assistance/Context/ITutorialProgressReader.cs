namespace AMath.Core.Assistance.Context
{
    /// <summary>
    /// Read-only view of the Script Tutorial's current position, for the AI
    /// context ("Current Tutorial Step" / "Current Objective"). The concrete
    /// implementation lives in <c>AMath.Core</c> and is updated by subscribing
    /// to a Tutorial-published event (see <c>Core/Events/TutorialBridgeEvents.cs</c>)
    /// — this interface itself has no dependency on <c>AMath.Tutorial</c>.
    /// </summary>
    public interface ITutorialProgressReader
    {
        /// <summary>True while a scripted tutorial is running.</summary>
        bool IsTutorialActive { get; }

        /// <summary>Id of the active tutorial sequence, or null when none is active.</summary>
        string ActiveTutorialId { get; }

        /// <summary>Authored id of the current step, or null when none is active.</summary>
        string CurrentStepId { get; }

        /// <summary>0-based index of the current step within the active tutorial.</summary>
        int CurrentStepIndex { get; }

        /// <summary>Total number of steps in the active tutorial.</summary>
        int TotalStepCount { get; }

        /// <summary>Localized objective text for the current step, or null when none is active.</summary>
        string CurrentObjectiveText { get; }
    }
}
