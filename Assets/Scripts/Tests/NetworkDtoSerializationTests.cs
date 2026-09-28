using System.Collections.Generic;
using AMath.Core;
using AMath.Networking.Messages;
using Mirror;
using NUnit.Framework;
using UnityEngine;

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

        [Test]
        public void MatchConfig_RoundTrip_PreservesPlayerColors()
        {
            var config = new MatchConfig
            {
                RandomSeed = 7,
                TurnSeconds = 60,
                GameVersion = "2.1.0",
                Players =
                {
                    new PlayerIdentity { PlayerId = 0, PersistentGuid = "a", DisplayName = "A", ColorId = 0 },
                    new PlayerIdentity { PlayerId = 1, PersistentGuid = "b", DisplayName = "B", ColorId = 7 },
                    // Highest valid id: an off-by-one in the palette bounds would
                    // silently repaint this seat instead of failing.
                    new PlayerIdentity
                    {
                        PlayerId = 2,
                        PersistentGuid = "c",
                        DisplayName = "C",
                        ColorId = (byte)(PlayerColorPalette.Count - 1)
                    }
                }
            };

            MatchConfig restored = RoundTrip(config);

            Assert.AreEqual(3, restored.Players.Count);
            for (int i = 0; i < config.Players.Count; i++)
            {
                Assert.AreEqual(
                    config.Players[i].ColorId,
                    restored.Players[i].ColorId,
                    $"ColorId lost for seat {i}");
            }
        }

        [Test]
        public void PlayerColorPalette_HasThirteenDistinctColors()
        {
            Assert.AreEqual(13, PlayerColorPalette.Count);
            Assert.IsFalse(PlayerColorPalette.IsValid((byte)PlayerColorPalette.Count));

            var seen = new HashSet<Color32>();
            for (byte id = 0; id < PlayerColorPalette.Count; id++)
            {
                Assert.IsTrue(PlayerColorPalette.IsValid(id));
                Assert.IsTrue(seen.Add(PlayerColorPalette.ColorOf(id)), $"Colour {id} is a duplicate");
                Assert.IsNotEmpty(PlayerColorPalette.NameKeyOf(id));
            }
        }

        [Test]
        public void FirstUnused_SkipsTakenColorsAndWraps()
        {
            var taken = new HashSet<byte> { 0, 1, 2 };

            // Starts inside the taken run, so it has to walk past all three.
            Assert.AreEqual(3, PlayerColorPalette.FirstUnused(taken.Contains, 0));

            // Starts at the end of the palette and must wrap to find a free slot.
            var allButFour = new HashSet<byte>();
            for (byte id = 0; id < PlayerColorPalette.Count; id++)
            {
                if (id != 4) allButFour.Add(id);
            }

            Assert.AreEqual(4, PlayerColorPalette.FirstUnused(allButFour.Contains, PlayerColorPalette.Count - 1));
        }

        [Test]
        public void ReadTurnRecord_RejectsOversizedPayload()
        {
            var writer = new NetworkWriter();
            writer.WriteInt(1);
            writer.WriteInt(0);
            writer.WriteByte(1);
            writer.WriteBytesAndSize(new byte[NetworkDtoSerialization.MaxCommandPayloadBytes + 1]);
            writer.WriteInt(0);
            writer.WriteLong(0);
            writer.WriteBool(false);
            writer.WriteByte(0);

            var reader = new NetworkReader(writer.ToArray());
            Assert.Throws<System.IO.InvalidDataException>(() => reader.ReadTurnRecord());
        }

        [Test]
        public void ReadMatchConfig_RejectsTooManyPlayers()
        {
            var writer = new NetworkWriter();
            writer.WriteInt(1);
            writer.WriteInt(60);
            writer.WriteString("test");
            writer.WriteByte((byte)MatchFormat.Individual);
            writer.WriteInt(GameRules.MaxPlayers + 1);

            var reader = new NetworkReader(writer.ToArray());
            Assert.Throws<System.IO.InvalidDataException>(() => reader.ReadMatchConfig());
        }

        [Test]
        public void MatchResult_RoundTrip_PreservesAuthoritativeDrawAndTiming()
        {
            var result = new MatchResult
            {
                Reason = MatchEndReason.EndedManually,
                WinnerPlayerId = -1,
                WinnerTeamId = -1,
                Format = MatchFormat.Team,
                IsDraw = true,
                StartedUtcTicks = 100,
                EndedUtcTicks = 200,
                DurationSeconds = 42
            };
            result.Standings.Add(new PlayerResult { PlayerId = 0, DisplayName = "A", FinalScore = 10, TeamId = 0 });
            result.Standings.Add(new PlayerResult { PlayerId = 1, DisplayName = "B", FinalScore = 10, TeamId = 1 });
            result.TeamStandings.Add(new TeamResult { TeamId = 0, TotalScore = 10 });
            result.TeamStandings.Add(new TeamResult { TeamId = 1, TotalScore = 10 });

            var writer = new NetworkWriter();
            writer.WriteMatchResult(result);
            var reader = new NetworkReader(writer.ToArray());
            MatchResult restored = reader.ReadMatchResult();

            Assert.IsTrue(restored.IsDraw);
            Assert.AreEqual(-1, restored.WinnerPlayerId);
            Assert.AreEqual(-1, restored.WinnerTeamId);
            Assert.AreEqual(42, restored.DurationSeconds);
            Assert.AreEqual(2, restored.Standings.Count);
            Assert.AreEqual("B", restored.Standings[1].DisplayName);
            Assert.AreEqual(2, restored.TeamStandings.Count);
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
