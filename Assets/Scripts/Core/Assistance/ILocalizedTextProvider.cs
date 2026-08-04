namespace AMath.Core.Assistance
{
    /// <summary>
    /// Resolves a localized string from a stable key. Tutorial definitions
    /// store keys rather than display text so authored content can be
    /// translated without changing runtime logic.
    /// </summary>
    public interface ILocalizedTextProvider
    {
        /// <summary>Returns the localized text for <paramref name="key"/>, or a safe fallback when unknown.</summary>
        string GetText(string key);
    }
}
