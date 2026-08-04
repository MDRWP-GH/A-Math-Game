using UnityEngine;

namespace AMath.Tutorial.Interfaces
{
    /// <summary>
    /// Presents one dialogue line and, when a clip is supplied, plays the
    /// matching pre-recorded voice-over. Voice lines are authored clips, not
    /// synthesized at runtime — this service only plays them back.
    /// </summary>
    public interface ITutorialDialogueService
    {
        /// <summary>
        /// Shows <paramref name="localizedText"/> and, if <paramref name="voiceClip"/>
        /// is not null, plays it. Calls <paramref name="onFinished"/> once the
        /// line (and any voice-over) has finished; instant when there is no clip.
        /// </summary>
        void Play(string localizedText, AudioClip voiceClip, System.Action onFinished);

        /// <summary>Cuts the current line short (skip button) and immediately raises its onFinished.</summary>
        void Skip();
    }
}
