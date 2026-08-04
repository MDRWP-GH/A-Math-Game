namespace AMath.Core.Commands
{
    /// <summary>
    /// Request to skip the turn. Also issued *by the host itself* when a
    /// player's turn timer expires, so timeouts flow through the same
    /// validated, replayable pipeline as every other action.
    /// </summary>
    public sealed class PassTurnCommand : IGameCommand
    {
        /// <inheritdoc />
        public CommandType Type => CommandType.PassTurn;

        /// <summary>True when the host generated this pass because the timer expired.</summary>
        public bool WasTimeout { get; set; }
    }
}
