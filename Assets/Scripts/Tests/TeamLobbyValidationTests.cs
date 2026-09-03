using AMath.Core;
using NUnit.Framework;

namespace AMath.Tests
{
    /// <summary>
    /// Team-mode lobby validation for 2–4 human players with self-selected teams.
    /// </summary>
    public sealed class TeamLobbyValidationTests
    {
        [Test]
        public void TwoPlayers_OnePerTeam_IsValid()
        {
            Assert.IsTrue(GameRules.IsValidHumanRoster(2));
            Assert.IsTrue(GameRules.IsValidTeamSplit(1, 1));
        }

        [Test]
        public void ThreePlayers_TwoVsOne_IsValid()
        {
            Assert.IsTrue(GameRules.IsValidHumanRoster(3));
            Assert.IsTrue(GameRules.IsValidTeamSplit(2, 1));
            Assert.IsTrue(GameRules.IsValidTeamSplit(1, 2));
        }

        [Test]
        public void FourPlayers_TwoVsTwo_IsValid()
        {
            Assert.IsTrue(GameRules.IsValidHumanRoster(4));
            Assert.IsTrue(GameRules.IsValidTeamSplit(2, 2));
        }

        [Test]
        public void FourPlayers_OneVsThree_IsValid()
        {
            Assert.IsTrue(GameRules.IsValidTeamSplit(1, 3));
            Assert.IsTrue(GameRules.IsValidTeamSplit(3, 1));
        }

        [Test]
        public void AllOnSameTeam_IsBlocked()
        {
            Assert.IsTrue(GameRules.IsValidHumanRoster(3));
            Assert.IsFalse(GameRules.IsValidTeamSplit(3, 0));
            Assert.IsFalse(GameRules.IsValidTeamSplit(4, 0));
        }

        [Test]
        public void SoloHost_IsBlocked()
        {
            Assert.IsFalse(GameRules.IsValidHumanRoster(1));
        }

        [Test]
        public void FiveHumans_ExceedsRoomCap()
        {
            Assert.IsFalse(GameRules.IsValidHumanRoster(5));
            Assert.AreEqual(GameRules.MaxPlayers, 4);
        }
    }
}
