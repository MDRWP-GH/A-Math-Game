using System.IO;
using AMath.Core.Commands;
using AMath.Gameplay.Board;
using NUnit.Framework;

namespace AMath.Tests
{
    public sealed class CommandSerializerTests
    {
        [Test]
        public void PlaceTilesCommand_RoundTrips()
        {
            var original = new PlaceTilesCommand();
            original.Placements.Add(new TilePlacement { TileId = 12, X = 7, Y = 7, DeclaredAs = TilePlacement.NoDeclaration });
            original.Placements.Add(new TilePlacement { TileId = AMathTileSet.Blank, X = 8, Y = 7, DeclaredAs = 5 });

            byte[] bytes = CommandSerializer.Serialize(original);
            var restored = (PlaceTilesCommand)CommandSerializer.Deserialize(CommandType.PlaceTiles, bytes);

            CollectionAssert.AreEqual(original.Placements, restored.Placements);
        }

        [Test]
        public void ExchangeTilesCommand_RoundTrips()
        {
            var original = new ExchangeTilesCommand();
            original.TileIds.AddRange(new byte[] { 1, 2, AMathTileSet.EqualsSign });

            byte[] bytes = CommandSerializer.Serialize(original);
            var restored = (ExchangeTilesCommand)CommandSerializer.Deserialize(CommandType.ExchangeTiles, bytes);

            CollectionAssert.AreEqual(original.TileIds, restored.TileIds);
        }

        [Test]
        public void PassTurnCommand_RoundTripsTimeoutFlag()
        {
            byte[] bytes = CommandSerializer.Serialize(new PassTurnCommand { WasTimeout = true });
            var restored = (PassTurnCommand)CommandSerializer.Deserialize(CommandType.PassTurn, bytes);
            Assert.IsTrue(restored.WasTimeout);
        }

        [Test]
        public void MalformedPayload_ThrowsInvalidData_NotCrash()
        {
            Assert.Throws<InvalidDataException>(() =>
                CommandSerializer.Deserialize(CommandType.PlaceTiles, new byte[] { 200 })); // absurd count

            Assert.Throws<InvalidDataException>(() =>
                CommandSerializer.Deserialize(CommandType.PlaceTiles, new byte[] { 3, 1, 2 })); // truncated

            Assert.Throws<InvalidDataException>(() =>
                CommandSerializer.Deserialize((CommandType)99, new byte[] { 0 })); // unknown type
        }
    }
}
