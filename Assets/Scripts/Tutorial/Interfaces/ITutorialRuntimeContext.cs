using AMath.Core.Assistance;
using AMath.Core.Events;

namespace AMath.Tutorial.Interfaces
{
    /// <summary>
    /// The dependency bag handed to every <see cref="ITutorialAction"/> and
    /// used by <see cref="ITutorialCondition"/> implementations that need
    /// read access to gameplay. Bundling these references here means
    /// individual actions take one constructor parameter instead of five,
    /// while every dependency is still an explicit, mockable interface —
    /// no service-locator singleton, no ambient static access.
    /// </summary>
    public interface ITutorialRuntimeContext
    {
        /// <summary>Shared event bus (for actions/conditions that need to publish or subscribe directly).</summary>
        IEventBus EventBus { get; }

        /// <summary>Read-only board access, for highlight targeting.</summary>
        IBoardStateReader BoardState { get; }

        /// <summary>Read-only player/hand access, for highlight targeting.</summary>
        IPlayerStateReader PlayerState { get; }

        /// <summary>Read-only match/turn access.</summary>
        IMatchStateReader MatchState { get; }

        /// <summary>Highlight presentation service.</summary>
        ITutorialHighlightService Highlighter { get; }

        /// <summary>Dialogue presentation service.</summary>
        ITutorialDialogueService Dialogue { get; }

        /// <summary>Tutorial UI presentation service (objective, progress, hints).</summary>
        ITutorialUiService Ui { get; }
    }
}
