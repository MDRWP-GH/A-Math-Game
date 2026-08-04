using System;
using AMath.Tutorial.Actions;
using AMath.Tutorial.Interfaces;

namespace AMath.Tutorial.Highlights
{
    /// <summary>
    /// Shows one named highlight target. It only delegates to the injected
    /// presentation service; target resolution and visuals stay in Tutorial
    /// UI so this action is reusable in any scene.
    /// </summary>
    public sealed class TutorialHighlight : TutorialAction
    {
        private readonly string _targetId;

        /// <summary>Creates a highlight action for a stable target id.</summary>
        public TutorialHighlight(string targetId)
        {
            _targetId = string.IsNullOrWhiteSpace(targetId)
                ? throw new ArgumentException("A highlight target id is required.", nameof(targetId))
                : targetId;
        }

        /// <inheritdoc />
        public override void Execute(ITutorialRuntimeContext context, Action onComplete)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Highlighter.Highlight(_targetId);
            onComplete?.Invoke();
        }
    }
}
