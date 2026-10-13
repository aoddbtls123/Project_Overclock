using System;
using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    public class RunManagerTests
    {
        private const int NoPity = int.MaxValue;
        private const int BasePityThreshold = 50;
        private const int BaseRoundCount = 8;
        private const int BaseDrawsPerRound = 10;
        private const int StatisticsRunCount = 100000;

        private readonly List<PartData> createdParts = new List<PartData>();
        private readonly List<ModuleData> createdModules = new List<ModuleData>();

        private PartData normal;
        private PartData normalDouble;
        private PartData rare;
        private PartData superRare;

        [SetUp]
        public void CreateParts()
        {
            normal = CreatePart("N1", Grade.N, 1);
            normalDouble = CreatePart("N2", Grade.N, 2);
            rare = CreatePart("R1", Grade.R, 3);
            superRare = CreatePart("SR1", Grade.SR, 10);
        }

        [TearDown]
        public void DestroyCreatedParts()
        {
            foreach (PartData part in createdParts)
            {
                UnityEngine.Object.DestroyImmediate(part);
            }

            createdParts.Clear();

            foreach (ModuleData module in createdModules)
            {
                UnityEngine.Object.DestroyImmediate(module);
            }

            createdModules.Clear();
        }

        private PartData CreatePart(string displayName, Grade grade, int baseOutput)
        {
            var part = ScriptableObject.CreateInstance<PartData>();
            part.InitializeForTests(displayName, grade, baseOutput);
            createdParts.Add(part);
            return part;
        }

        private ModuleData CreateModule(ModifierType type, float value)
        {
            var module = ScriptableObject.CreateInstance<ModuleData>();
            module.InitializeForTests(type.ToString(), new ModifierEffect(type, GradeFilter.Any, value));
            createdModules.Add(module);
            return module;
        }

        private RunManager CreateFixedGradeRun(double n, double r, double sr, int roundCount, int drawsPerRound)
        {
            var random = new RandomService(1);
            var parts = new List<PartData> { normal, rare, superRare };
            var engine = new GachaEngine(random, NoPity, n, r, sr);
            return new RunManager(engine, new PartCatalog(parts, random), roundCount, drawsPerRound);
        }

        private static void DrawTimes(RunManager run, int count)
        {
            for (int i = 0; i < count; i++)
            {
                run.Draw();
            }
        }

        [Test]
        public void QuotaFor_MatchesDesignTable()
        {
            int[] expected = { 8, 12, 17, 24, 35, 51, 74, 108 };

            for (int round = 1; round <= expected.Length; round++)
            {
                Assert.AreEqual(expected[round - 1], RunManager.QuotaFor(round), "round " + round);
            }
        }

        [TestCase(0, BaseDrawsPerRound)]
        [TestCase(BaseRoundCount, 0)]
        public void Constructor_RejectsNonPositiveCounts(int roundCount, int drawsPerRound)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateFixedGradeRun(1.0, 0.0, 0.0, roundCount, drawsPerRound));
        }

        [Test]
        public void AllNormal_ClearsOnQuotaThenResetsForNextRound()
        {
            var run = CreateFixedGradeRun(1.0, 0.0, 0.0, BaseRoundCount, BaseDrawsPerRound);
            Assert.Throws<InvalidOperationException>(() => run.AdvanceRound());

            DrawTimes(run, 7);
            Assert.AreEqual(RunState.Drawing, run.State);

            Assert.AreSame(normal, run.Draw().Part);
            Assert.AreEqual(RunState.RoundCleared, run.State);
            Assert.AreEqual(8, run.Production);
            Assert.AreEqual(2, run.DrawsRemaining);
            Assert.Throws<InvalidOperationException>(() => run.Draw());

            run.AdvanceRound();
            Assert.AreEqual(2, run.CurrentRound);
            Assert.AreEqual(12, run.Quota);
            Assert.AreEqual(0, run.Production);
            Assert.AreEqual(BaseDrawsPerRound, run.DrawsRemaining);

            DrawTimes(run, 9);
            Assert.AreEqual(RunState.Drawing, run.State);

            run.Draw();
            Assert.AreEqual(RunState.RunFailed, run.State);
            Assert.AreEqual(10, run.Production);
            Assert.Throws<InvalidOperationException>(() => run.Draw());
            Assert.Throws<InvalidOperationException>(() => run.AdvanceRound());
        }

        [Test]
        public void ReachingQuotaOnLastDraw_ClearsRound()
        {
            var run = CreateFixedGradeRun(1.0, 0.0, 0.0, BaseRoundCount, 8);

            DrawTimes(run, 8);

            Assert.AreEqual(RunState.RoundCleared, run.State);
            Assert.AreEqual(0, run.DrawsRemaining);
        }

        [Test]
        public void ClearingFinalRound_EndsRun()
        {
            var run = CreateFixedGradeRun(0.0, 0.0, 1.0, 2, BaseDrawsPerRound);

            run.Draw();
            Assert.AreEqual(RunState.RoundCleared, run.State);
            run.AdvanceRound();

            run.Draw();
            Assert.AreEqual(RunState.Drawing, run.State);
            run.Draw();

            Assert.AreEqual(RunState.RunCleared, run.State);
            Assert.AreEqual(2, run.CurrentRound);
            Assert.Throws<InvalidOperationException>(() => run.AdvanceRound());
            Assert.Throws<InvalidOperationException>(() => run.Draw());
        }

        [Test]
        public void PityCounter_CarriesOverAcrossRounds()
        {
            var random = new RandomService(1);
            var engine = new GachaEngine(random, 9, 1.0, 0.0, 0.0);
            var catalog = new PartCatalog(new List<PartData> { normal, rare, superRare }, random);
            var run = new RunManager(engine, catalog, BaseRoundCount, BaseDrawsPerRound);

            DrawTimes(run, 8);
            Assert.AreEqual(RunState.RoundCleared, run.State);

            run.AdvanceRound();
            Assert.AreEqual(8, engine.DrawsWithoutSuperRare);

            Assert.AreSame(normal, run.Draw().Part);
            Assert.AreSame(superRare, run.Draw().Part);
            Assert.AreEqual(0, engine.DrawsWithoutSuperRare);
        }

        [Test]
        public void Draw_PicksPartFromCatalogAndAddsItsOutput()
        {
            var runRandom = new RandomService(7);
            var mirrorRandom = new RandomService(7);
            var parts = new List<PartData> { normal, normalDouble, rare, superRare };
            var catalog = new PartCatalog(parts, runRandom);
            var engine = new GachaEngine(runRandom, NoPity, 1.0, 0.0, 0.0);
            var run = new RunManager(engine, catalog, BaseRoundCount, BaseDrawsPerRound);
            int expectedProduction = 0;
            int drawCount = 0;

            while (run.State == RunState.Drawing)
            {
                // GachaEngine.Draw 가 등급 추첨에 난수 1개를 먼저 쓴다
                mirrorRandom.NextDouble();
                PartData expected = catalog.Select(Grade.N, mirrorRandom.NextDouble());

                Assert.AreSame(expected, run.Draw().Part, "draw " + (drawCount + 1));
                expectedProduction += expected.BaseOutput;
                Assert.AreEqual(expectedProduction, run.Production);
                drawCount++;
            }

            Assert.AreEqual(5, drawCount);
            Assert.AreEqual(mirrorRandom.State, runRandom.State);
        }

        [Test]
        public void EquipModule_UpdatesProbabilitiesWithoutResettingPity()
        {
            var random = new RandomService(1);
            var engine = new GachaEngine(random, NoPity, 1.0, 0.0, 0.0);
            var catalog = new PartCatalog(new List<PartData> { normal, rare, superRare }, random);
            var run = new RunManager(engine, catalog, BaseRoundCount, BaseDrawsPerRound);
            DrawTimes(run, 3);

            run.EquipModule(CreateModule(ModifierType.SuperRareChance, 0.05f));

            Assert.AreEqual(0.95, engine.GetProbability(Grade.N), 1e-6);
            Assert.AreEqual(0.0, engine.GetProbability(Grade.R), 1e-6);
            Assert.AreEqual(0.05, engine.GetProbability(Grade.SR), 1e-6);
            Assert.AreEqual(3, engine.DrawsWithoutSuperRare);
        }

        [Test]
        public void Multiplier_ChangesDrawOutputAndProduction()
        {
            var run = CreateFixedGradeRun(0.0, 1.0, 0.0, BaseRoundCount, BaseDrawsPerRound);
            run.EquipModule(CreateModule(ModifierType.OutputMultiplier, 0.3f));

            DrawResult result = run.Draw();

            Assert.AreSame(rare, result.Part);
            Assert.AreEqual(4, result.Output);
            Assert.AreEqual(4, run.Production);
        }

        [Test]
        public void Multiplier_WorksOnAllSuperRareTable()
        {
            var run = CreateFixedGradeRun(0.0, 0.0, 1.0, BaseRoundCount, BaseDrawsPerRound);

            run.EquipModule(CreateModule(ModifierType.OutputMultiplier, 0.3f));

            Assert.AreEqual(13, run.Draw().Output);
        }

        [Test]
        public void ExtraDraws_ApplyFromNextRound()
        {
            var run = CreateFixedGradeRun(1.0, 0.0, 0.0, BaseRoundCount, BaseDrawsPerRound);

            run.EquipModule(CreateModule(ModifierType.ExtraDraws, 2f));
            Assert.AreEqual(BaseDrawsPerRound, run.DrawsRemaining);

            DrawTimes(run, 8);
            run.AdvanceRound();

            Assert.AreEqual(BaseDrawsPerRound + 2, run.DrawsRemaining);
        }

        [Test]
        public void Streak_TriggersEveryThirdAndResetsEachRound()
        {
            var run = CreateFixedGradeRun(1.0, 0.0, 0.0, BaseRoundCount, BaseDrawsPerRound);
            DrawTimes(run, 8);
            run.EquipModule(CreateModule(ModifierType.StreakBonus, 1f));
            run.AdvanceRound();
            bool[] expected = { false, false, true, false, false, true, false, false, true };

            for (int i = 0; i < expected.Length; i++)
            {
                DrawResult result = run.Draw();
                Assert.AreEqual(expected[i], result.StreakTriggered, "draw " + (i + 1));
                Assert.AreEqual(expected[i] ? 2 : 1, result.Output, "draw " + (i + 1));
            }

            Assert.AreEqual(RunState.RoundCleared, run.State);
            Assert.AreEqual(12, run.Production);
        }

        [Test]
        public void Streak_BreaksWhenGradeChanges()
        {
            var random = new RandomService(1);
            var engine = new GachaEngine(random, NoPity, 1.0, 0.0, 0.0);
            var catalog = new PartCatalog(new List<PartData> { normal, rare, superRare }, random);
            var run = new RunManager(engine, catalog, BaseRoundCount, BaseDrawsPerRound);
            run.EquipModule(CreateModule(ModifierType.StreakBonus, 1f));

            Assert.IsFalse(run.Draw().StreakTriggered);
            Assert.IsFalse(run.Draw().StreakTriggered);
            engine.SetProbabilities(0.0, 1.0, 0.0);
            Assert.IsFalse(run.Draw().StreakTriggered);
            engine.SetProbabilities(1.0, 0.0, 0.0);
            Assert.IsFalse(run.Draw().StreakTriggered);
            Assert.IsFalse(run.Draw().StreakTriggered);
            Assert.AreEqual(RunState.Drawing, run.State);

            Assert.IsTrue(run.Draw().StreakTriggered);
            Assert.AreEqual(9, run.Production);
            Assert.AreEqual(RunState.RoundCleared, run.State);
        }

        [Test]
        public void EquipModule_RejectsFinishedRun()
        {
            var run = CreateFixedGradeRun(1.0, 0.0, 0.0, BaseRoundCount, 1);
            run.Draw();
            Assert.AreEqual(RunState.RunFailed, run.State);

            Assert.Throws<InvalidOperationException>(() => run.EquipModule(CreateModule(ModifierType.ExtraDraws, 2f)));
        }

        [Test]
        public void WithoutModules_RoundPassRatesMatchAnalysis()
        {
            var random = new RandomService(2026);
            var catalog = new PartCatalog(new List<PartData> { normal, rare, superRare }, random);
            var reached = new int[BaseRoundCount + 1];
            var passed = new int[BaseRoundCount + 1];

            for (int i = 0; i < StatisticsRunCount; i++)
            {
                var engine = new GachaEngine(random, BasePityThreshold, 0.72, 0.25, 0.03);
                var run = new RunManager(engine, catalog, BaseRoundCount, BaseDrawsPerRound);

                while (true)
                {
                    reached[run.CurrentRound]++;
                    while (run.State == RunState.Drawing)
                    {
                        run.Draw();
                    }

                    if (run.State == RunState.RunFailed)
                    {
                        break;
                    }

                    passed[run.CurrentRound]++;
                    if (run.State == RunState.RunCleared)
                    {
                        break;
                    }

                    run.AdvanceRound();
                }
            }

            Assert.AreEqual(reached[1], passed[1]);
            Assert.AreEqual(0.9626, passed[2] / (double)reached[2], 0.005);
            Assert.AreEqual(0.4414, passed[3] / (double)reached[3], 0.005);
            Assert.AreEqual(0.1336, passed[4] / (double)reached[4], 0.005);
            Assert.AreEqual(0.0068, passed[5] / (double)reached[5], 0.003);
        }
    }
}
