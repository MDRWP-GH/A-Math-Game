using System.Threading;
using System.Threading.Tasks;

namespace AMath.AI.Interfaces
{
    /// <summary>
    /// Thin transport abstraction over whatever backend answers questions.
    /// This is the single seam Step 6 fills with a real HTTP-based client;
    /// every mode (<c>RuleAssistant</c>, <c>StrategyCoach</c>,
    /// <c>ReplayCoach</c>) depends on this interface only, never on a
    /// concrete HTTP/SDK type, so the backend can change without touching
    /// mode logic.
    /// </summary>
    public interface IAiClient
    {
        /// <summary>Sends a fully-built prompt and returns the raw model response text.</summary>
        Task<string> SendAsync(string prompt, CancellationToken cancellationToken);
    }
}
