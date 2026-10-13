using System;
using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    public class ModulePreviewTests
    {
        private const int NoPity = int.MaxValue;
        private const double Tolerance = 1e-9;

        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

        private PartData normal;
        private PartData rare;
        private PartData superRare;

        [SetUp]
        public void CreateParts()
        {
            normal = CreatePart("N1", Grade.N, 1);
            rare = CreatePart("R1", Grade.R, 3);
            superRare = CreatePart("SR1", Grade.SR, 10);
        }

        [TearDown]
        public void DestroyCreatedObjects()
        {
            foreach (UnityEngine.Object created in createdObjects)
            {
                UnityEngine.Object.DestroyImmediate(created);
            }

            createdObjects.Clear();
        }

        private PartData CreatePart(string displayName, Grade grade, int baseOutput)
        {
            var part = ScriptableObject.CreateInstance<PartData>();
            part.InitializeForTests(displayName, grade, baseOutput);
            createdObjects.Add(part);
            return part;
        }

        private ModuleData CreateModule(ModifierType type, GradeFilter target, float value)
        {
            var module = ScriptableObject.CreateInstance<ModuleData>();
            module.InitializeForTests(type.ToString(), new ModifierEffect(type, target, value));
            createdObjects.Add(module);
            return module;
        }

        private RunManager CreateRun(GachaEngine engine, RandomService random, int drawsPerRound = 10)
        {
            var catalog = new PartCatalog(new List<PartData> { normal, rare, superRare }, random);
            return new RunManager(engine, catalog, 8, drawsPerRound);
        }

        [Test]
        public void ProbabilityPreview_MatchesTableAfterEquip()
        {
            var random = new RandomService(1);
            var engine = new GachaEngine(random, NoPity, 0.72, 0.25, 0.03);
            var run = CreateRun(engine, random);
            ModuleData calibrator = CreateModule(ModifierType.SuperRareChance, GradeFilter.Any, 0.05f);

            ModulePreview preview = run.PreviewModule(calibrator);

            preview.GetProbabilitiesBefore(out double n0, out double r0, out double sr0);
            Assert.AreEqual(0.72, n0, Tolerance);
            Assert.AreEqual(0.25, r0, Tolerance);
            Assert.AreEqual(0.03, sr0, Tolerance);

            preview.GetProbabilitiesAfter(out double n1, out double r1, out double sr1);
            Assert.AreEqual(0.72 * 0.92 / 0.97, n1, 1e-6);
            Assert.AreEqual(0.25 * 0.92 / 0.97, r1, 1e-6);
            Assert.AreEqual(0.08, sr1, 1e-6);

            run.EquipModule(calibrator);

            Assert.AreEqual(n1, engine.GetProbability(Grade.N), Tolerance);
            Assert.AreEqual(r1, engine.GetProbability(Grade.R), Tolerance);
            Assert.AreEqual(sr1, engine.GetProbability(Grade.SR), Tolerance);
        }

        [Test]
        public void Preview_DoesNotChangeRunState()
        {
            var random = new RandomService(1);
            var engine = new GachaEngine(random, NoPity, 0.0, 1.0, 0.0);
            var run = CreateRun(engine, random);
            run.Draw();
            run.Draw();
            uint stateBefore = random.State;
            int pityBefore = engine.DrawsWithoutSuperRare;
            int productionBefore = run.Production;

            run.PreviewModule(CreateModule(ModifierType.OutputMultiplier, GradeFilter.Any, 0.3f));
            run.PreviewModule(CreateModule(ModifierType.SuperRareChance, GradeFilter.Any, 0.05f));
            run.PreviewModule(CreateModule(ModifierType.ExtraDraws, GradeFilter.Any, 2f));

            Assert.AreEqual(stateBefore, random.State);
            Assert.AreEqual(2, pityBefore);
            Assert.AreEqual(pityBefore, engine.DrawsWithoutSuperRare);
            Assert.AreEqual(productionBefore, run.Production);
            Assert.AreEqual(1.0, engine.GetProbability(Grade.R), Tolerance);
            Assert.AreEqual(0.0, engine.GetProbability(Grade.SR), Tolerance);
            Assert.AreEqual(8, run.DrawsRemaining);
            Assert.AreEqual(3, run.Draw().Output);
        }

        [Test]
        public void OutputPreview_MatchesDrawAfterEquip()
        {
            var random = new RandomService(1);
            var engine = new GachaEngine(random, NoPity, 0.0, 1.0, 0.0);
            var run = CreateRun(engine, random);
            ModuleData coil = CreateModule(ModifierType.OutputMultiplier, GradeFilter.Any, 0.3f);

            ModulePreview preview = run.PreviewModule(coil);

            Assert.AreEqual(3, preview.OutputBefore(Grade.R, 3, false));
            Assert.AreEqual(4, preview.OutputAfter(Grade.R, 3, false));
            Assert.AreEqual(13, preview.OutputAfter(Grade.SR, 10, false));

            run.EquipModule(coil);

            Assert.AreEqual(4, run.Draw().Output);
        }

        [Test]
        public void StreakPreview_ShowsBonusOnlyWhenTriggered()
        {
            var random = new RandomService(1);
            var run = CreateRun(new GachaEngine(random, NoPity, 0.72, 0.25, 0.03), random);

            ModulePreview preview = run.PreviewModule(CreateModule(ModifierType.StreakBonus, GradeFilter.Any, 10f));

            Assert.AreEqual(1, preview.OutputBefore(Grade.N, 1, true));
            Assert.AreEqual(11, preview.OutputAfter(Grade.N, 1, true));
            Assert.AreEqual(1, preview.OutputAfter(Grade.N, 1, false));
        }

        [TestCase(10, 12)]
        [TestCase(13, 15)]
        public void DrawsPreview_MatchesNextRoundAfterEquip(int baseDraws, int expectedAfter)
        {
            var random = new RandomService(1);
            var run = CreateRun(new GachaEngine(random, NoPity, 1.0, 0.0, 0.0), random, baseDraws);
            ModuleData overload = CreateModule(ModifierType.ExtraDraws, GradeFilter.Any, 2f);

            ModulePreview preview = run.PreviewModule(overload);

            Assert.AreEqual(baseDraws, preview.DrawsPerRoundBefore);
            Assert.AreEqual(expectedAfter, preview.DrawsPerRoundAfter);

            for (int i = 0; i < 8; i++)
            {
                run.Draw();
            }

            run.EquipModule(overload);
            run.AdvanceRound();

            Assert.AreEqual(expectedAfter, run.DrawsRemaining);
        }

        [Test]
        public void Preview_IncludesAlreadyEquippedModules()
        {
            var random = new RandomService(1);
            var run = CreateRun(new GachaEngine(random, NoPity, 0.0, 1.0, 0.0), random);
            run.EquipModule(CreateModule(ModifierType.OutputMultiplier, GradeFilter.Any, 0.3f));

            ModulePreview preview = run.PreviewModule(CreateModule(ModifierType.OutputMultiplier, GradeFilter.Any, 0.3f));

            Assert.AreEqual(4, preview.OutputBefore(Grade.R, 3, false));
            Assert.AreEqual(5, preview.OutputAfter(Grade.R, 3, false));
        }

        [Test]
        public void Preview_IsNotAffectedByLaterEquip()
        {
            var random = new RandomService(1);
            var run = CreateRun(new GachaEngine(random, NoPity, 0.0, 1.0, 0.0), random);
            ModulePreview preview = run.PreviewModule(CreateModule(ModifierType.OutputMultiplier, GradeFilter.Any, 0.3f));

            run.EquipModule(CreateModule(ModifierType.OutputMultiplier, GradeFilter.Any, 1.0f));

            Assert.AreEqual(3, preview.OutputBefore(Grade.R, 3, false));
            Assert.AreEqual(4, preview.OutputAfter(Grade.R, 3, false));
        }

        [Test]
        public void PreviewModule_RejectsNull()
        {
            var random = new RandomService(1);
            var run = CreateRun(new GachaEngine(random, NoPity, 0.72, 0.25, 0.03), random);

            Assert.Throws<ArgumentNullException>(() => run.PreviewModule(null));
        }
    }
}
