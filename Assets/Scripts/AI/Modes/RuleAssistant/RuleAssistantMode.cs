using AMath.AI.Context;
using AMath.AI.Interfaces;

namespace AMath.AI.Modes.RuleAssistant
{
    /// <summary>
    /// Explains A-Math rules, placement rejection reasons and scoring from
    /// the supplied current state. It never proposes or executes commands.
    /// </summary>
    public sealed class RuleAssistantMode : AiAssistantModeBase
    {
        /// <summary>Creates the Rule Assistant mode.</summary>
        public RuleAssistantMode(
            IAiClient client,
            AiPromptProfile profile,
            GameContextPromptFormatter formatter)
            : base(client, profile, formatter)
        {
        }
    }
}
