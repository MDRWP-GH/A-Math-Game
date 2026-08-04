using System;
using System.IO;
using AMath.Gameplay.Board;

namespace AMath.Core.Commands
{
    /// <summary>
    /// Compact, Mirror-independent binary serialization for commands.
    ///
    /// Living in Core (not the networking assembly) is deliberate: the same
    /// bytes are used on the wire, inside <see cref="TurnRecord"/>s, in replay
    /// files and in saves — so replay/save never depend on a transport library.
    /// A full 8-tile placement is 2 + 8*4 = 34 bytes.
    /// </summary>
    public static class CommandSerializer
    {
        #region Serialization

        /// <summary>Serializes a command (payload only, excluding the type byte).</summary>
        public static byte[] Serialize(IGameCommand command)
        {
            using var stream = new MemoryStream(64);
            using var writer = new BinaryWriter(stream);

            switch (command)
            {
                case PlaceTilesCommand place:
                    writer.Write((byte)place.Placements.Count);
                    foreach (TilePlacement p in place.Placements)
                    {
                        writer.Write(p.TileId);
                        writer.Write(p.X);
                        writer.Write(p.Y);
                        writer.Write(p.DeclaredAs);
                    }
                    break;

                case ExchangeTilesCommand exchange:
                    writer.Write((byte)exchange.TileIds.Count);
                    foreach (byte tileId in exchange.TileIds)
                        writer.Write(tileId);
                    break;

                case PassTurnCommand pass:
                    writer.Write(pass.WasTimeout);
                    break;

                default:
                    throw new NotSupportedException($"Unknown command type {command?.GetType().Name}.");
            }

            return stream.ToArray();
        }

        #endregion

        #region Deserialization

        /// <summary>
        /// Deserializes a command payload. Throws <see cref="InvalidDataException"/>
        /// on malformed input — callers on the host treat that as a rejected
        /// (potentially malicious) request, never as a crash.
        /// </summary>
        public static IGameCommand Deserialize(CommandType type, byte[] payload)
        {
            if (payload == null) throw new InvalidDataException("Null command payload.");

            using var stream = new MemoryStream(payload, false);
            using var reader = new BinaryReader(stream);

            try
            {
                switch (type)
                {
                    case CommandType.PlaceTiles:
                        return ReadPlaceTiles(reader);
                    case CommandType.ExchangeTiles:
                        return ReadExchange(reader);
                    case CommandType.PassTurn:
                        return new PassTurnCommand { WasTimeout = reader.ReadBoolean() };
                    default:
                        throw new InvalidDataException($"Unknown command type {type}.");
                }
            }
            catch (EndOfStreamException)
            {
                throw new InvalidDataException("Truncated command payload.");
            }
        }

        private static PlaceTilesCommand ReadPlaceTiles(BinaryReader reader)
        {
            int count = reader.ReadByte();
            if (count < 1 || count > GameRules.RackSize)
                throw new InvalidDataException($"Invalid placement count {count}.");

            var command = new PlaceTilesCommand();
            for (int i = 0; i < count; i++)
            {
                command.Placements.Add(new TilePlacement
                {
                    TileId = reader.ReadByte(),
                    X = reader.ReadByte(),
                    Y = reader.ReadByte(),
                    DeclaredAs = reader.ReadByte()
                });
            }

            return command;
        }

        private static ExchangeTilesCommand ReadExchange(BinaryReader reader)
        {
            int count = reader.ReadByte();
            if (count < 1 || count > GameRules.RackSize)
                throw new InvalidDataException($"Invalid exchange count {count}.");

            var command = new ExchangeTilesCommand();
            for (int i = 0; i < count; i++)
                command.TileIds.Add(reader.ReadByte());

            return command;
        }

        #endregion
    }
}
