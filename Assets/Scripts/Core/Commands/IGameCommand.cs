namespace AMath.Core.Commands
{
    /// <summary>Discriminator for serialized commands. Values are wire/save format — never reorder.</summary>
    public enum CommandType : byte
    {
        PlaceTiles = 1,
        ExchangeTiles = 2,
        PassTurn = 3
    }

    /// <summary>
    /// A player action in the Command pattern.
    ///
    /// Commands are the single entry point for state mutation:
    ///   UI creates one → network layer ships bytes to the host → host validates
    ///   and executes → the accepted record is broadcast, replayed on every
    ///   client, appended to the replay log and captured by autosave.
    ///
    /// Security: commands deliberately carry no player id. The host stamps the
    /// acting player from the network connection, so payload tampering cannot
    /// impersonate another player.
    /// </summary>
    public interface IGameCommand
    {
        /// <summary>Wire discriminator for this command.</summary>
        CommandType Type { get; }
    }
}
