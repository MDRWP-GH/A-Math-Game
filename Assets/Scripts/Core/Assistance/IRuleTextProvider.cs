namespace AMath.Core.Assistance
{
    /// <summary>
    /// Resolves a rule explanation by key. Backed by <c>Localization/</c> so
    /// no rule text is ever hardcoded at the call site — callers (Tutorial
    /// dialogue, the Rule Assistant AI mode) pass a stable key, never prose.
    /// </summary>
    public interface IRuleTextProvider
    {
        /// <summary>Returns the localized rule text for <paramref name="ruleKey"/>, or a fallback if the key is unknown.</summary>
        string GetRuleText(string ruleKey);
    }
}
