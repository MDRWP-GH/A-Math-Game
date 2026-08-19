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
        private readonly TileBag _tileBag = new();
        private readonly List<byte> _dealBuffer = new(GameRules.RackSize);

        private CommandProcessor _processor;
        private DeterministicRandom _rng;
        private long _matchStartedUtcTicks;

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
            TurnManager turnManager)
        {
            _eventBus = eventBus;
            _stateMachine = stateMachine;
            _boardManager = boardManager;
            _playerManager = playerManager;
            _turnManager = turnManager;
            _processor = new CommandProcessor(boardManager, playerManager, turnManager, _tileBag);
        }

        #endregion

        #region Match lifecycle

        /// <summary>
        /// Starts a match deterministically from a config. Runs identically on
        /// every peer, producing identical racks and bag from the shared seed.
        /// </summary>
        public void StartMatch(MatchConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Result = null;
            _matchStartedUtcTicks = DateTime.UtcNow.Ticks;

            // Rematch path: the machine only allows Finished -> Lobby -> Loading.
            if (Phase == MatchPhase.Finished)
                _stateMachine.TransitionTo(MatchPhase.Lobby);

            _rng = new DeterministicRandom(config.RandomSeed);
            _boardManager.Reset();
            _tileBag.Reset(_rng);
            _playerManager.Setup(config);

            // Deal racks in seat order — order is part of the deterministic contract.
            foreach (PlayerState player in _playerManager.Players)
            {
                _dealBuffer.Clear();
                _tileBag.Draw(GameRules.RackSize, _dealBuffer);
                _playerManager.AddToRack(player.PlayerId, _dealBuffer);
            }

            _stateMachine.TransitionTo(MatchPhase.Loading);
            _eventBus.Publish(new MatchStartedEvent { Config = config });

            _turnManager.StartMatch(config.Players.Count, config.TurnSeconds);
            _stateMachine.TransitionTo(MatchPhase.Playing);
        }

        /// <summary>Ends the match immediately (host choice, or stranded players after failed migration).</summary>
        public void EndMatchManually()
        {
            if (Phase == MatchPhase.Finished) return;
            EndMatch(MatchEndReason.EndedManually, finisherPlayerId: -1);
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
                TimestampUtcTicks = DateTime.UtcNow.Ticks
            };

            FinishTurn(record, outcome);
            _eventBus.Publish(new TurnResolvedEvent { Record = record, IsAuthority = true });
            return outcome;
        }

        #endregion

        #region Client path (mirror)

        /// <summary>
        /// Applies a host-accepted record to the local mirror. Any divergence
        /// from the host's declared result is reported as a desync so the
        /// client can request a full snapshot resync.
        /// </summary>
        public bool ApplyRecord(TurnRecord record)
        {
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
                record.EndedMatch = true;
                record.EndReason = MatchEndReason.PlayerFinishedTiles;
                EndMatch(MatchEndReason.PlayerFinishedTiles, actor.PlayerId);
                return;
            }

            _turnManager.AdvanceTurn(outcome.CountsAsPass);

            // End condition 2: everyone passed for the configured number of rounds.
            if (_turnManager.ShouldEndByPasses)
            {
                record.EndedMatch = true;
                record.EndReason = MatchEndReason.AllPlayersPassed;
                EndMatch(MatchEndReason.AllPlayersPassed, finisherPlayerId: -1);
            }
        }

        private void EndMatch(MatchEndReason reason, int finisherPlayerId)
        {
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
                EndedUtcTicks = DateTime.UtcNow.Ticks
            };
            result.DurationSeconds = result.StartedUtcTicks > 0
                ? (int)((result.EndedUtcTicks - result.StartedUtcTicks) / TimeSpan.TicksPerSecond)
                : 0;

            int bestScore = int.MinValue;
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
                }
            }

            if (result.Format == MatchFormat.Team)
                ResolveTeamWinner(result);
            else
                result.Standings.Sort((a, b) => b.FinalScore.CompareTo(a.FinalScore));

            Result = result;

            _stateMachine.TransitionTo(MatchPhase.Finished);
            _eventBus.Publish(new MatchFinishedEvent { Result = result });
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
            foreach (KeyValuePair<int, int> pair in teamScores)
            {
                result.TeamStandings.Add(new TeamResult { TeamId = pair.Key, TotalScore = pair.Value });
                if (pair.Value > bestTeamScore)
                {
                    bestTeamScore = pair.Value;
                    winningTeamId = pair.Key;
                }
            }

            result.TeamStandings.Sort((a, b) => b.TotalScore.CompareTo(a.TotalScore));
            result.WinnerTeamId = winningTeamId;

            if (winningTeamId >= 0
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
                MatchStartedUtcTicks = _matchStartedUtcTicks
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
            Config = snapshot.Config;
            Result = snapshot.Result;
            _matchStartedUtcTicks = snapshot.MatchStartedUtcTicks;
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

            _eventBus.Publish(new MatchStartedEvent { Config = Config });
            _stateMachine.RestoreTo(enterPhase ?? (MatchPhase)snapshot.Phase);
        }

        #endregion
    }
}
