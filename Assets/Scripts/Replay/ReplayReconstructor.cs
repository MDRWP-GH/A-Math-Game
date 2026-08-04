using AMath.Core;
using AMath.Core.Commands;
using AMath.Core.Events;
using AMath.Core.Snapshot;
using AMath.Core.StateMachines;
using AMath.Gameplay.Board;
using AMath.Gameplay.Players;
using AMath.Managers;

namespace AMath.Replay
{
    /// <summary>
    /// Rebuilds match state from a replay log by re-executing every recorded
    /// command through a fresh, headless instance of the exact same engine
    /// that runs live matches. Determinism (shared seed + ordered commands)
    /// guarantees a bit-identical result: board, racks, bag and scores.
    ///
    /// Uses:
    ///  - replay viewer (step through a match turn by turn),
    ///  - integrity verification of saves,
    ///  - recovering a match when only the event log survived.
    /// </summary>
    public static class ReplayReconstructor
    {
        /// <summary>
        /// Replays the log up to and including <paramref name="upToTurn"/>
        /// (int.MaxValue = whole match) and returns the resulting snapshot.
        /// </summary>
        public static bool TryReconstruct(ReplayLog log, int upToTurn, out GameStateSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;

            if (log?.Config == null)
            {
                error = "Replay log has no match config.";
                return false;
            }

            // A private, headless copy of the whole engine — nothing here
            // touches the live game, networking or the shared event bus.
            var bus = new EventBus();
            var stateMachine = new GameStateMachine(bus);
            var boardManager = new BoardManager();
            var playerManager = new PlayerManager(bus);
            var turnManager = new TurnManager(bus);
            var gameManager = new GameManager(bus, stateMachine, boardManager, playerManager, turnManager)
            {
                IsAuthority = true
            };

            gameManager.StartMatch(log.Config);

            foreach (ReplayEvent evt in log.Events)
            {
                if (evt.Turn > upToTurn) break;

                IGameCommand command;
                try
                {
                    command = CommandSerializer.Deserialize((CommandType)evt.CommandType, evt.ToRecord().CommandPayload);
                }
                catch (System.Exception ex)
                {
                    error = $"Turn {evt.Turn}: unreadable command ({ex.Message}).";
                    return false;
                }

                var outcome = gameManager.SubmitCommand(evt.PlayerId, command, out TurnRecord record);
                if (!outcome.Success)
                {
                    error = $"Turn {evt.Turn}: replay diverged ({outcome.Error}).";
                    return false;
                }

                if (record.ScoreDelta != evt.ScoreDelta)
                {
                    error = $"Turn {evt.Turn}: score mismatch (replay {record.ScoreDelta}, log {evt.ScoreDelta}).";
                    return false;
                }
            }

            snapshot = gameManager.CaptureSnapshot();
            return true;
        }
    }
}
