using System;
using AMath.Core.Assistance;
using AMath.Tutorial.Actions;
using AMath.Tutorial.Interfaces;
using UnityEngine;

namespace AMath.Tutorial.Dialogue
{
    /// <summary>
    /// Shows one localized tutorial line and plays its optional, pre-recorded
    /// voice clip. It contains no text lookup or audio implementation;
    /// those responsibilities belong to the injected localization and
    /// dialogue services respectively.
    /// </summary>
    public sealed class TutorialDialogue : TutorialAction
    {
        private readonly ILocalizedTextProvider _textProvider;
        private readonly string _textKey;
        private readonly AudioClip _voiceClip;
        private readonly bool _waitForDismiss;

        /// <summary>Creates a dialogue action from a localization key and optional voice-over.</summary>
        public TutorialDialogue(
            ILocalizedTextProvider textProvider,
            string textKey,
            AudioClip voiceClip = null,
            bool waitForDismiss = true)
        {
            _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
            _textKey = string.IsNullOrWhiteSpace(textKey)
                ? throw new ArgumentException("A dialogue localization key is required.", nameof(textKey))
                : textKey;
            _voiceClip = voiceClip;
            _waitForDismiss = waitForDismiss;
        }

        /// <inheritdoc />
        public override void Execute(ITutorialRuntimeContext context, Action onComplete)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Dialogue.Play(
                _textProvider.GetText(_textKey),
                _voiceClip,
                _waitForDismiss ? onComplete : null);
            if (!_waitForDismiss)
                onComplete?.Invoke();
        }
    }
}
