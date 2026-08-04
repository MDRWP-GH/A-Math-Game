using System.Threading;
using System.Threading.Tasks;
using AMath.Core.Assistance.Context;

namespace AMath.AI.Interfaces
{
    /// <summary>
    /// One of the three coaching responsibilities (Rule Assistant, Strategy
    /// Coach, Replay Coach). All three consume the exact same
    /// <see cref="GameContextSnapshot"/> shape but answer different
    /// questions, which is what keeps them small, independently testable
    /// and addable/removable without touching each other.
    /// </summary>
    public interface IAiAssistantMode
    {
        /// <summary>Stable identifier for this mode (e.g. selecting it in the chat UI).</summary>
        string ModeId { get; }

        /// <summary>Produces an answer for <paramref name="question"/> given the current, read-only game context.</summary>
        Task<string> RespondAsync(string question, GameContextSnapshot context, CancellationToken cancellationToken);
    }
}
