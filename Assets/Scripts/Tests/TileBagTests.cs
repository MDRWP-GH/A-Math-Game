using System.Collections.Generic;
using AMath.Core.RandomNumbers;
using AMath.Gameplay.Board;
using NUnit.Framework;

namespace AMath.Tests
{
    public sealed class TileBagTests
    {
        [Test]
        public void FullSet_Contains100Tiles()
        {
            Assert.AreEqual(100, AMathTileSet.CreateFullSet().Count);
        }

        [Test]
        public void Reset_IsDeterministicForSameSeed()
        {
            var bagA = new TileBag();
            var bagB = new TileBag();
            bagA.Reset(new DeterministicRandom(99));
            bagB.Reset(new DeterministicRandom(99));

            CollectionAssert.AreEqual(bagA.ExportContents(), bagB.ExportContents());
        }

        [Test]
        public void Draw_ReducesCount_AndStopsWhenEmpty()
        {
            var bag = new TileBag();
            bag.Reset(new DeterministicRandom(1));

            var drawn = new List<byte>();
            bag.Draw(8, drawn);
            Assert.AreEqual(8, drawn.Count);
            Assert.AreEqual(92, bag.Count);

            drawn.Clear();
            bag.Draw(200, drawn);
            Assert.AreEqual(92, drawn.Count);
            Assert.AreEqual(0, bag.Count);
        }

        [Test]
        public void Exchange_PreservesTotalTileCount()
        {
            var rng = new DeterministicRandom(5);
            var bag = new TileBag();
            bag.Reset(rng);

            var rack = new List<byte>();
            bag.Draw(8, rack);

            var returned = new List<byte> { rack[0], rack[1], rack[2] };
            var drawn = new List<byte>();
            bag.Exchange(returned, drawn, rng);

            Assert.AreEqual(3, drawn.Count);
            Assert.AreEqual(92, bag.Count); // 100 - 8 drawn - 3 taken + 3 returned
        }
    }
}
