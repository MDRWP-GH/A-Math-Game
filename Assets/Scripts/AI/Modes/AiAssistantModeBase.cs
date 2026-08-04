using System;
using System.Threading;
using System.Threading.Tasks;
using AMath.AI.Context;
using AMath.AI.Interfaces;
using AMath.Core.Assistance.Context;

namespace AMath.AI.Modes
{
    /// <summary>
    /// Shared request pipeline for assistant modes. Concrete modes differ only
    /// by their authored prompt profile; transport, safe-context formatting
    /// and cancellation behavior stay consistent and unduplicated.
    /// </summary>
    public abstract class AiAssistantModeBase : IAiAssistantMode
    {
        private readonly IAiClient _client;
        private readonly AiPromptProfile _profile;
        private readonly GameContextPromptFormatter _formatter;

        /// <summary>Creates a mode backed by one prompt profile and shared transport.</summary>
        protected AiAssistantModeBase(
            IAiClient client,
            AiPromptProfile profile,
            GameContextPromptFormatter formatter)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
            if (!_profile.IsValid)
                throw new ArgumentException("AI prompt profile requires a mode id and system instruction.", nameof(profile));
        }

        /// <inheritdoc />
        public string ModeId => _profile.ModeId;

        /// <inheritdoc />
        public virtual Task<string> RespondAsync(
            string question,
            GameContextSnapshot context,
            CancellationToken cancellationToken)
        {
            string prompt = _formatter.Format(_profile.SystemInstruction, question, context);
            return _client.SendAsync(prompt, cancellationToken);
        }
    }

    /// <summary>The selected assistant mode cannot run in the current game state.</summary>
    public sealed class AiModeNotReadyException : InvalidOperationException
    {
        /// <summary>Creates a mode-state failure.</summary>
        public AiModeNotReadyException(string message)
            : base(message)
        {
        }
    }
}
