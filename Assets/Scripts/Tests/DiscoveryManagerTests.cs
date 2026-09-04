using System.Collections.Generic;
using System.Net;
using AMath.Core;
using AMath.Core.Events;
using AMath.Networking.Discovery;
using NUnit.Framework;

namespace AMath.Tests
{
    public sealed class LanBroadcastTargetsTests
    {
        [Test]
        public void ComputeBroadcastAddress_ReturnsSubnetDirectedAddress()
        {
            var host = IPAddress.Parse("192.168.1.42");
            var mask = IPAddress.Parse("255.255.255.0");

            IPAddress broadcast = LanBroadcastTargets.ComputeBroadcastAddress(host, mask);

            Assert.AreEqual("192.168.1.255", broadcast.ToString());
        }

        [Test]
        public void ComputeBroadcastAddress_ReturnsNullForNonIpv4()
        {
            var host = IPAddress.Parse("192.168.1.1");
            var mask = IPAddress.IPv6Loopback;

            Assert.IsNull(LanBroadcastTargets.ComputeBroadcastAddress(host, mask));
            Assert.IsNull(LanBroadcastTargets.ComputeBroadcastAddress(null, IPAddress.Parse("255.255.255.0")));
        }

        [Test]
        public void GetEndpoints_IncludesGlobalBroadcast()
        {
            IReadOnlyList<IPEndPoint> endpoints = LanBroadcastTargets.GetEndpoints(DiscoveryManager.DiscoveryPort);

            Assert.IsNotEmpty(endpoints);
            Assert.IsTrue(
                ContainsAddress(endpoints, IPAddress.Broadcast),
                "Expected the global broadcast address to be included.");
        }

        private static bool ContainsAddress(IReadOnlyList<IPEndPoint> endpoints, IPAddress address)
        {
            foreach (IPEndPoint endpoint in endpoints)
            {
                if (endpoint.Address.Equals(address))
                    return true;
            }

            return false;
        }
    }

    public sealed class DiscoveryManagerTests
    {
        private EventBus _bus;
        private DiscoveryManager _discovery;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _discovery = new DiscoveryManager(_bus);
        }

        [TearDown]
        public void TearDown()
        {
            _discovery.Dispose();
        }

        [Test]
        public void TryResolveRoomCode_IsCaseInsensitive()
        {
            _discovery.TestReceiveAdvertisement("192.168.0.10", SampleAdvertisement("AB29KF"), 0f);

            Assert.IsTrue(_discovery.TryResolveRoomCode("ab29kf", out RoomInfo room));
            Assert.AreEqual("AB29KF", room.Advertisement.RoomCode);
        }

        [Test]
        public void TryResolveRoomCode_ReturnsFalseWhenMissing()
        {
            Assert.IsFalse(_discovery.TryResolveRoomCode("ZZZZZZ", out _));
            Assert.IsFalse(_discovery.TryResolveRoomCode(null, out _));
        }

        [Test]
        public void TestExpireStaleRooms_RemovesSilentHosts()
        {
            _discovery.TestReceiveAdvertisement("192.168.0.10", SampleAdvertisement("ROOM01"), 0f);
            _discovery.TestReceiveAdvertisement("192.168.0.11", SampleAdvertisement("ROOM02"), 0f);

            _discovery.TestExpireStaleRooms(DiscoveryManager.RoomStaleSeconds + 0.1f);

            Assert.AreEqual(0, _discovery.Rooms.Count);
        }

        [Test]
        public void TestReceiveAdvertisement_RefreshesExistingHostEntry()
        {
            _discovery.TestReceiveAdvertisement(
                "192.168.0.10",
                SampleAdvertisement("ROOM01", currentPlayers: 1),
                0f);
            _discovery.TestReceiveAdvertisement(
                "192.168.0.10",
                SampleAdvertisement("ROOM01", currentPlayers: 2),
                1f);

            Assert.AreEqual(1, _discovery.Rooms.Count);
            Assert.AreEqual(2, _discovery.Rooms[0].Advertisement.CurrentPlayers);
        }

        [Test]
        public void StartSearching_SetsSearchFailedWhenPortIsAlreadyBound()
        {
            using var blocker = new DiscoveryManager(_bus);
            blocker.StartSearching();
            Assert.IsFalse(blocker.SearchFailed);

            _discovery.StartSearching();

            Assert.IsTrue(_discovery.SearchFailed);
            Assert.IsFalse(_discovery.IsSearching);
        }

        private static RoomAdvertisement SampleAdvertisement(string roomCode, int currentPlayers = 1) => new()
        {
            RoomName = "Test Room",
            RoomCode = roomCode,
            CurrentPlayers = currentPlayers,
            MaxPlayers = 4,
            GameVersion = "test",
            Port = 7778,
            MatchInProgress = false
        };
    }
}
