namespace AMath.Core.Assistance.Context
{
    /// <summary>
    /// Single aggregation point the AI Assistant depends on. Internally
    /// composes <see cref="IBoardStateReader"/>, <see cref="IPlayerStateReader"/>,
    /// <see cref="IMatchStateReader"/>, <see cref="ISelectionStateReader"/> and
    /// <see cref="ITutorialProgressReader"/> — callers outside Core never need
    /// to know those five interfaces exist; they depend on this one.
    /// </summary>
    public interface IGameContextProvider
    {
        /// <summary>Captures a fresh snapshot of everything the AI is allowed to see, right now.</summary>
        GameContextSnapshot Capture();
    }
}
