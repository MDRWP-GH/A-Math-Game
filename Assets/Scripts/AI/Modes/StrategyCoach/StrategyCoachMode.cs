using AMath.AI.Context;
using AMath.AI.Interfaces;

namespace AMath.AI.Modes.StrategyCoach
{
    /// <summary>
    /// Suggests strategic options from public state and the local player's
    /// own hand. Advice remains optional and cannot control gameplay.
    /// </summary>
    public sealed class StrategyCoachMode : AiAssistantModeBase
    {
        /// <summary>Creates the Strategy Coach mode.</summary>
        public StrategyCoachMode(
            IAiClient client,
            AiPromptProfile profile,
            GameContextPromptFormatter formatter)
            : base(client, profile, formatter)
        {
        }
    }
}
