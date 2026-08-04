using System;
using System.Threading;
using System.Threading.Tasks;
using AMath.AI.Context;
using AMath.AI.Interfaces;
using AMath.Core.Assistance.Context;
using AMath.Core.StateMachines;

namespace AMath.AI.Modes.ReplayCoach
{
    /// <summary>
    /// Reviews public replay facts after a match, identifying strengths and
    /// improvement opportunities. It refuses analysis before match completion
    /// rather than presenting a partial live-game history as a finished review.
    /// </summary>
    public sealed class ReplayCoachMode : AiAssistantModeBase
    {
        /// <summary>Creates the Replay Coach mode.</summary>
        public ReplayCoachMode(
            IAiClient client,
            AiPromptProfile profile,
            GameContextPromptFormatter formatter)
            : base(client, profile, formatter)
        {
        }

        /// <inheritdoc />
        public override Task<string> RespondAsync(
            string question,
            GameContextSnapshot context,
            CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.MatchPhase != MatchPhase.Finished)
                throw new AiModeNotReadyException("Replay Coach is available only after the match finishes.");

            return base.RespondAsync(question, context, cancellationToken);
        }
    }
}
