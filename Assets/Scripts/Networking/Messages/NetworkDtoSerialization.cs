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
