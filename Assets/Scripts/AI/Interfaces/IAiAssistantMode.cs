using System.Threading;
using System.Threading.Tasks;
using AMath.Core.Assistance.Context;

namespace AMath.AI.Interfaces
{
    /// <summary>
    /// Optional chat assistant mode (e.g. Rule Assistant, scripted Strategy
    /// Coach). Modes consume the same <see cref="GameContextSnapshot"/> but
    /// answer different questions. Tile-placing AI seats use
    /// <c>IAiMoveChooser</c> instead — not this interface.
    /// </summary>
    public interface IAiAssistantMode
    {
        /// <summary>Stable identifier for this mode (e.g. selecting it in the chat UI).</summary>
        string ModeId { get; }

        /// <summary>Produces an answer for <paramref name="question"/> given the current, read-only game context.</summary>
        Task<string> RespondAsync(string question, GameContextSnapshot context, CancellationToken cancellationToken);
    }
}
