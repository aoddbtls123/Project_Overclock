using System;
using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    public class ModifierEngineTests
    {
        private const double Tolerance = 1e-6;

        private readonly List<ModuleData> createdModules = new List<ModuleData>();

        [TearDown]
        public void DestroyCreatedModules()
        {
            foreach (ModuleData module in createdModules)
            {
                UnityEngine.Object.DestroyImmediate(module);
            }

            createdModules.Clear();
        }

        private ModuleData CreateModule(ModifierType type, GradeFilter target, float value)
        {
            var module = ScriptableObject.CreateInstance<ModuleData>();
            module.InitializeForTests(type.ToString(), new ModifierEffect(type, target, value));
            createdModules.Add(module);
            return module;
        }

        private static ModifierEngine CreateBaseEngine()
        {
            return new ModifierEngine(0.72, 0.25, 0.03);
        }

        private void EquipTimes(ModifierEngine engine, int count, ModifierType type, GradeFilter target, float value)
        {
            for (int i = 0; i < count; i++)
            {
                engine.Equip(CreateModule(type, target, value));
            }
        }

        private static void AssertTable(ModifierEngine engine, double expectedN, double expectedR, double expectedSR)
        {
            engine.GetProbabilities(out double n, out double r, out double sr);
            Assert.AreEqual(expectedN, n, Tolerance, "N");
            Assert.AreEqual(expectedR, r, Tolerance, "R");
            Assert.AreEqual(expectedSR, sr, Tolerance, "SR");
        }

        [Test]
        public void WithoutModules_ReturnsBaseValues()
        {
            var engine = CreateBaseEngine();

            AssertTable(engine, 0.72, 0.25, 0.03);
            Assert.AreEqual(1, engine.CalculateOutput(Grade.N, PartTag.None, 1, true));
            Assert.AreEqual(10, engine.CalculateOutput(Grade.SR, PartTag.None, 10, false));
            Assert.AreEqual(10, engine.DrawsPerRound(10));
        }

        [Test]
        public void SuperRareBonus_ShrinksOtherGradesProportionally()
        {
            var engine = CreateBaseEngine();

            engine.Equip(CreateModule(ModifierType.SuperRareChance, GradeFilter.Any, 0.05f));

            AssertTable(engine, 0.72 * 0.92 / 0.97, 0.25 * 0.92 / 0.97, 0.08);
        }

        [Test]
        public void SuperRareBonus_StacksAndStopsAtCap()
        {
            var engine = CreateBaseEngine();

            EquipTimes(engine, 2, ModifierType.SuperRareChance, GradeFilter.Any, 0.05f);
            AssertTable(engine, 0.72 * 0.87 / 0.97, 0.25 * 0.87 / 0.97, 0.13);

            EquipTimes(engine, 3, ModifierType.SuperRareChance, GradeFilter.Any, 0.05f);
            AssertTable(engine, 0.72 * 0.75 / 0.97, 0.25 * 0.75 / 0.97, 0.25);
        }

        [Test]
        public void NonProbabilityModules_KeepBaseTable()
        {
            var allSuperRare = new ModifierEngine(0.0, 0.0, 1.0);
            allSuperRare.Equip(CreateModule(ModifierType.OutputMultiplier, GradeFilter.Any, 0.3f));
            AssertTable(allSuperRare, 0.0, 0.0, 1.0);

            allSuperRare.Equip(CreateModule(ModifierType.SuperRareChance, GradeFilter.Any, 0.05f));
            AssertTable(allSuperRare, 0.0, 0.0, 1.0);

            var aboveCap = new ModifierEngine(0.5, 0.2, 0.3);
            aboveCap.Equip(CreateModule(ModifierType.OutputMultiplier, GradeFilter.Any, 0.3f));
            AssertTable(aboveCap, 0.5, 0.2, 0.3);
        }

        [Test]
        public void StreakBonus_IsAddedBeforeMultiplier()
        {
            var engine = CreateBaseEngine();
            engine.Equip(CreateModule(ModifierType.StreakBonus, GradeFilter.Any, 10f));
            engine.Equip(CreateModule(ModifierType.OutputMultiplier, GradeFilter.Any, 0.3f));

            Assert.AreEqual(14, engine.CalculateOutput(Grade.N, PartTag.None, 1, true));
            Assert.AreEqual(1, engine.CalculateOutput(Grade.N, PartTag.None, 1, false));
        }

        [Test]
        public void GradeMultiplier_AppliesOnlyToItsGrade_AndMultipliersAdd()
        {
            var engine = CreateBaseEngine();
            engine.Equip(CreateModule(ModifierType.OutputMultiplier, GradeFilter.SR, 1.0f));
            engine.Equip(CreateModule(ModifierType.OutputMultiplier, GradeFilter.Any, 0.3f));

            Assert.AreEqual(23, engine.CalculateOutput(Grade.SR, PartTag.None, 10, false));
            Assert.AreEqual(4, engine.CalculateOutput(Grade.R, PartTag.None, 3, false));

            engine.Equip(CreateModule(ModifierType.OutputMultiplier, GradeFilter.Any, 0.3f));

            Assert.AreEqual(26, engine.CalculateOutput(Grade.SR, PartTag.None, 10, false));
        }

        [Test]
        public void Multiplier_StopsAtCap()
        {
            var engine = CreateBaseEngine();

            EquipTimes(engine, 10, ModifierType.OutputMultiplier, GradeFilter.Any, 1.0f);

            Assert.AreEqual(80, engine.CalculateOutput(Grade.SR, PartTag.None, 10, false));
        }

        [Test]
        public void Output_RoundsHalfAwayFromZero()
        {
            var engine = CreateBaseEngine();

            engine.Equip(CreateModule(ModifierType.OutputMultiplier, GradeFilter.Any, 0.5f));

            Assert.AreEqual(5, engine.CalculateOutput(Grade.R, PartTag.None, 3, false));
        }

        [Test]
        public void ExtraDraws_StackAndStopAtCap()
        {
            var engine = CreateBaseEngine();

            engine.Equip(CreateModule(ModifierType.ExtraDraws, GradeFilter.Any, 2f));
            Assert.AreEqual(12, engine.DrawsPerRound(10));

            EquipTimes(engine, 9, ModifierType.ExtraDraws, GradeFilter.Any, 2f);
            Assert.AreEqual(20, engine.DrawsPerRound(10));
        }

        [Test]
        public void Equip_AppliesEveryEffectOfModule()
        {
            var engine = CreateBaseEngine();
            var module = ScriptableObject.CreateInstance<ModuleData>();
            module.InitializeForTests(
                "Combo",
                new ModifierEffect(ModifierType.SuperRareChance, GradeFilter.Any, 0.05f),
                new ModifierEffect(ModifierType.ExtraDraws, GradeFilter.Any, 2f));
            createdModules.Add(module);

            engine.Equip(module);

            AssertTable(engine, 0.72 * 0.92 / 0.97, 0.25 * 0.92 / 0.97, 0.08);
            Assert.AreEqual(12, engine.DrawsPerRound(10));
        }

        [Test]
        public void Equip_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => CreateBaseEngine().Equip(null));
        }
    }
}
