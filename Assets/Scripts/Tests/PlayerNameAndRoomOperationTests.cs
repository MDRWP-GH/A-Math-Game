using System;
using AMath.Core;
using AMath.Core.Identity;
using AMath.Core.Events;
using AMath.Networking;
using AMath.Networking.Discovery;
using AMath.Networking.Room;
using AMath.Settings;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AMath.Tests
{
    public sealed class PlayerNameValidatorTests
    {
        [TestCase("  Math Kid_2  ", "Math Kid_2")]
        [TestCase("  นักคณิต-01  ", "นักคณิต-01")]
        public void TryNormalize_AcceptsSupportedNamesAndTrims(string raw, string expected)
        {
            Assert.IsTrue(PlayerNameValidator.TryNormalize(raw, out string normalized, out var error));
            Assert.AreEqual(PlayerNameValidationError.None, error);
            Assert.AreEqual(expected, normalized);
        }

        [TestCase(null, PlayerNameValidationError.Required)]
        [TestCase("", PlayerNameValidationError.Required)]
        [TestCase("   ", PlayerNameValidationError.Required)]
        [TestCase("Player@Home", PlayerNameValidationError.UnsupportedCharacter)]
        [TestCase("Player🙂", PlayerNameValidationError.UnsupportedCharacter)]
        [TestCase("Line\nBreak", PlayerNameValidationError.UnsupportedCharacter)]
        [TestCase("Name\n", PlayerNameValidationError.UnsupportedCharacter)]
        [TestCase("\tName", PlayerNameValidationError.UnsupportedCharacter)]
        public void TryNormalize_RejectsInvalidNames(string raw, PlayerNameValidationError expected)
        {
            Assert.IsFalse(PlayerNameValidator.TryNormalize(raw, out string normalized, out var error));
            Assert.AreEqual(string.Empty, normalized);
            Assert.AreEqual(expected, error);
        }

        [Test]
        public void TryNormalize_RejectsMoreThanTwentyFourCharacters()
        {
            Assert.IsFalse(PlayerNameValidator.TryNormalize(new string('A', 25), out string normalized, out var error));
            Assert.AreEqual(string.Empty, normalized);
            Assert.AreEqual(PlayerNameValidationError.TooLong, error);
        }

        [Test]
        public void TryNormalize_AcceptsBothLengthBoundaries()
        {
            Assert.IsTrue(PlayerNameValidator.TryNormalize("A", out string shortest, out _));
            Assert.AreEqual(1, shortest.Length);
            Assert.IsTrue(PlayerNameValidator.TryNormalize(new string('Z', 24), out string longest, out _));
            Assert.AreEqual(24, longest.Length);
        }
    }

    public sealed class PlayerProfileNameTests
    {
        private const string Prefix = "amath.general.playerName.profile.";
        private const string MigrationKey = "amath.general.playerName.profileMigrated";
        private string _first;
        private string _second;
        private bool _hadMigrationMarker;
        private int _migrationMarker;

        [SetUp]
        public void SetUp()
        {
            _first = "test-" + Guid.NewGuid().ToString("N");
            _second = "test-" + Guid.NewGuid().ToString("N");
            _hadMigrationMarker = PlayerPrefs.HasKey(MigrationKey);
            _migrationMarker = PlayerPrefs.GetInt(MigrationKey, 0);
            PlayerPrefs.SetInt(MigrationKey, 1);
        }

        [TearDown]
        public void TearDown()
        {
            GameSettings.ClearPlayerProfile();
            PlayerPrefs.DeleteKey(Prefix + _first);
            PlayerPrefs.DeleteKey(Prefix + _second);
            if (_hadMigrationMarker)
                PlayerPrefs.SetInt(MigrationKey, _migrationMarker);
            else
                PlayerPrefs.DeleteKey(MigrationKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void SwitchingAccounts_LoadsEachAccountsOwnPlayerName()
        {
            GameSettings.UsePlayerProfile(_first);
            Assert.AreEqual(string.Empty, GameSettings.PlayerName);
            Assert.IsTrue(GameSettings.TrySetPlayerName("  First Hero  ", out _));

            GameSettings.UsePlayerProfile(_second);
            Assert.AreEqual(string.Empty, GameSettings.PlayerName,
                "A new account must not inherit the username or another account's player name.");
            Assert.IsTrue(GameSettings.TrySetPlayerName("Second_Hero", out _));

            GameSettings.UsePlayerProfile(_first);
            Assert.AreEqual("First Hero", GameSettings.PlayerName);
            GameSettings.UsePlayerProfile(_second);
            Assert.AreEqual("Second_Hero", GameSettings.PlayerName);
        }

        [Test]
        public void InvalidPlayerName_IsNotPersisted()
        {
            GameSettings.UsePlayerProfile(_first);
            Assert.IsFalse(GameSettings.TrySetPlayerName("Bad@Name", out var error));
            Assert.AreEqual(PlayerNameValidationError.UnsupportedCharacter, error);
            Assert.IsFalse(PlayerPrefs.HasKey(Prefix + _first));
        }

        [Test]
        public void InvalidEdit_DoesNotOverwriteTheLastValidName()
        {
            GameSettings.UsePlayerProfile(_first);
            Assert.IsTrue(GameSettings.TrySetPlayerName("Stable Name", out _));

            Assert.IsFalse(GameSettings.TrySetPlayerName(new string('X', 25), out var error));
            Assert.AreEqual(PlayerNameValidationError.TooLong, error);
            Assert.AreEqual("Stable Name", GameSettings.PlayerName);
            Assert.AreEqual("Stable Name", PlayerPrefs.GetString(Prefix + _first));
        }
    }

    public sealed class RoomOperationDecisionTests
    {
        [Test]
        public void CreateDecision_DistinguishesDuplicateAndNetworkFailure()
        {
            Assert.AreEqual(RoomOperationError.AlreadyInSession,
                RoomManager.EvaluateCreateAvailability(true, false, false, true));
            Assert.AreEqual(RoomOperationError.NetworkUnavailable,
                RoomManager.EvaluateCreateAvailability(false, false, false, false));
            Assert.AreEqual(RoomOperationError.None,
                RoomManager.EvaluateCreateAvailability(false, false, false, true));
        }

        [Test]
        public void JoinDecision_DistinguishesInvalidFullStartedAndNetworkFailure()
        {
            Assert.AreEqual(RoomOperationError.NetworkUnavailable,
                RoomManager.EvaluateJoinAvailability(null, false, false, false, false, false));
            Assert.AreEqual(RoomOperationError.InvalidRoom,
                RoomManager.EvaluateJoinAvailability(null, false, false, false, true, false));
            Assert.AreEqual(RoomOperationError.RoomFull,
                RoomManager.EvaluateJoinAvailability(Room(players: 4), false, false, false, true, false));
            Assert.AreEqual(RoomOperationError.MatchStarted,
                RoomManager.EvaluateJoinAvailability(Room(players: 2, started: true), false, false, false, true, false));
            Assert.AreEqual(RoomOperationError.None,
                RoomManager.EvaluateJoinAvailability(Room(players: 2), false, false, false, true, false));
        }

        [TestCase(RoomOperationError.RoomFull, "ui.play.err_room_full")]
        [TestCase(RoomOperationError.MatchStarted, "ui.play.err_match_started")]
        [TestCase(RoomOperationError.RoomClosed, "ui.play.err_room_closed")]
        [TestCase(RoomOperationError.NetworkUnavailable, "ui.play.err_network")]
        [TestCase(RoomOperationError.TransportFailed, "ui.play.err_transport")]
        public void RoomErrors_HaveSpecificMessages(RoomOperationError error, string key)
        {
            Assert.AreEqual(key, RoomOperationErrorText.LocalizationKey(error));
        }

        [TestCase(TransportError.Refused, true, false, true, RoomOperationError.RoomClosed)]
        [TestCase(TransportError.Timeout, true, false, true, RoomOperationError.RoomClosed)]
        [TestCase(TransportError.ConnectionClosed, true, false, true, RoomOperationError.RoomClosed)]
        [TestCase(TransportError.Unexpected, true, false, true, RoomOperationError.TransportFailed)]
        [TestCase(TransportError.Timeout, false, false, true, RoomOperationError.NetworkUnavailable)]
        [TestCase(TransportError.Timeout, true, true, false, RoomOperationError.None)]
        public void ClientTransportErrors_MapWithoutInterruptingMatchRecovery(
            TransportError transportError,
            bool networkAvailable,
            bool matchRunning,
            bool expectedMapped,
            RoomOperationError expectedError)
        {
            bool mapped = AMathNetworkManager.TryMapInitialRoomError(
                transportError, networkAvailable, matchRunning, out RoomOperationError error);
            Assert.AreEqual(expectedMapped, mapped);
            Assert.AreEqual(expectedError, error);
        }

        [Test]
        public void JoinDecision_RejectsMalformedAdvertisementFields()
        {
            RoomInfo invalidPort = Room(players: 1);
            invalidPort.Advertisement.Port = 70000;
            RoomInfo invalidCode = Room(players: 1);
            invalidCode.Advertisement.RoomCode = "BAD";
            RoomInfo invalidCapacity = Room(players: 1);
            invalidCapacity.Advertisement.MaxPlayers = 99;

            Assert.AreEqual(RoomOperationError.InvalidRoom,
                RoomManager.EvaluateJoinAvailability(invalidPort, false, false, false, true, false));
            Assert.AreEqual(RoomOperationError.InvalidRoom,
                RoomManager.EvaluateJoinAvailability(invalidCode, false, false, false, true, false));
            Assert.AreEqual(RoomOperationError.InvalidRoom,
                RoomManager.EvaluateJoinAvailability(invalidCapacity, false, false, false, true, false));
        }

        [Test]
        public void CreateTransportFailure_RollsBackRoomSession()
        {
            var bus = new EventBus();
            var session = new RoomSession();
            var discovery = new DiscoveryManager(bus);
            var owner = new GameObject("Room transport rollback test");
            var networkManager = owner.AddComponent<AMathNetworkManager>();
            var roomManager = new RoomManager(
                bus, session, discovery, null, networkManager, () => true);

            try
            {
                LogAssert.Expect(LogType.Error, "[Room] Active transport is not KcpTransport.");
                LogAssert.Expect(LogType.Error,
                    new System.Text.RegularExpressions.Regex("^\\[Room\\] Could not start host:"));

                Assert.IsFalse(roomManager.TryCreateRoom("Test", 4, out var error));
                Assert.AreEqual(RoomOperationError.TransportFailed, error);
                Assert.IsFalse(session.IsActive);
                Assert.IsFalse(session.IsHost);
                Assert.IsNull(session.RoomCode);
                Assert.IsFalse(discovery.IsAdvertising);
            }
            finally
            {
                roomManager.Dispose();
                discovery.Dispose();
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [TestCase(MatchFormat.Individual)]
        [TestCase(MatchFormat.Team)]
        public void BuildMatchConfig_PreservesHumanSeatsWithoutAddingAi(MatchFormat format)
        {
            var humans = new[]
            {
                new PlayerIdentity
                {
                    PlayerId = 99,
                    PersistentGuid = "host",
                    DisplayName = "Host",
                    TeamId = 0,
                    ColorId = 2
                },
                new PlayerIdentity
                {
                    PlayerId = 42,
                    PersistentGuid = "guest",
                    DisplayName = "Guest",
                    TeamId = 1,
                    ColorId = 3
                }
            };

            MatchConfig config = RoomManager.BuildMatchConfig(
                humans,
                format,
                randomSeed: 12345,
                turnSeconds: GameRules.DefaultTurnSeconds,
                gameVersion: "test");

            Assert.AreEqual(2, config.Players.Count);
            Assert.IsFalse(config.Players[0].IsAi || config.Players[1].IsAi);
            Assert.AreEqual(0, config.Players[0].PlayerId);
            Assert.AreEqual(1, config.Players[1].PlayerId);
            Assert.AreEqual("host", config.Players[0].PersistentGuid);
            Assert.AreEqual("guest", config.Players[1].PersistentGuid);
            Assert.AreEqual(format == MatchFormat.Team ? 0 : -1, config.Players[0].TeamId);
            Assert.AreEqual(format == MatchFormat.Team ? 1 : -1, config.Players[1].TeamId);
        }

        [Test]
        public void JoinTransportFailure_RollsBackRoomSession()
        {
            var bus = new EventBus();
            var session = new RoomSession();
            var discovery = new DiscoveryManager(bus);
            var owner = new GameObject("Join transport rollback test");
            var networkManager = owner.AddComponent<AMathNetworkManager>();
            var roomManager = new RoomManager(
                bus, session, discovery, null, networkManager, () => true);

            try
            {
                LogAssert.Expect(LogType.Error, "[Room] Active transport is not KcpTransport.");
                LogAssert.Expect(LogType.Error,
                    new System.Text.RegularExpressions.Regex("^\\[Room\\] Could not start client:"));

                Assert.IsFalse(roomManager.TryJoinRoom(Room(players: 2), out var error));
                Assert.AreEqual(RoomOperationError.TransportFailed, error);
                Assert.IsFalse(session.IsActive);
                Assert.IsFalse(session.IsHost);
                Assert.IsNull(session.RoomCode);
            }
            finally
            {
                roomManager.Dispose();
                discovery.Dispose();
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static RoomInfo Room(int players, bool started = false) => new()
        {
            HostAddress = "192.168.1.20",
            Advertisement = new RoomAdvertisement
            {
                RoomName = "Same name is allowed",
                RoomCode = "ABC234",
                CurrentPlayers = players,
                MaxPlayers = 4,
                Port = 7778,
                MatchInProgress = started
            }
        };
    }
}
