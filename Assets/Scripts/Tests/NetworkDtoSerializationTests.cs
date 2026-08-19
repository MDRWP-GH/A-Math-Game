using AMath.Core;
using AMath.Networking.Messages;
using Mirror;
using NUnit.Framework;

namespace AMath.Tests
{
    /// <summary>
    /// Round-trips the DTOs that cross the wire. A field silently dropped here
    /// does not fail loudly — it just gives every client a subtly different
    /// view of the match, which is the hardest class of bug to trace back.
    /// </summary>
    public sealed class NetworkDtoSerializationTests
    {
        [Test]
        public void MatchConfig_RoundTrip_PreservesAiSeats()
        {
            var config = new MatchConfig
            {
                RandomSeed = 12345,
                TurnSeconds = 45,
                GameVersion = "1.2.3",
                Players =
                {
                    new PlayerIdentity
                    {
                        PlayerId = 0, PersistentGuid = "guid-human", DisplayName = "Human", IsAi = false
                    },
                    new PlayerIdentity
                    {
                        PlayerId = 1, PersistentGuid = "ai:12345:1", DisplayName = "AI", IsAi = true
                    }
                }
            };

            MatchConfig restored = RoundTrip(config);

            Assert.AreEqual(config.RandomSeed, restored.RandomSeed);
            Assert.AreEqual(config.TurnSeconds, restored.TurnSeconds);
            Assert.AreEqual(config.GameVersion, restored.GameVersion);
            Assert.AreEqual(2, restored.Players.Count);

            for (int i = 0; i < config.Players.Count; i++)
            {
                Assert.AreEqual(config.Players[i].PlayerId, restored.Players[i].PlayerId);
                Assert.AreEqual(config.Players[i].PersistentGuid, restored.Players[i].PersistentGuid);
                Assert.AreEqual(config.Players[i].DisplayName, restored.Players[i].DisplayName);
                Assert.AreEqual(config.Players[i].IsAi, restored.Players[i].IsAi, $"IsAi lost for seat {i}");
            }
        }

        [Test]
        public void TurnRecord_RoundTrip_PreservesEveryField()
        {
            var record = new TurnRecord
            {
                TurnNumber = 7,
                PlayerId = 2,
                CommandType = 3,
                CommandPayload = new byte[] { 1, 2, 3, 250 },
                ScoreDelta = 42,
                TimestampUtcTicks = 638_000_000_000_000_000L,
                EndedMatch = true,
                EndReason = MatchEndReason.AllPlayersPassed
            };

            var writer = new NetworkWriter();
            writer.WriteTurnRecord(record);
            var reader = new NetworkReader(writer.ToArray());
            TurnRecord restored = reader.ReadTurnRecord();

            Assert.AreEqual(record.TurnNumber, restored.TurnNumber);
            Assert.AreEqual(record.PlayerId, restored.PlayerId);
            Assert.AreEqual(record.CommandType, restored.CommandType);
            CollectionAssert.AreEqual(record.CommandPayload, restored.CommandPayload);
            Assert.AreEqual(record.ScoreDelta, restored.ScoreDelta);
            Assert.AreEqual(record.TimestampUtcTicks, restored.TimestampUtcTicks);
            Assert.AreEqual(record.EndedMatch, restored.EndedMatch);
            Assert.AreEqual(record.EndReason, restored.EndReason);
        }

        [Test]
        public void MatchConfig_RoundTrip_PreservesFormatAndTeams()
        {
            var config = new MatchConfig
            {
                RandomSeed = 99,
                TurnSeconds = 60,
                GameVersion = "2.0.0",
                Format = MatchFormat.Team,
                Players =
                {
                    new PlayerIdentity { PlayerId = 0, PersistentGuid = "a", DisplayName = "A", TeamId = 0 },
                    new PlayerIdentity { PlayerId = 1, PersistentGuid = "b", DisplayName = "B", TeamId = 1 }
                }
            };

            MatchConfig restored = RoundTrip(config);
            Assert.AreEqual(MatchFormat.Team, restored.Format);
            Assert.AreEqual(0, restored.Players[0].TeamId);
            Assert.AreEqual(1, restored.Players[1].TeamId);
        }

        private static MatchConfig RoundTrip(MatchConfig config)
        {
            var writer = new NetworkWriter();
            writer.WriteMatchConfig(config);
            var reader = new NetworkReader(writer.ToArray());
            return reader.ReadMatchConfig();
        }
    }
}
