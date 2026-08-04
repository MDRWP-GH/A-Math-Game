namespace AMath.AI.Interfaces
{
    /// <summary>
    /// Belt-and-suspenders output check. The primary defenses against the AI
    /// leaking hidden information or acting like it controls gameplay are
    /// structural (see <c>AI/Restrictions/</c> notes and
    /// <c>GameContextSnapshot</c>'s deliberately narrow shape); this
    /// interface is the last line — scanning outgoing text for anything
    /// that looks like a command, a cheat, or leaked hidden data before it
    /// ever reaches the chat window.
    /// </summary>
    public interface IAiRestrictionGuard
    {
        /// <summary>
        /// Attempts to pass <paramref name="rawResponse"/> through. Returns
        /// false when the response must be blocked outright; otherwise
        /// <paramref name="safeResponse"/> is the (possibly redacted) text
        /// safe to display.
        /// </summary>
        bool TryFilter(string rawResponse, out string safeResponse);
    }
}
