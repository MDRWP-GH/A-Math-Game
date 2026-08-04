using AMath.Tutorial.Save;

namespace AMath.Tutorial.Interfaces
{
    /// <summary>
    /// Persistence contract for tutorial progress. Deliberately separate
    /// from the match <c>SaveManager</c>/<c>SaveFile</c> pipeline in
    /// <c>AMath.Save</c> — tutorial completion must survive independently
    /// of any particular match save, and this interface lets
    /// <c>TutorialManager</c> be unit-tested against an in-memory fake
    /// instead of real disk I/O.
    /// </summary>
    public interface ITutorialSaveStore
    {
        /// <summary>Attempts to load saved progress for a tutorial id.</summary>
        bool TryLoad(string tutorialId, out TutorialProgressData data);

        /// <summary>Persists progress for a tutorial id, overwriting any previous save for it.</summary>
        void Save(string tutorialId, TutorialProgressData data);
    }
}
