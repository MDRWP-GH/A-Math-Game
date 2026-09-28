using System;
using AMath.Tutorial.Definitions;
using AMath.Tutorial.Interfaces;

namespace AMath.Tutorial.Save
{
    /// <summary>
    /// Plain, serializable record of how far a player has gotten in one
    /// tutorial. Mirrors the shape of <c>AMath.Save.SaveFile</c>'s role for
    /// matches, but intentionally has no dependency on it — tutorial
    /// progress and match saves are unrelated concerns that happen to both
    /// be persisted to disk.
    /// </summary>
    [Serializable]
    public sealed class TutorialProgressData
    {
        /// <summary>Id of the tutorial this record belongs to.</summary>
        public string TutorialId;

        /// <summary>0-based index of the furthest step reached.</summary>
        public int StepIndex;

        /// <summary>True once every step has been completed.</summary>
        public bool IsCompleted;

        /// <summary>UTC ticks of the last write, for diagnostics/ordering.</summary>
        public long TimestampUtcTicks;
    }

    /// <summary>Derives chapter locks from the latest persisted progress.</summary>
    internal readonly struct TutorialChapterAvailability
    {
        public TutorialChapterAvailability(bool connectUnlocked, bool premiumUnlocked)
        {
            ConnectUnlocked = connectUnlocked;
            PremiumUnlocked = premiumUnlocked;
        }

        public bool ConnectUnlocked { get; }
        public bool PremiumUnlocked { get; }

        public static TutorialChapterAvailability Load(ITutorialSaveStore saveStore)
        {
            bool introComplete = saveStore != null
                && saveStore.TryLoad(IntroTutorialSequence.IntroTutorialId, out TutorialProgressData intro)
                && intro.IsCompleted;
            bool connectComplete = saveStore != null
                && saveStore.TryLoad(ConnectTutorialSequence.ConnectTutorialId, out TutorialProgressData connect)
                && connect.IsCompleted;
            return new TutorialChapterAvailability(introComplete, connectComplete);
        }
    }
}
