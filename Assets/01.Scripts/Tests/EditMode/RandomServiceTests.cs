using System;
using NUnit.Framework;
using Overclock.Core;

namespace Overclock.Tests
{
    public class RandomServiceTests
    {
        private const int SampleCount = 1000;

        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var first = new RandomService(12345);
            var second = new RandomService(12345);

            for (int i = 0; i < SampleCount; i++)
            {
                Assert.AreEqual(first.NextDouble(), second.NextDouble(), "index " + i);
            }
        }

        [Test]
        public void AdjacentSeeds_ProduceDifferentSequences()
        {
            var first = new RandomService(1);
            var second = new RandomService(2);
            int sameCount = 0;

            for (int i = 0; i < SampleCount; i++)
            {
                if (first.NextDouble() == second.NextDouble())
                {
                    sameCount++;
                }
            }

            Assert.AreEqual(0, sameCount);
        }

        [Test]
        public void NextDouble_StaysInUnitRange()
        {
            var service = new RandomService(42);

            for (int i = 0; i < SampleCount; i++)
            {
                double value = service.NextDouble();
                Assert.IsTrue(value >= 0.0 && value < 1.0, "index " + i + " value " + value);
            }
        }

        [Test]
        public void RestoredState_ContinuesSameSequence()
        {
            var original = new RandomService(777);
            for (int i = 0; i < 500; i++)
            {
                original.NextDouble();
            }

            uint savedState = original.State;

            var expected = new double[500];
            for (int i = 0; i < expected.Length; i++)
            {
                expected[i] = original.NextDouble();
            }

            var restored = new RandomService(1);
            restored.RestoreState(savedState);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], restored.NextDouble(), "index " + i);
            }

            Assert.AreNotEqual(expected[0], new RandomService(777).NextDouble());
        }

        [Test]
        public void SeedZero_ProducesVaryingValues()
        {
            var service = new RandomService(0);
            double firstValue = service.NextDouble();
            bool hasDifferentValue = false;

            for (int i = 0; i < 10; i++)
            {
                if (service.NextDouble() != firstValue)
                {
                    hasDifferentValue = true;
                }
            }

            Assert.IsTrue(hasDifferentValue);
            Assert.Throws<ArgumentException>(() => service.RestoreState(0));
        }

        [Test]
        public void MaxSeed_IsRejected()
        {
            var exception = Assert.Throws<ArgumentException>(() => new RandomService(uint.MaxValue));
            Assert.AreEqual("seed", exception.ParamName);
        }
    }
}
