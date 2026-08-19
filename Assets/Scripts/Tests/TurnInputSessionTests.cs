using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Commands;
using AMath.Core.Events;
using AMath.Gameplay.Board;
using AMath.Gameplay.Interaction;
using AMath.Gameplay.Players;
using NUnit.Framework;
using static AMath.Gameplay.Board.AMathTileSet;

namespace AMath.Tests
{
    public sealed class TurnInputSessionTests
    {
        [Test]
        public void ConfirmPlace_PublishesLocalCommand_WhenDraftValid()
        {
            var bus = new EventBus();
            var board = new BoardManager();
            var players = new PlayerManager(bus);
            players.Setup(new MatchConfig
            {
                RandomSeed = 1,
                Players =
                {
                    new PlayerIdentity { PlayerId = 0, PersistentGuid = "a", DisplayName = "A" },
                    new PlayerIdentity { PlayerId = 1, PersistentGuid = "b", DisplayName = "B" }
                }
            });
            players.LocalPlayerId = 0;
            players.AddToRack(0, new List<byte> { 1, Plus, 2, EqualsSign, 3, 4, 5, 6 });

            var session = new TurnInputSession(bus, board, players);
            LocalCommandRequestedEvent? published = null;
            bus.Subscribe<LocalCommandRequestedEvent>(evt => published = evt);

            Place(session, 0, 5, 7);
            Place(session, 1, 6, 7);
            Place(session, 2, 7, 7);
            Place(session, 3, 8, 7);
            Place(session, 4, 9, 7);

            Assert.IsTrue(session.PreviewValidation.IsValid, session.PreviewValidation.Error);
            Assert.IsTrue(session.TryConfirmPlace(out string error), error);
            Assert.IsNotNull(published);
            Assert.IsInstanceOf<PlaceTilesCommand>(published.Value.Command);
        }

        private static void Place(TurnInputSession session, int rackIndex, int x, int y)
        {
            session.SelectFromRack(rackIndex);
            Assert.IsTrue(session.TryPlaceOnCell(x, y, out string error), error);
        }
    }
}
