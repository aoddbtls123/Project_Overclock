using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    public class RunSessionTests
    {
        private const double Tolerance = 1e-12;

        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

        private PartData[] parts;
        private ModuleData[] modules;

        [SetUp]
        public void CreateData()
        {
            parts = new[]
            {
                CreatePart("N1", Grade.N, 1),
                CreatePart("N2", Grade.N, 1),
                CreatePart("R1", Grade.R, 3),
                CreatePart("SR1", Grade.SR, 10),
            };
            modules = new[]
            {
                CreateModule("교정", ModuleAxis.Probability, ModifierType.SuperRareChance, GradeFilter.Any, 0.05f),
                CreateModule("코일", ModuleAxis.Multiplier, ModifierType.OutputMultiplier, GradeFilter.Any, 0.3f),
                CreateModule("선별", ModuleAxis.Multiplier, ModifierType.OutputMultiplier, GradeFilter.SR, 1f),
                CreateModule("동기화", ModuleAxis.Conditional, ModifierType.StreakBonus, GradeFilter.Any, 10f),
                CreateModule("과부하", ModuleAxis.Draws, ModifierType.ExtraDraws, GradeFilter.Any, 2f),
            };
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

        private ModuleData CreateModule(string displayName, ModuleAxis axis, ModifierType type, GradeFilter target, float value)
        {
            var module = ScriptableObject.CreateInstance<ModuleData>();
            module.InitializeForTests(displayName, axis, new ModifierEffect(type, target, value));
            createdObjects.Add(module);
            return module;
        }

        [Test]
        public void Create_UsesDesignRules()
        {
            RunSession session = RunSession.Create(1, parts, modules);

            Assert.AreEqual(0.72, session.GachaEngine.GetProbability(Grade.N), Tolerance);
            Assert.AreEqual(0.25, session.GachaEngine.GetProbability(Grade.R), Tolerance);
            Assert.AreEqual(0.03, session.GachaEngine.GetProbability(Grade.SR), Tolerance);
            Assert.AreEqual(19, session.GachaEngine.PityThreshold);
            Assert.AreEqual(8, session.RunManager.RoundCount);
            Assert.AreEqual(1, session.RunManager.CurrentRound);
            Assert.AreEqual(10, session.RunManager.DrawsRemaining);
            Assert.AreEqual(RunState.Drawing, session.RunManager.State);
        }

        [Test]
        public void Create_MatchesManualAssemblyWithOneRandomService()
        {
            // 기대값은 기획 숫자로 직접 조립한 거울 런. 난수원이 하나라도 갈라지면 부품이나 카드가 달라진다
            for (uint seed = 1; seed <= 100; seed++)
            {
                RunSession session = RunSession.Create(seed, parts, modules);

                var mirrorRandom = new RandomService(seed);
                var mirrorEngine = new GachaEngine(mirrorRandom, 19, 0.72, 0.25, 0.03);
                var mirrorRun = new RunManager(mirrorEngine, new PartCatalog(parts, mirrorRandom), 8, 10);
                var mirrorOffer = new ModuleOffer(modules, mirrorRandom, mirrorRun.CanOfferModule);

                RunManager run = session.RunManager;
                while (run.State == RunState.Drawing || run.State == RunState.RoundCleared)
                {
                    if (run.State == RunState.Drawing)
                    {
                        Assert.AreSame(mirrorRun.Draw().Part, run.Draw().Part, "seed " + seed);
                        continue;
                    }

                    List<ModuleData> offer = session.ModuleOffer.CreateOffer();
                    List<ModuleData> mirrorCards = mirrorOffer.CreateOffer();
                    Assert.AreEqual(mirrorCards.Count, offer.Count, "seed " + seed);
                    for (int i = 0; i < offer.Count; i++)
                    {
                        Assert.AreSame(mirrorCards[i], offer[i], "seed " + seed);
                    }

                    run.EquipModule(offer[0]);
                    mirrorRun.EquipModule(mirrorCards[0]);
                    run.AdvanceRound();
                    mirrorRun.AdvanceRound();
                }

                Assert.AreEqual(mirrorRun.State, run.State, "seed " + seed);
                Assert.AreEqual(mirrorRun.CurrentRound, run.CurrentRound, "seed " + seed);
                Assert.AreEqual(mirrorEngine.DrawsWithoutSuperRare, session.GachaEngine.DrawsWithoutSuperRare, "seed " + seed);
            }
        }
    }
}
