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
            return new TurnRecord
            {
                TurnNumber = reader.ReadInt(),
                PlayerId = reader.ReadInt(),
                CommandType = reader.ReadByte(),
                CommandPayload = reader.ReadBytesAndSize(),
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
            for (int i = 0; i < count; i++)
            {
                config.Players.Add(new PlayerIdentity
                {
                    PlayerId = reader.ReadInt(),
                    PersistentGuid = reader.ReadString(),
                    DisplayName = reader.ReadString(),
                    IsAi = reader.ReadBool(),
                    TeamId = reader.ReadInt()
                });
            }

            return config;
        }

        #endregion
    }
}
