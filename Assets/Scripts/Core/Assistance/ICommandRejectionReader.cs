namespace AMath.Core.Assistance
{
    /// <summary>
    /// Exposes why the local player's last action was refused. The AI
    /// Assistant needs this to answer "why can't I place this?", which is the
    /// question players actually ask when they are stuck.
    /// </summary>
    public interface ICommandRejectionReader
    {
        /// <summary>Reason the last command was rejected, or null when the last action succeeded.</summary>
        string LastRejectionReason { get; }
    }
}
