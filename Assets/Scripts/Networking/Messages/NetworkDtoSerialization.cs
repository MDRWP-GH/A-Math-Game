using System.IO;
using AMath.Core;
using Mirror;

namespace AMath.Networking.Messages
{
    /// <summary>
    /// Custom Mirror writers/readers for core DTOs used in RPC signatures.
    /// Mirror's weaver picks these up automatically, so <see cref="TurnRecord"/>
    /// and <see cref="MatchConfig"/> cross the wire as compact binary
    /// (a typical turn is ~50 bytes) instead of reflection-based JSON.
    /// </summary>
    public static class NetworkDtoSerialization
    {
        /// <summary>
        /// Largest command payload a peer may claim. Matches the cap the host
        /// already enforces on <c>CmdSubmitCommand</c>, so the client-bound RPC
        /// path is no more trusting than the client-to-host path.
        /// </summary>
        public const int MaxCommandPayloadBytes = 256;

        #region TurnRecord

        public static void WriteTurnRecord(this NetworkWriter writer, TurnRecord record)
        {
            writer.WriteInt(record.TurnNumber);
            writer.WriteInt(record.PlayerId);
            writer.WriteByte(record.CommandType);
            writer.WriteBytesAndSize(record.CommandPayload);
            writer.WriteInt(record.ScoreDelta);
            writer.WriteLong(record.TimestampUtcTicks);
            writer.WriteBool(record.EndedMatch);
            writer.WriteByte((byte)record.EndReason);
        }

        public static TurnRecord ReadTurnRecord(this NetworkReader reader)
        {
            int turnNumber = reader.ReadInt();
            int playerId = reader.ReadInt();
            byte commandType = reader.ReadByte();
            byte[] payload = reader.ReadBytesAndSize();

            if (payload != null && payload.Length > MaxCommandPayloadBytes)
            {
                throw new InvalidDataException(
                    $"Turn record payload of {payload.Length} bytes exceeds the {MaxCommandPayloadBytes}-byte limit.");
            }

            return new TurnRecord
            {
                TurnNumber = turnNumber,
                PlayerId = playerId,
                CommandType = commandType,
                CommandPayload = payload,
                ScoreDelta = reader.ReadInt(),
                TimestampUtcTicks = reader.ReadLong(),
                EndedMatch = reader.ReadBool(),
                EndReason = (MatchEndReason)reader.ReadByte()
            };
        }

        #endregion

        #region MatchResult

        public static void WriteMatchResult(this NetworkWriter writer, MatchResult result)
        {
            writer.WriteByte((byte)result.Reason);
            writer.WriteInt(result.WinnerPlayerId);
            writer.WriteInt(result.WinnerTeamId);
            writer.WriteByte((byte)result.Format);
            writer.WriteBool(result.IsDraw);
            writer.WriteLong(result.StartedUtcTicks);
            writer.WriteLong(result.EndedUtcTicks);
            writer.WriteInt(result.DurationSeconds);

            writer.WriteInt(result.Standings?.Count ?? 0);
            if (result.Standings != null)
            {
                foreach (PlayerResult row in result.Standings)
                {
                    writer.WriteInt(row.PlayerId);
                    writer.WriteString(row.DisplayName);
                    writer.WriteInt(row.FinalScore);
                    writer.WriteInt(row.TeamId);
                }
            }

            writer.WriteInt(result.TeamStandings?.Count ?? 0);
            if (result.TeamStandings != null)
            {
                foreach (TeamResult team in result.TeamStandings)
                {
                    writer.WriteInt(team.TeamId);
                    writer.WriteInt(team.TotalScore);
                }
            }
        }

        public static MatchResult ReadMatchResult(this NetworkReader reader)
        {
            var result = new MatchResult
            {
                Reason = (MatchEndReason)reader.ReadByte(),
                WinnerPlayerId = reader.ReadInt(),
                WinnerTeamId = reader.ReadInt(),
                Format = (MatchFormat)reader.ReadByte(),
                IsDraw = reader.ReadBool(),
                StartedUtcTicks = reader.ReadLong(),
                EndedUtcTicks = reader.ReadLong(),
                DurationSeconds = reader.ReadInt()
            };

            int playerCount = reader.ReadInt();
            if (playerCount < 0 || playerCount > GameRules.MaxPlayers)
                throw new InvalidDataException($"Match result claims {playerCount} players.");
            for (int i = 0; i < playerCount; i++)
            {
                result.Standings.Add(new PlayerResult
                {
                    PlayerId = reader.ReadInt(),
                    DisplayName = reader.ReadString(),
                    FinalScore = reader.ReadInt(),
                    TeamId = reader.ReadInt()
                });
            }

            int teamCount = reader.ReadInt();
            if (teamCount < 0 || teamCount > GameRules.TeamCount)
                throw new InvalidDataException($"Match result claims {teamCount} teams.");
            for (int i = 0; i < teamCount; i++)
            {
                result.TeamStandings.Add(new TeamResult
                {
                    TeamId = reader.ReadInt(),
                    TotalScore = reader.ReadInt()
                });
            }

            return result;
        }

        #endregion

        #region MatchConfig

        public static void WriteMatchConfig(this NetworkWriter writer, MatchConfig config)
        {
            writer.WriteInt(config.RandomSeed);
            writer.WriteInt(config.TurnSeconds);
            writer.WriteString(config.GameVersion);
            writer.WriteByte((byte)config.Format);
            writer.WriteInt(config.Players.Count);
            foreach (PlayerIdentity player in config.Players)
            {
                writer.WriteInt(player.PlayerId);
                writer.WriteString(player.PersistentGuid);
                writer.WriteString(player.DisplayName);
                writer.WriteBool(player.IsAi);
                writer.WriteInt(player.TeamId);
                writer.WriteByte(player.ColorId);
            }
        }

        public static MatchConfig ReadMatchConfig(this NetworkReader reader)
        {
            var config = new MatchConfig
            {
                RandomSeed = reader.ReadInt(),
                TurnSeconds = reader.ReadInt(),
                GameVersion = reader.ReadString(),
                Format = (MatchFormat)reader.ReadByte()
            };

            int count = reader.ReadInt();

            // The count is attacker-controlled: a hostile or corrupt host could
            // claim int.MaxValue seats and make every client allocate until it
            // dies. A match can never hold more than MaxPlayers.
            if (count < 0 || count > GameRules.MaxPlayers)
            {
                throw new InvalidDataException(
                    $"Match config claims {count} players; the limit is {GameRules.MaxPlayers}.");
            }

            for (int i = 0; i < count; i++)
            {
                config.Players.Add(new PlayerIdentity
                {
                    PlayerId = reader.ReadInt(),
                    PersistentGuid = reader.ReadString(),
                    DisplayName = reader.ReadString(),
                    IsAi = reader.ReadBool(),
                    TeamId = reader.ReadInt(),
                    ColorId = reader.ReadByte()
                });
            }

            return config;
        }

        #endregion
    }
}
