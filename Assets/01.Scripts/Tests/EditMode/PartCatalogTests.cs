using System;
using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    public class PartCatalogTests
    {
        private readonly List<PartData> createdParts = new List<PartData>();

        private PartData normal;
        private PartData rareFirst;
        private PartData rareSecond;
        private PartData superRare;
        private List<PartData> standardParts;

        [SetUp]
        public void CreateStandardParts()
        {
            normal = CreatePart("N1", Grade.N, 1);
            rareFirst = CreatePart("R1", Grade.R, 3);
            rareSecond = CreatePart("R2", Grade.R, 3);
            superRare = CreatePart("SR1", Grade.SR, 10);
            standardParts = new List<PartData> { normal, rareFirst, superRare, rareSecond };
        }

        [TearDown]
        public void DestroyCreatedParts()
        {
            foreach (PartData part in createdParts)
            {
                UnityEngine.Object.DestroyImmediate(part);
            }

            createdParts.Clear();
        }

        private PartData CreatePart(string displayName, Grade grade, int baseOutput)
        {
            var part = ScriptableObject.CreateInstance<PartData>();
            part.InitializeForTests(displayName, grade, baseOutput);
            createdParts.Add(part);
            return part;
        }

        [Test]
        public void Constructor_RejectsMissingGrade()
        {
            var parts = new List<PartData> { normal, rareFirst };

            Assert.Throws<ArgumentException>(() => new PartCatalog(parts, new RandomService(1)));
        }

        [Test]
        public void Select_ReturnsPartsOfEachGrade()
        {
            var catalog = new PartCatalog(standardParts, new RandomService(1));

            Assert.AreSame(normal, catalog.Select(Grade.N, 0.0));
            Assert.AreSame(rareFirst, catalog.Select(Grade.R, 0.0));
            Assert.AreSame(superRare, catalog.Select(Grade.SR, 0.0));
        }

        [Test]
        public void Select_MapsRollToEvenSlots()
        {
            var catalog = new PartCatalog(standardParts, new RandomService(1));

            Assert.AreSame(rareFirst, catalog.Select(Grade.R, 0.0));
            Assert.AreSame(rareFirst, catalog.Select(Grade.R, 0.5 - 1e-12));
            Assert.AreSame(rareSecond, catalog.Select(Grade.R, 0.5));
            Assert.AreSame(rareSecond, catalog.Select(Grade.R, 1.0 - 1e-12));
        }

        [Test]
        public void Pick_ConsumesOneRollFromInjectedRandomService()
        {
            var catalogRandom = new RandomService(7);
            var mirrorRandom = new RandomService(7);
            var catalog = new PartCatalog(standardParts, catalogRandom);

            for (int i = 0; i < 100; i++)
            {
                PartData expected = catalog.Select(Grade.R, mirrorRandom.NextDouble());
                Assert.AreSame(expected, catalog.Pick(Grade.R), "pick " + i);
            }

            Assert.AreEqual(mirrorRandom.State, catalogRandom.State);
        }
    }
}
