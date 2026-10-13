using System;
using NUnit.Framework;
using Overclock.Core;

namespace Overclock.Tests
{
    public class GachaEngineTests
    {
        private const int BasePityThreshold = 50;
        private const int NoPity = int.MaxValue;
        private const int LargeSampleCount = 1000000;

        private static GachaEngine CreateBaseEngine(int pityThreshold)
        {
            return new GachaEngine(new RandomService(2026), pityThreshold, 0.72, 0.25, 0.03);
        }

        [Test]
        public void Resolve_SplitsAtCumulativeBoundaries()
        {
            var engine = CreateBaseEngine(NoPity);

            Assert.AreEqual(Grade.N, engine.Resolve(0.0));
            Assert.AreEqual(Grade.N, engine.Resolve(0.72 - 1e-12));
            Assert.AreEqual(Grade.R, engine.Resolve(0.72));
            Assert.AreEqual(Grade.R, engine.Resolve(0.97 - 1e-12));
            Assert.AreEqual(Grade.SR, engine.Resolve(0.97));
            Assert.AreEqual(Grade.SR, engine.Resolve(1.0 - 1e-12));
        }

        [Test]
        public void WithoutPity_MatchesBaseProbabilities()
        {
            var engine = CreateBaseEngine(NoPity);
            var counts = new int[3];

            for (int i = 0; i < LargeSampleCount; i++)
            {
                counts[(int)engine.Draw()]++;
            }

            Assert.AreEqual(0.72, counts[(int)Grade.N] / (double)LargeSampleCount, 0.002);
            Assert.AreEqual(0.25, counts[(int)Grade.R] / (double)LargeSampleCount, 0.002);
            Assert.AreEqual(0.03, counts[(int)Grade.SR] / (double)LargeSampleCount, 0.002);
        }

        [Test]
        public void WithPity_SuperRareRatioIsAbout3_80Percent()
        {
            var engine = CreateBaseEngine(BasePityThreshold);
            int superRareCount = 0;

            for (int i = 0; i < LargeSampleCount; i++)
            {
                if (engine.Draw() == Grade.SR)
                {
                    superRareCount++;
                }
            }

            Assert.AreEqual(0.03805, superRareCount / (double)LargeSampleCount, 0.001);
        }

        [Test]
        public void Pity_ForcesSuperRareRightAfterThreshold()
        {
            var engine = new GachaEngine(new RandomService(1), 3, 1.0, 0.0, 0.0);
            Grade[] expected = { Grade.N, Grade.N, Grade.N, Grade.SR, Grade.N, Grade.N, Grade.N, Grade.SR };

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], engine.Draw(), "draw " + (i + 1));
            }

            Assert.AreEqual(0, engine.DrawsWithoutSuperRare);
        }

        [Test]
        public void NaturalSuperRare_ResetsPityCounter()
        {
            var engine = new GachaEngine(new RandomService(1), 3, 1.0, 0.0, 0.0);
            engine.Draw();
            engine.Draw();
            Assert.AreEqual(2, engine.DrawsWithoutSuperRare);

            engine.SetProbabilities(0.0, 0.0, 1.0);

            Assert.AreEqual(Grade.SR, engine.Draw());
            Assert.AreEqual(0, engine.DrawsWithoutSuperRare);
        }

        [Test]
        public void SetProbabilities_ChangesTableAfterConstruction()
        {
            var engine = CreateBaseEngine(NoPity);

            engine.SetProbabilities(0.0, 1.0, 0.0);

            Assert.AreEqual(Grade.R, engine.Resolve(0.5));
            Assert.AreEqual(Grade.R, engine.Draw());
        }

        [TestCase(0.70, 0.25, 0.03)]
        [TestCase(0.80, 0.25, -0.05)]
        [TestCase(double.NaN, 0.25, 0.03)]
        public void SetProbabilities_RejectsInvalidTable(double n, double r, double sr)
        {
            var engine = CreateBaseEngine(NoPity);
            Assert.Throws<ArgumentException>(() => engine.SetProbabilities(n, r, sr));
        }

        [Test]
        public void GetProbability_ReflectsCurrentTable()
        {
            var engine = CreateBaseEngine(NoPity);
            Assert.AreEqual(0.72, engine.GetProbability(Grade.N));
            Assert.AreEqual(0.25, engine.GetProbability(Grade.R));
            Assert.AreEqual(0.03, engine.GetProbability(Grade.SR));

            engine.SetProbabilities(0.68, 0.24, 0.08);

            Assert.AreEqual(0.68, engine.GetProbability(Grade.N));
            Assert.AreEqual(0.24, engine.GetProbability(Grade.R));
            Assert.AreEqual(0.08, engine.GetProbability(Grade.SR));
        }

        [TestCase(3)]
        [TestCase(50)]
        public void PityThreshold_ReturnsConstructorValue(int threshold)
        {
            Assert.AreEqual(threshold, CreateBaseEngine(threshold).PityThreshold);
        }
    }
}
