using System;
using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Commands;
using AMath.Core.Events;
using AMath.Core.RandomNumbers;
using AMath.Core.Snapshot;
using AMath.Core.StateMachines;
using AMath.Gameplay;
using AMath.Gameplay.Board;
using AMath.Gameplay.Players;

namespace AMath.Managers
{
    /// <summary>
    /// Orchestrates a match. Thin by design: rules live in the board domain,
    /// turn flow in <see cref="TurnManager"/>, roster in PlayerManager and
    /// execution in <see cref="CommandProcessor"/> — this class only sequences
    /// them and publishes events.
    ///
    /// The same instance runs on every peer:
    ///  - with <see cref="IsAuthority"/> = true (host): validates requests,
    ///    produces authoritative <see cref="TurnRecord"/>s, runs the turn timer;
    ///  - with false (client): applies host-accepted records to an identical
    ///    local mirror and verifies the result (desync detection).
    /// </summary>
    public sealed class GameManager : ITickable
    {
        #region Fields

        private readonly IEventBus _eventBus;
        private readonly GameStateMachine _stateMachine;
        private readonly BoardManager _boardManager;
        private readonly PlayerManager _playerManager;
        private readonly TurnManager _turnManager;
        private readonly IMatchClock _clock;
        private readonly TileBag _tileBag = new();
        private readonly List<byte> _dealBuffer = new(GameRules.RackSize);

        private CommandProcessor _processor;
        private DeterministicRandom _rng;
        private long _matchStartedUtcTicks;
        private double _matchStartedMonotonicSeconds;
        private double _elapsedBeforeCurrentProcessSeconds;
        private bool _matchFinishedEventPublished;

        #endregion

        #region Properties

        /// <summary>True on the host (and in offline/replay use). Set by the network layer.</summary>
        public bool IsAuthority { get; set; }

        /// <summary>Header of the running match; null while in the lobby.</summary>
        public MatchConfig Config { get; private set; }

        /// <summary>Final result once the match finished.</summary>
        public MatchResult Result { get; private set; }

        /// <summary>Tiles remaining in the bag (UI display).</summary>
        public int BagCount => _tileBag.Count;

        /// <summary>Current phase (delegated to the state machine).</summary>
        public MatchPhase Phase => _stateMachine.CurrentPhase;

        #endregion

        #region Construction

        public GameManager(
            IEventBus eventBus,
            GameStateMachine stateMachine,
            BoardManager boardManager,
            PlayerManager playerManager,
            TurnManager turnManager,
            IMatchClock clock = null)
        {
            _eventBus = eventBus;
            _stateMachine = stateMachine;
            _boardManager = boardManager;
            _playerManager = playerManager;
            _turnManager = turnManager;
            _clock = clock ?? SystemMatchClock.Shared;
            _processor = new CommandProcessor(boardManager, playerManager, turnManager, _tileBag);
        }

        #endregion

        #region Match lifecycle

        /// <summary>
        /// Starts a match deterministically from a config. Runs identically on
        /// every peer, producing identical racks and bag from the shared seed.
        /// </summary>
        public void StartMatch(MatchConfig config) => StartMatch(config, openingRacks: null);

        /// <summary>
        /// Starts a match, optionally dealing predetermined opening racks
        /// (tutorial). When <paramref name="openingRacks"/> is null, tiles
        /// are drawn from the shuffled bag as usual.
        /// </summary>
        public void StartMatch(MatchConfig config, IReadOnlyList<IReadOnlyList<byte>> openingRacks)
        {
            if (Phase == MatchPhase.Playing || Phase == MatchPhase.Paused)
                EndMatchManually();

            // Rematch path: the machine only allows Finished -> Lobby -> Loading.
            if (Phase == MatchPhase.Finished)
                _stateMachine.TransitionTo(MatchPhase.Lobby);

            Config = config ?? throw new ArgumentNullException(nameof(config));
            Result = null;
            _matchStartedUtcTicks = _clock.UtcNowTicks;
            _matchStartedMonotonicSeconds = _clock.MonotonicSeconds;
            _elapsedBeforeCurrentProcessSeconds = 0d;
            _matchFinishedEventPublished = false;

            _rng = new DeterministicRandom(config.RandomSeed);
            _boardManager.Reset();
            _tileBag.Reset(_rng);
            _playerManager.Setup(config);

            if (openingRacks != null)
                DealPredeterminedRacks(openingRacks);
            else
                DealRandomRacks();

            _stateMachine.TransitionTo(MatchPhase.Loading);
            _eventBus.Publish(new MatchStartedEvent { Config = config });

            _turnManager.StartMatch(config.Players.Count, config.TurnSeconds);
            _stateMachine.TransitionTo(MatchPhase.Playing);
        }

        private void DealRandomRacks()
        {
            foreach (PlayerState player in _playerManager.Players)
            {
                _dealBuffer.Clear();
                _tileBag.Draw(GameRules.RackSize, _dealBuffer);
                _playerManager.AddToRack(player.PlayerId, _dealBuffer);
            }
        }

        private void DealPredeterminedRacks(IReadOnlyList<IReadOnlyList<byte>> openingRacks)
        {
            if (openingRacks.Count != _playerManager.Players.Count)
            {
                throw new ArgumentException(
                    "Opening racks must match the seated player count.",
                    nameof(openingRacks));
            }

            foreach (PlayerState player in _playerManager.Players)
            {
                if (player.PlayerId < 0 || player.PlayerId >= openingRacks.Count)
                {
                    throw new ArgumentException(
                        $"No opening rack was supplied for player {player.PlayerId}.",
                        nameof(openingRacks));
                }

                IReadOnlyList<byte> rack = openingRacks[player.PlayerId];
                if (rack == null || rack.Count != GameRules.RackSize)
                {
                    throw new ArgumentException(
                        $"Opening rack for player {player.PlayerId} must contain {GameRules.RackSize} tiles.",
                        nameof(openingRacks));
                }

                _dealBuffer.Clear();
                if (!_tileBag.TryTakeSpecific(rack, _dealBuffer))
                {
                    throw new InvalidOperationException(
                        $"Bag is missing tiles required by player {player.PlayerId}'s opening rack.");
                }

                _playerManager.AddToRack(player.PlayerId, _dealBuffer);
            }
        }

        /// <summary>Ends the match immediately (host choice, or stranded players after failed reconnect).</summary>
        public void EndMatchManually()
        {
            if (Phase == MatchPhase.Finished) return;

            // No match has been dealt yet, so there are no scores to settle and
            // no winner to name. Producing a result here would show the players
            // a defeat screen for a game they never started.
            if (Config == null || Phase == MatchPhase.Lobby)
            {
                AbandonSession();
                return;
            }

            EndMatch(MatchEndReason.EndedManually, finisherPlayerId: -1);
        }

        /// <summary>
        /// Clears local match state and returns to lobby after the room was
        /// dissolved (reconnect/fresh-join failed). Does not publish a match result.
        /// </summary>
        public void AbandonSession()
        {
            Config = null;
            Result = null;
            _stateMachine.RestoreTo(MatchPhase.Lobby);
        }

        #endregion

        #region Host path (authoritative)

        /// <summary>
        /// Validates and executes a command on the host. On success the
        /// resulting <see cref="TurnRecord"/> is exposed for broadcast and a
        /// <see cref="TurnResolvedEvent"/> is published (replay + autosave).
        /// </summary>
        public CommandOutcome SubmitCommand(int playerId, IGameCommand command, out TurnRecord record)
        {
            record = null;

            if (!IsAuthority)
                return new CommandOutcome { Success = false, Error = "Only the host validates commands." };

            if (Phase != MatchPhase.Playing)
                return new CommandOutcome { Success = false, Error = "Match is not in progress." };

            int turnNumber = _turnManager.TurnNumber;
            CommandOutcome outcome = _processor.Execute(playerId, command, _rng);
            if (!outcome.Success)
                return outcome;

            record = new TurnRecord
            {
                TurnNumber = turnNumber,
                PlayerId = playerId,
                CommandType = (byte)command.Type,
                CommandPayload = CommandSerializer.Serialize(command),
                ScoreDelta = outcome.ScoreDelta,
                TimestampUtcTicks = _clock.UtcNowTicks
            };

            FinishTurn(record, outcome);
            _eventBus.Publish(new TurnResolvedEvent { Record = record, IsAuthority = true });
            PublishFinishedMatchAfterTurn(record);
            return outcome;
        }

        #endregion

        #region Client path (mirror)

        /// <summary>
        /// Applies a host-accepted record to the local mirror. Any divergence
        /// from the host's declared result is reported as a desync so the
        /// client can request a full snapshot resync.
        /// </summary>
        public bool ApplyRecord(TurnRecord record) =>
            ApplyRecord(record, publishFinishedEvent: true, allowReplicatedFinishedPhase: false);

        /// <summary>
        /// Applies a network record while reserving the final notification for
        /// the host's authoritative result RPC.
        /// </summary>
        public bool ApplyReplicatedRecord(TurnRecord record) =>
            ApplyRecord(record, publishFinishedEvent: false, allowReplicatedFinishedPhase: true);

        private bool ApplyRecord(
            TurnRecord record,
            bool publishFinishedEvent,
            bool allowReplicatedFinishedPhase)
        {
            if (record == null)
            {
                _eventBus.Publish(new DesyncDetectedEvent { Reason = "Host sent a missing turn record." });
                return false;
            }

            // A finished match accepts nothing more. The turn that ends a match
            // never advances the turn counter (FinishTurn returns early), so a
            // duplicate of that last record still looks "current" by turn number
            // alone and would otherwise be re-executed and reported as a desync.
            bool phaseArrivedBeforeTerminalTurn = Phase == MatchPhase.Finished;
            if (phaseArrivedBeforeTerminalTurn)
            {
                // A SyncVar phase update can be observed before the reliable
                // terminal-turn RPC. Temporarily reopen only that one missing
                // terminal record so replay/autosave still see the final turn.
                if (!allowReplicatedFinishedPhase || Result != null || !record.EndedMatch)
                    return false;
            }

            // Turn numbers are the only ordering guarantee we have. A record we
            // already executed must never run twice (it would double the score
            // and the tile draw), and a record from the future means we dropped
            // one in between and can only recover with a full resync.
            if (record.TurnNumber < _turnManager.TurnNumber)
                return false;

            if (record.TurnNumber > _turnManager.TurnNumber)
            {
                _eventBus.Publish(new DesyncDetectedEvent
                {
                    Reason = $"Missing turns (local {_turnManager.TurnNumber}, host {record.TurnNumber})."
                });
                return false;
            }

            IGameCommand command;
            try
            {
                command = CommandSerializer.Deserialize((CommandType)record.CommandType, record.CommandPayload);
            }
            catch (Exception ex)
            {
                _eventBus.Publish(new DesyncDetectedEvent { Reason = $"Unreadable record: {ex.Message}" });
                return false;
            }

            // Reopen only after the record is known to be current and readable.
            // If execution still fails, keep the replicated Finished phase while
            // the desync handler requests an authoritative snapshot.
            if (phaseArrivedBeforeTerminalTurn)
                _stateMachine.RestoreTo(MatchPhase.Playing);

            bool applied = false;
            try
            {
                CommandOutcome outcome = _processor.Execute(record.PlayerId, command, _rng);
                if (!outcome.Success)
                {
                    _eventBus.Publish(new DesyncDetectedEvent { Reason = $"Record failed locally: {outcome.Error}" });
                    return false;
                }

                if (outcome.ScoreDelta != record.ScoreDelta)
                {
                    _eventBus.Publish(new DesyncDetectedEvent
                    {
                        Reason = $"Score mismatch (local {outcome.ScoreDelta}, host {record.ScoreDelta})."
                    });
                    return false;
                }

                FinishTurn(record, outcome);
                _eventBus.Publish(new TurnResolvedEvent { Record = record, IsAuthority = false });
                if (publishFinishedEvent)
                    PublishFinishedMatchAfterTurn(record);
                applied = true;
                return true;
            }
            finally
            {
                if (phaseArrivedBeforeTerminalTurn && !applied && Phase != MatchPhase.Finished)
                    _stateMachine.RestoreTo(MatchPhase.Finished);
            }
        }

        /// <summary>
        /// Replaces a client's provisional result with the host result and
        /// publishes completion exactly once.
        /// </summary>
        public bool ApplyAuthoritativeResult(MatchResult result)
        {
            if (result == null)
                return false;

            Result = result;
            if (result.Standings != null)
            {
                foreach (PlayerResult row in result.Standings)
                {
                    PlayerState player = _playerManager.GetById(row.PlayerId);
                    if (player != null)
                        player.Score = row.FinalScore;
                }
            }
            if (Phase != MatchPhase.Finished)
                _stateMachine.RestoreTo(MatchPhase.Finished);
            PublishMatchFinishedOnce(result);
            return true;
        }

        #endregion

        #region Shared turn resolution

        private void FinishTurn(TurnRecord record, CommandOutcome outcome)
        {
            PlayerState actor = _playerManager.GetById(record.PlayerId);
            if (actor == null)
            {
                _eventBus.Publish(new DesyncDetectedEvent
                {
                    Reason = $"Record names unknown player {record.PlayerId}."
                });
                return;
            }

            // End condition 1: the actor emptied their rack with an empty bag.
            if (actor.Rack.Count == 0 && _tileBag.Count == 0)
            {
                if (EndMatch(MatchEndReason.PlayerFinishedTiles, actor.PlayerId, publishEvent: false))
                {
                    record.EndedMatch = true;
                    record.EndReason = MatchEndReason.PlayerFinishedTiles;
                }
                return;
            }

            _turnManager.AdvanceTurn(outcome.CountsAsPass);

            // End condition 2: everyone passed for the configured number of rounds.
            if (_turnManager.ShouldEndByPasses)
            {
                if (EndMatch(MatchEndReason.AllPlayersPassed, finisherPlayerId: -1, publishEvent: false))
                {
                    record.EndedMatch = true;
                    record.EndReason = MatchEndReason.AllPlayersPassed;
                }
            }
        }

        /// <summary>
        /// Publishes completion only after the terminal TurnResolved event has
        /// reached replay/autosave subscribers. Manual endings have no terminal
        /// turn, so they continue to publish directly from EndMatch.
        /// </summary>
        private void PublishFinishedMatchAfterTurn(TurnRecord record)
        {
            if (record?.EndedMatch == true && Result != null)
                PublishMatchFinishedOnce(Result);
        }

        private void PublishMatchFinishedOnce(MatchResult result)
        {
            if (_matchFinishedEventPublished || result == null)
                return;

            _matchFinishedEventPublished = true;
            _eventBus.Publish(new MatchFinishedEvent { Result = result });
        }

        private bool EndMatch(MatchEndReason reason, int finisherPlayerId, bool publishEvent = true)
        {
            // Only a live match can end. Scoring from any other phase would
            // mutate racks and announce a winner for a match that was never
            // dealt, which every peer would render as a final scoreboard.
            if (Config == null || (Phase != MatchPhase.Playing && Phase != MatchPhase.Paused))
            {
                UnityEngine.Debug.LogWarning(
                    $"[Match] Ignoring end request (reason {reason}) from phase {Phase}.");
                return false;
            }

            // Leftover-tile adjustment: everyone loses their remaining tile
            // points; a player who went out additionally gains everyone else's.
            int forfeited = 0;
            foreach (PlayerState player in _playerManager.Players)
            {
                int leftover = 0;
                foreach (byte tileId in player.Rack)
                    leftover += AMathTileSet.PointsOf(tileId);

                player.Score -= leftover;
                if (player.PlayerId != finisherPlayerId)
                    forfeited += leftover;
            }

            PlayerState finisher = finisherPlayerId >= 0 ? _playerManager.GetById(finisherPlayerId) : null;
            if (finisher != null)
                finisher.Score += forfeited;

            var result = new MatchResult
            {
                Reason = reason,
                Format = Config?.Format ?? MatchFormat.Individual,
                StartedUtcTicks = _matchStartedUtcTicks,
                EndedUtcTicks = _clock.UtcNowTicks
            };
            double elapsed = CurrentElapsedSeconds();
            result.DurationSeconds = elapsed >= int.MaxValue ? int.MaxValue : (int)elapsed;
            if (result.StartedUtcTicks > 0)
            {
                long minimumEndTicks = result.StartedUtcTicks
                    + (long)result.DurationSeconds * TimeSpan.TicksPerSecond;
                if (result.EndedUtcTicks < minimumEndTicks)
                    result.EndedUtcTicks = minimumEndTicks;
            }

            int bestScore = int.MinValue;
            int bestScoreCount = 0;
            foreach (PlayerState player in _playerManager.Players)
            {
                int teamId = GetTeamId(player.PlayerId);
                result.Standings.Add(new PlayerResult
                {
                    PlayerId = player.PlayerId,
                    DisplayName = player.DisplayName,
                    FinalScore = player.Score,
                    TeamId = teamId
                });

                if (result.Format != MatchFormat.Team && player.Score > bestScore)
                {
                    bestScore = player.Score;
                    result.WinnerPlayerId = player.PlayerId;
                    bestScoreCount = 1;
                }
                else if (result.Format != MatchFormat.Team && player.Score == bestScore)
                {
                    bestScoreCount++;
                }
            }

            if (result.Format == MatchFormat.Team)
                ResolveTeamWinner(result);
            else
            {
                if (bestScoreCount > 1)
                {
                    result.IsDraw = true;
                    result.WinnerPlayerId = -1;
                }
                result.Standings.Sort((a, b) => b.FinalScore.CompareTo(a.FinalScore));
            }

            Result = result;

            _stateMachine.TransitionTo(MatchPhase.Finished);
            if (publishEvent)
                PublishMatchFinishedOnce(result);
            return true;
        }

        private int GetTeamId(int playerId)
        {
            if (Config?.Players == null)
                return -1;

            foreach (PlayerIdentity identity in Config.Players)
            {
                if (identity.PlayerId == playerId)
                    return identity.TeamId;
            }

            return -1;
        }

        private static void ResolveTeamWinner(MatchResult result)
        {
            var teamScores = new Dictionary<int, int>();
            var teamMembers = new Dictionary<int, List<PlayerResult>>();

            foreach (PlayerResult row in result.Standings)
            {
                if (row.TeamId < 0)
                    continue;

                teamScores.TryGetValue(row.TeamId, out int total);
                teamScores[row.TeamId] = total + row.FinalScore;

                if (!teamMembers.TryGetValue(row.TeamId, out List<PlayerResult> members))
                {
                    members = new List<PlayerResult>();
                    teamMembers[row.TeamId] = members;
                }

                members.Add(row);
            }

            int bestTeamScore = int.MinValue;
            int winningTeamId = -1;
            int bestTeamCount = 0;
            foreach (KeyValuePair<int, int> pair in teamScores)
            {
                result.TeamStandings.Add(new TeamResult { TeamId = pair.Key, TotalScore = pair.Value });
                if (pair.Value > bestTeamScore)
                {
                    bestTeamScore = pair.Value;
                    winningTeamId = pair.Key;
                    bestTeamCount = 1;
                }
                else if (pair.Value == bestTeamScore)
                {
                    bestTeamCount++;
                }
            }

            result.TeamStandings.Sort((a, b) => b.TotalScore.CompareTo(a.TotalScore));
            result.IsDraw = bestTeamCount > 1;
            result.WinnerTeamId = result.IsDraw ? -1 : winningTeamId;

            if (!result.IsDraw && winningTeamId >= 0
                && teamMembers.TryGetValue(winningTeamId, out List<PlayerResult> winners))
            {
                PlayerResult captain = winners[0];
                foreach (PlayerResult candidate in winners)
                {
                    if (candidate.FinalScore > captain.FinalScore)
                        captain = candidate;
                }

                result.WinnerPlayerId = captain.PlayerId;
            }

            result.Standings.Sort((a, b) => b.FinalScore.CompareTo(a.FinalScore));
        }

        #endregion

        #region Timer

        private double CurrentElapsedSeconds()
        {
            if (_matchStartedUtcTicks <= 0)
                return 0d;

            double sinceAnchor = _clock.MonotonicSeconds - _matchStartedMonotonicSeconds;
            if (double.IsNaN(sinceAnchor) || double.IsInfinity(sinceAnchor) || sinceAnchor < 0d)
                sinceAnchor = 0d;
            return Math.Max(0d, _elapsedBeforeCurrentProcessSeconds + sinceAnchor);
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            // Timeout handling is authoritative and flows through the same
            // command pipeline, so it is replayable and synchronized like any move.
            if (!IsAuthority || Phase != MatchPhase.Playing)
                return;

            if (_turnManager.TickTimer(deltaTime))
                SubmitCommand(_turnManager.CurrentPlayerId, new PassTurnCommand { WasTimeout = true }, out _);
        }

        #endregion

        #region Snapshot support

        /// <summary>Captures the complete match state (save / migration / resync).</summary>
        public GameStateSnapshot CaptureSnapshot()
        {
            var snapshot = new GameStateSnapshot
            {
                Config = Config,
                Phase = (byte)Phase,
                RandomState = _rng?.State ?? 0,
                TurnNumber = _turnManager.TurnNumber,
                CurrentPlayerId = _turnManager.CurrentPlayerId,
                ConsecutivePasses = _turnManager.ConsecutivePasses,
                BagTiles = _tileBag.ExportContents(),
                Result = Result,
                MatchStartedUtcTicks = _matchStartedUtcTicks,
                MatchElapsedSeconds = Result != null
                    ? Math.Max(0, Result.DurationSeconds)
                    : CurrentElapsedSeconds()
            };

            _boardManager.ExportTo(snapshot);
            _playerManager.ExportTo(snapshot);
            return snapshot;
        }

        /// <summary>
        /// Restores the complete match state from a snapshot (load, host
        /// migration, reconnection resync). The phase comes from the snapshot
        /// unless <paramref name="enterPhase"/> overrides it — a migrated host
        /// passes Paused because it waits for players before resuming.
        /// </summary>
        public void RestoreSnapshot(GameStateSnapshot snapshot, MatchPhase? enterPhase = null)
        {
            string snapshotError = null;
            if (snapshot == null || !snapshot.TryValidate(out snapshotError))
                throw new ArgumentException(snapshotError ?? "Snapshot is missing.", nameof(snapshot));

            Config = snapshot.Config;
            Result = snapshot.Result;
            if (Result == null)
                _matchFinishedEventPublished = false;
            _matchStartedUtcTicks = snapshot.MatchStartedUtcTicks;
            _elapsedBeforeCurrentProcessSeconds = RestoreElapsedSeconds(snapshot);
            _matchStartedMonotonicSeconds = _clock.MonotonicSeconds;
            _rng = DeterministicRandom.FromState(snapshot.RandomState);

            _boardManager.RestoreFrom(snapshot);
            _playerManager.RestoreFrom(snapshot);
            _tileBag.Restore(snapshot.BagTiles);
            _turnManager.Restore(
                snapshot.Players.Count,
                snapshot.Config.TurnSeconds,
                snapshot.TurnNumber,
                snapshot.CurrentPlayerId,
                snapshot.ConsecutivePasses);

            // Not MatchStartedEvent: this match already has a history, and
            // listeners that reset themselves per match would discard it.
            _eventBus.Publish(new MatchRestoredEvent { Config = Config });
            _stateMachine.RestoreTo(enterPhase ?? (MatchPhase)snapshot.Phase);
            if (Result != null && Phase == MatchPhase.Finished)
                PublishMatchFinishedOnce(Result);
        }

        private double RestoreElapsedSeconds(GameStateSnapshot snapshot)
        {
            if (snapshot.Result != null)
                return Math.Max(0, snapshot.Result.DurationSeconds);
            if (snapshot.MatchElapsedSeconds > 0d)
                return snapshot.MatchElapsedSeconds;
            if (snapshot.MatchStartedUtcTicks <= 0)
                return 0d;

            // Compatibility with saves created before MatchElapsedSeconds was added.
            double utcElapsed = (_clock.UtcNowTicks - snapshot.MatchStartedUtcTicks)
                / (double)TimeSpan.TicksPerSecond;
            return Math.Max(0d, utcElapsed);
        }

        #endregion
    }
}
