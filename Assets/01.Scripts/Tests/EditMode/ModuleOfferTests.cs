using System;
using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    public class ModuleOfferTests
    {
        private const int SampleCount = 10000;

        private readonly List<ModuleData> createdModules = new List<ModuleData>();

        private ModuleData calibrator;
        private ModuleData amplifierCoil;
        private ModuleData selectiveAmplifier;
        private ModuleData syncCircuit;
        private ModuleData overloadCircuit;
        private List<ModuleData> standardPool;

        [SetUp]
        public void CreateStandardPool()
        {
            calibrator = CreateModule("Calibrator", ModuleAxis.Probability);
            amplifierCoil = CreateModule("AmplifierCoil", ModuleAxis.Multiplier);
            selectiveAmplifier = CreateModule("SelectiveAmplifier", ModuleAxis.Multiplier);
            syncCircuit = CreateModule("SyncCircuit", ModuleAxis.Conditional);
            overloadCircuit = CreateModule("OverloadCircuit", ModuleAxis.Draws);
            standardPool = new List<ModuleData> { calibrator, amplifierCoil, selectiveAmplifier, syncCircuit, overloadCircuit };
        }

        [TearDown]
        public void DestroyCreatedModules()
        {
            foreach (ModuleData module in createdModules)
            {
                UnityEngine.Object.DestroyImmediate(module);
            }

            createdModules.Clear();
        }

        private ModuleData CreateModule(string displayName, ModuleAxis axis)
        {
            var module = ScriptableObject.CreateInstance<ModuleData>();
            module.InitializeForTests(displayName, axis);
            createdModules.Add(module);
            return module;
        }

        [Test]
        public void Offer_HasThreeModulesWithDistinctAxes()
        {
            var offer = new ModuleOffer(standardPool, new RandomService(2026));

            for (int i = 0; i < SampleCount; i++)
            {
                List<ModuleData> cards = offer.CreateOffer();
                Assert.AreEqual(3, cards.Count, "offer " + i);

                var seenAxes = new HashSet<ModuleAxis>();
                foreach (ModuleData card in cards)
                {
                    Assert.IsTrue(seenAxes.Add(card.Axis), "offer " + i + " duplicated axis " + card.Axis);
                }
            }
        }

        [Test]
        public void Offer_PicksAxisFirstThenModuleWithinAxis()
        {
            var offer = new ModuleOffer(standardPool, new RandomService(2026));
            var counts = new Dictionary<ModuleData, int>();
            foreach (ModuleData module in standardPool)
            {
                counts[module] = 0;
            }

            for (int i = 0; i < SampleCount; i++)
            {
                foreach (ModuleData card in offer.CreateOffer())
                {
                    counts[card]++;
                }
            }

            // 축 4개 중 3개가 나오므로 축마다 0.75. 배수 축은 모듈 2개가 나눠 각 0.375
            Assert.AreEqual(0.75, counts[calibrator] / (double)SampleCount, 0.02, "calibrator");
            Assert.AreEqual(0.375, counts[amplifierCoil] / (double)SampleCount, 0.02, "amplifierCoil");
            Assert.AreEqual(0.375, counts[selectiveAmplifier] / (double)SampleCount, 0.02, "selectiveAmplifier");
            Assert.AreEqual(0.75, counts[syncCircuit] / (double)SampleCount, 0.02, "syncCircuit");
            Assert.AreEqual(0.75, counts[overloadCircuit] / (double)SampleCount, 0.02, "overloadCircuit");
        }

        [Test]
        public void Offer_WithFewerAxes_OffersOnePerAxis()
        {
            var pool = new List<ModuleData> { amplifierCoil, selectiveAmplifier, calibrator };
            var offer = new ModuleOffer(pool, new RandomService(1));

            for (int i = 0; i < 100; i++)
            {
                List<ModuleData> cards = offer.CreateOffer();
                Assert.AreEqual(2, cards.Count, "offer " + i);
                Assert.AreNotEqual(cards[0].Axis, cards[1].Axis, "offer " + i);
            }
        }

        [Test]
        public void Offer_ConsumesTwoRollsPerCardFromInjectedRandomService()
        {
            var offerRandom = new RandomService(7);
            var mirrorRandom = new RandomService(7);
            var offer = new ModuleOffer(standardPool, offerRandom);

            for (int i = 0; i < 20; i++)
            {
                offer.CreateOffer();
                for (int roll = 0; roll < 6; roll++)
                {
                    mirrorRandom.NextDouble();
                }

                Assert.AreEqual(mirrorRandom.State, offerRandom.State, "offer " + i);
            }
        }

        [Test]
        public void Offer_CardsFollowInjectedRandomService()
        {
            var offerRandom = new RandomService(7);
            var mirrorRandom = new RandomService(7);
            var offer = new ModuleOffer(standardPool, offerRandom);
            var modulesByAxis = new List<ModuleData>[]
            {
                new List<ModuleData> { calibrator },
                new List<ModuleData> { amplifierCoil, selectiveAmplifier },
                new List<ModuleData> { syncCircuit },
                new List<ModuleData> { overloadCircuit }
            };

            for (int i = 0; i < 50; i++)
            {
                var remainingAxes = new List<int> { 0, 1, 2, 3 };
                List<ModuleData> cards = offer.CreateOffer();

                for (int card = 0; card < cards.Count; card++)
                {
                    int axisSlot = (int)(mirrorRandom.NextDouble() * remainingAxes.Count);
                    List<ModuleData> candidates = modulesByAxis[remainingAxes[axisSlot]];
                    remainingAxes.RemoveAt(axisSlot);
                    ModuleData expected = candidates[(int)(mirrorRandom.NextDouble() * candidates.Count)];

                    Assert.AreSame(expected, cards[card], "offer " + i + " card " + card);
                }
            }
        }

        [Test]
        public void SameSeed_ProducesSameOffers()
        {
            var first = new ModuleOffer(standardPool, new RandomService(99));
            var second = new ModuleOffer(standardPool, new RandomService(99));

            for (int i = 0; i < 100; i++)
            {
                List<ModuleData> a = first.CreateOffer();
                List<ModuleData> b = second.CreateOffer();
                for (int card = 0; card < a.Count; card++)
                {
                    Assert.AreSame(a[card], b[card], "offer " + i + " card " + card);
                }
            }
        }

        [Test]
        public void Constructor_RejectsInvalidPool()
        {
            var random = new RandomService(1);

            Assert.Throws<ArgumentNullException>(() => new ModuleOffer(null, random));
            Assert.Throws<ArgumentNullException>(() => new ModuleOffer(standardPool, null));
            Assert.Throws<ArgumentException>(() => new ModuleOffer(new List<ModuleData>(), random));
            Assert.Throws<ArgumentException>(() => new ModuleOffer(new List<ModuleData> { calibrator, null }, random));
        }
    }
}
