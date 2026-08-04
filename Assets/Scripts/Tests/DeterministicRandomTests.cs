using System.Collections.Generic;
using AMath.Core.RandomNumbers;
using NUnit.Framework;

namespace AMath.Tests
{
    /// <summary>
    /// Determinism is the foundation of sync, replay and migration — these
    /// tests guard the exact contracts the architecture relies on.
    /// </summary>
    public sealed class DeterministicRandomTests
    {
        [Test]
        public void SameSeed_ProducesIdenticalSequence()
        {
            var a = new DeterministicRandom(12345);
            var b = new DeterministicRandom(12345);

            for (int i = 0; i < 1000; i++)
                Assert.AreEqual(a.NextInt(100), b.NextInt(100));
        }

        [Test]
        public void DifferentSeeds_Diverge()
        {
            var a = new DeterministicRandom(1);
            var b = new DeterministicRandom(2);

            bool anyDifferent = false;
            for (int i = 0; i < 100; i++)
                anyDifferent |= a.NextInt(1000) != b.NextInt(1000);

            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void RestoredState_ContinuesSequenceExactly()
        {
            var original = new DeterministicRandom(777);
            for (int i = 0; i < 50; i++)
                original.NextInt(100);

            DeterministicRandom restored = DeterministicRandom.FromState(original.State);

            for (int i = 0; i < 100; i++)
                Assert.AreEqual(original.NextInt(100), restored.NextInt(100));
        }

        [Test]
        public void Shuffle_IsDeterministicForSameSeed()
        {
            var listA = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var listB = new List<int>(listA);

            new DeterministicRandom(42).Shuffle(listA);
            new DeterministicRandom(42).Shuffle(listB);

            CollectionAssert.AreEqual(listA, listB);
        }
    }
}
