using System.Collections.Generic;
using System;
using AMath.Core;
using AMath.Networking.HostMigration;
using AMath.Networking.RPC;
using AMath.Networking.Room;
using NUnit.Framework;
using Mirror;
using UnityEngine;

namespace AMath.Tests
{
    public sealed class RoomCodeAndMigrationTests
    {
        #region Room codes

        [Test]
        public void GeneratedCodes_HaveValidFormat()
        {
            for (int i = 0; i < 100; i++)
            {
                string code = RoomCodeGenerator.Generate();
                Assert.AreEqual(RoomCodeGenerator.CodeLength, code.Length);
                Assert.IsTrue(RoomCodeGenerator.IsValidFormat(code), $"Bad code: {code}");
            }
        }

        [Test]
        public void AmbiguousOrMalformedCodes_AreRejected()
        {
            Assert.IsFalse(RoomCodeGenerator.IsValidFormat(null));
            Assert.IsFalse(RoomCodeGenerator.IsValidFormat(""));
            Assert.IsFalse(RoomCodeGenerator.IsValidFormat("ABC"));        // too short
            Assert.IsFalse(RoomCodeGenerator.IsValidFormat("AB29KF7"));    // too long
            Assert.IsFalse(RoomCodeGenerator.IsValidFormat("AB29K0"));     // '0' excluded
            Assert.IsFalse(RoomCodeGenerator.IsValidFormat("AB29K1"));     // '1' excluded
            Assert.IsTrue(RoomCodeGenerator.IsValidFormat("ab29kf"));      // case-insensitive
        }

        #endregion

        #region Host succession ranking

        [Test]
        public void Ranking_PrefersLowestPing_ThenLowestSeat()
        {
            var table = new List<MigrationCandidate>
            {
                new() { PlayerId = 3, Address = "192.168.1.4", RttMs = 40 },
                new() { PlayerId = 1, Address = "192.168.1.2", RttMs = 12 },
                new() { PlayerId = 2, Address = "192.168.1.3", RttMs = 12 },
            };

            MigrationRanking.Sort(table);

            Assert.AreEqual(1, table[0].PlayerId); // 12ms, lower seat wins the tie
            Assert.AreEqual(2, table[1].PlayerId); // 12ms
            Assert.AreEqual(3, table[2].PlayerId); // 40ms

            Assert.AreEqual(0, MigrationRanking.RankOf(table, 1));
            Assert.AreEqual(2, MigrationRanking.RankOf(table, 3));
            Assert.AreEqual(-1, MigrationRanking.RankOf(table, 99));
        }

        [Test]
        public void Ranking_IsDeterministic_AcrossPeers()
        {
            // Two peers sorting the same replicated data must agree exactly —
            // this is what makes leaderless election safe.
            var peerA = new List<MigrationCandidate>
            {
                new() { PlayerId = 5, RttMs = 30 },
                new() { PlayerId = 4, RttMs = 30 },
                new() { PlayerId = 6, RttMs = 5 },
            };
            var peerB = new List<MigrationCandidate>(peerA);

            MigrationRanking.Sort(peerA);
            MigrationRanking.Sort(peerB);

            for (int i = 0; i < peerA.Count; i++)
                Assert.AreEqual(peerA[i].PlayerId, peerB[i].PlayerId);
        }

        [Test]
        public void ReconnectGrace_AllowsSeveralAttemptsWithinTheWindow()
        {
            Assert.AreEqual(10f, HostReconnectManager.GraceSeconds);

            // One attempt is not enough: the first try usually lands while
            // Mirror is still tearing the old client down.
            Assert.Greater(HostReconnectManager.MaxReconnectAttempts, 1);
        }

        [Test]
        public void LobbyFormat_RepeatedAndInvalidUpdatesAreIgnored()
        {
            var root = new GameObject("Network Player", typeof(NetworkIdentity));
            try
            {
                var player = root.AddComponent<NetworkPlayer>();

                Assert.IsTrue(player.TrySetLobbyMatchFormat(MatchFormat.Team));
                Assert.AreEqual(MatchFormat.Team, player.LobbyMatchFormat);
                Assert.IsFalse(player.TrySetLobbyMatchFormat(MatchFormat.Team));
                Assert.IsFalse(player.TrySetLobbyMatchFormat((MatchFormat)255));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LobbyTurnTime_RepeatedAndInvalidUpdatesAreIgnored()
        {
            var root = new GameObject("Network Player Turn Time", typeof(NetworkIdentity));
            try
            {
                var player = root.AddComponent<NetworkPlayer>();

                Assert.IsTrue(player.TrySetLobbyTurnTimePreset(TurnTimePreset.Long));
                Assert.AreEqual(TurnTimePreset.Long, player.LobbyTurnTimePreset);
                Assert.IsFalse(player.TrySetLobbyTurnTimePreset(TurnTimePreset.Long));
                Assert.AreEqual(TurnTimePreset.Long, player.LobbyTurnTimePreset);
                Assert.IsFalse(player.TrySetLobbyTurnTimePreset((TurnTimePreset)99));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LegacyHostMigrationName_RemainsACompatibilityWrapper()
        {
#pragma warning disable CS0618
            Type legacy = typeof(HostMigrationManager);
#pragma warning restore CS0618
            Assert.IsTrue(typeof(HostReconnectManager).IsAssignableFrom(legacy));
            Assert.IsNotNull(Attribute.GetCustomAttribute(legacy, typeof(ObsoleteAttribute)));
        }

        #endregion
    }
}
