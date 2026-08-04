using System;

namespace AMath.Tutorial.Interfaces
{
    /// <summary>
    /// A single, self-contained effect a tutorial step performs: showing a
    /// highlight, playing a dialogue line, moving the camera, and so on.
    /// Actions never touch gameplay — they only drive presentation services
    /// exposed through <see cref="ITutorialRuntimeContext"/>.
    /// </summary>
    public interface ITutorialAction
    {
        /// <summary>
        /// Runs the action. Instant actions call <paramref name="onComplete"/>
        /// synchronously; actions with a duration (camera pans, voiced
        /// dialogue) call it once that duration/animation/audio finishes.
        /// The owning step does not advance until every one of its actions
        /// has completed.
        /// </summary>
        void Execute(ITutorialRuntimeContext context, Action onComplete);
    }
}
