using System;
using AMath.Core.Assistance;
using AMath.Tutorial.Actions;
using AMath.Tutorial.Interfaces;

namespace AMath.Tutorial.Dialogue
{
    /// <summary>Shows one localized tutorial line and waits for the player to continue.</summary>
    public sealed class TutorialDialogue : TutorialAction
    {
        private readonly ILocalizedTextProvider _textProvider;
        private readonly string _textKey;
        private readonly bool _waitForDismiss;

        /// <summary>Creates a dialogue action from a localization key.</summary>
        public TutorialDialogue(
            ILocalizedTextProvider textProvider,
            string textKey,
            bool waitForDismiss = true)
        {
            _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
            _textKey = string.IsNullOrWhiteSpace(textKey)
                ? throw new ArgumentException("A dialogue localization key is required.", nameof(textKey))
                : textKey;
            _waitForDismiss = waitForDismiss;
        }

        /// <inheritdoc />
        public override void Execute(ITutorialRuntimeContext context, Action onComplete)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Dialogue.Play(
                _textProvider.GetText(_textKey),
                _waitForDismiss ? onComplete : null);
            if (!_waitForDismiss)
                onComplete?.Invoke();
        }
    }
}
