using System;
using AMath.Tutorial.Interfaces;

namespace AMath.Tutorial.Actions
{
    /// <summary>
    /// Presentation-only base class for authored tutorial actions. Derived
    /// actions receive only <see cref="ITutorialRuntimeContext"/>, whose
    /// services expose no gameplay mutation APIs.
    /// </summary>
    public abstract class TutorialAction : ITutorialAction
    {
        /// <inheritdoc />
        public abstract void Execute(ITutorialRuntimeContext context, Action onComplete);
    }
}
