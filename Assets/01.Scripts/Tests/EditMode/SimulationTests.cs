using System;
using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    public class SimulationTests
    {
        private const double Tolerance = 1e-6;

        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

        private PartData[] parts;
        private ModuleData calibrator;
        private ModuleData coil;
        private ModuleData selective;
        private ModuleData sync;
        private ModuleData overload;
        private ModuleData[] modules;

        private sealed class FixedSlotStrategy : IModuleStrategy
        {
            private readonly int slot;

            public FixedSlotStrategy(int slot)
            {
                this.slot = slot;
            }

            public string Name => "고정 칸 " + slot;

            public ModuleData Choose(IReadOnlyList<ModuleData> offer, RunManager runManager, RandomService choiceRandom)
            {
                return offer[Math.Min(slot, offer.Count - 1)];
            }
        }

        private sealed class OutsideOfferStrategy : IModuleStrategy
        {
            private readonly ModuleData outsider;

            public OutsideOfferStrategy(ModuleData outsider)
            {
                this.outsider = outsider;
            }

            public string Name => "제안 밖";

            public ModuleData Choose(IReadOnlyList<ModuleData> offer, RunManager runManager, RandomService choiceRandom)
            {
                return outsider;
            }
        }

        [SetUp]
        public void CreateData()
        {
            parts = new[]
            {
                CreatePart("N1", Grade.N, 1),
                CreatePart("N2", Grade.N, 1),
                CreatePart("R1", Grade.R, 3),
                CreatePart("R2", Grade.R, 3),
                CreatePart("SR1", Grade.SR, 10),
            };
            calibrator = CreateModule("교정", ModuleAxis.Probability, ModifierType.SuperRareChance, GradeFilter.Any, 0.05f);
            coil = CreateModule("코일", ModuleAxis.Multiplier, ModifierType.OutputMultiplier, GradeFilter.Any, 0.3f);
            selective = CreateModule("선별", ModuleAxis.Multiplier, ModifierType.OutputMultiplier, GradeFilter.SR, 1f);
            sync = CreateModule("동기화", ModuleAxis.Conditional, ModifierType.StreakBonus, GradeFilter.Any, 10f);
            overload = CreateModule("과부하", ModuleAxis.Draws, ModifierType.ExtraDraws, GradeFilter.Any, 2f);
            modules = new[] { calibrator, coil, selective, sync, overload };
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

        private static RunRecord Record(int finalRound, bool cleared)
        {
            return new RunRecord(1, finalRound, cleared, new List<ModuleData>());
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(2)]
        public void Simulate_MatchesManualPlayLoop(int slot)
        {
            // slot -1 = 고르지 않음. 거울 런은 기획 숫자로 직접 조립하고 화면과 같은 순서(제안 → 장착 → 진행)로 진행한다
            IModuleStrategy strategy = slot < 0 ? (IModuleStrategy)new NoModuleStrategy() : new FixedSlotStrategy(slot);

            for (uint seed = 1; seed <= 200; seed++)
            {
                RunRecord record = RunSimulator.Simulate(seed, parts, modules, strategy);

                var random = new RandomService(seed);
                var engine = new GachaEngine(random, 50, 0.72, 0.25, 0.03);
                var run = new RunManager(engine, new PartCatalog(parts, random), 8, 10);
                var offer = new ModuleOffer(modules, random);
                var equipped = new List<ModuleData>();
                while (run.State == RunState.Drawing || run.State == RunState.RoundCleared)
                {
                    if (run.State == RunState.Drawing)
                    {
                        run.Draw();
                        continue;
                    }

                    List<ModuleData> cards = offer.CreateOffer();
                    if (slot >= 0)
                    {
                        ModuleData choice = cards[Math.Min(slot, cards.Count - 1)];
                        run.EquipModule(choice);
                        equipped.Add(choice);
                    }

                    run.AdvanceRound();
                }

                Assert.AreEqual(seed, record.Seed);
                Assert.AreEqual(run.CurrentRound, record.FinalRound, "seed " + seed);
                Assert.AreEqual(run.State == RunState.RunCleared, record.Cleared, "seed " + seed);
                Assert.AreEqual(equipped.Count, record.EquippedModules.Count, "seed " + seed);
                for (int i = 0; i < equipped.Count; i++)
                {
                    Assert.AreSame(equipped[i], record.EquippedModules[i], "seed " + seed);
                }
            }
        }

        [Test]
        public void Simulate_RejectsChoiceOutsideOffer()
        {
            ModuleData outsider = CreateModule("밖", ModuleAxis.Draws, ModifierType.ExtraDraws, GradeFilter.Any, 1f);

            Assert.Throws<InvalidOperationException>(() => RunSimulator.Simulate(1, parts, modules, new OutsideOfferStrategy(outsider)));
        }

        [Test]
        public void Simulate_RandomStrategyUsesChoiceSeedFromGameSeed()
        {
            // 선택 난수원은 런마다 (게임 시드 XOR 0x2545F491) 로 새로 만든다. CSV 의 시드 하나로 무작위 런을 재현하기 위한 규칙
            var strategy = new RandomModuleStrategy();

            for (uint seed = 1; seed <= 200; seed++)
            {
                RunRecord record = RunSimulator.Simulate(seed, parts, modules, strategy);

                var random = new RandomService(seed);
                var choiceRandom = new RandomService(seed ^ 0x2545F491u);
                var engine = new GachaEngine(random, 50, 0.72, 0.25, 0.03);
                var run = new RunManager(engine, new PartCatalog(parts, random), 8, 10);
                var offer = new ModuleOffer(modules, random);
                var equipped = new List<ModuleData>();
                while (run.State == RunState.Drawing || run.State == RunState.RoundCleared)
                {
                    if (run.State == RunState.Drawing)
                    {
                        run.Draw();
                        continue;
                    }

                    List<ModuleData> cards = offer.CreateOffer();
                    ModuleData choice = cards[(int)(choiceRandom.NextDouble() * cards.Count)];
                    run.EquipModule(choice);
                    equipped.Add(choice);
                    run.AdvanceRound();
                }

                Assert.AreEqual(run.CurrentRound, record.FinalRound, "seed " + seed);
                Assert.AreEqual(equipped.Count, record.EquippedModules.Count, "seed " + seed);
                for (int i = 0; i < equipped.Count; i++)
                {
                    Assert.AreSame(equipped[i], record.EquippedModules[i], "seed " + seed);
                }
            }
        }

        [Test]
        public void SimulateMany_MatchesSingleRunsForEachSeed()
        {
            var strategy = new RandomModuleStrategy();

            List<RunRecord> records = RunSimulator.SimulateMany(10, 20, parts, modules, strategy);

            Assert.AreEqual(20, records.Count);
            for (int i = 0; i < 20; i++)
            {
                RunRecord single = RunSimulator.Simulate((uint)(10 + i), parts, modules, strategy);
                Assert.AreEqual((uint)(10 + i), records[i].Seed);
                AssertSameRecord(single, records[i]);
            }
        }

        [Test]
        public void SimulateMany_KeepsEarlierRecordsWhenRunCountGrows()
        {
            var strategy = new RandomModuleStrategy();

            List<RunRecord> shorter = RunSimulator.SimulateMany(1, 30, parts, modules, strategy);
            List<RunRecord> longer = RunSimulator.SimulateMany(1, 60, parts, modules, strategy);

            for (int i = 0; i < shorter.Count; i++)
            {
                AssertSameRecord(shorter[i], longer[i]);
            }
        }

        private static void AssertSameRecord(RunRecord expected, RunRecord actual)
        {
            Assert.AreEqual(expected.Seed, actual.Seed);
            Assert.AreEqual(expected.FinalRound, actual.FinalRound, "seed " + expected.Seed);
            Assert.AreEqual(expected.Cleared, actual.Cleared, "seed " + expected.Seed);
            Assert.AreEqual(expected.EquippedModules.Count, actual.EquippedModules.Count, "seed " + expected.Seed);
            for (int i = 0; i < expected.EquippedModules.Count; i++)
            {
                Assert.AreSame(expected.EquippedModules[i], actual.EquippedModules[i], "seed " + expected.Seed);
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        public void RandomStrategy_ChoosesWithGivenChoiceRandom(int offerSize)
        {
            var offer = new List<ModuleData>();
            for (int i = 0; i < offerSize; i++)
            {
                offer.Add(modules[i]);
            }

            var strategy = new RandomModuleStrategy();
            var choiceRandom = new RandomService(99);
            var mirror = new RandomService(99);

            for (int i = 0; i < 100; i++)
            {
                ModuleData expected = offer[(int)(mirror.NextDouble() * offerSize)];
                Assert.AreSame(expected, strategy.Choose(offer, null, choiceRandom));
            }
        }

        [TestCase("없음", 17.7)]
        [TestCase("교정", 21.942268041237114)]
        [TestCase("코일", 21.1)]
        [TestCase("선별", 20.7)]
        [TestCase("과부하", 21.24)]
        [TestCase("동기화", 33.35568062328275)]
        public void ExpectedRoundOutput_MatchesHandCalculation(string moduleName, double expected)
        {
            // 기대값: 10회 × (0.72×1 + 0.25×3 + 0.03×10) 을 효과별로 손 계산.
            // 동기화는 3^10 가지 등급 순서를 전부 나열해 구한 발동 기대 횟수 1.5655680623 × 10 을 더한 값
            ModuleData module = moduleName == "없음"
                ? CreateModule("없음", ModuleAxis.Draws, ModifierType.ExtraDraws, GradeFilter.Any, 0f)
                : Array.Find(modules, m => m.DisplayName == moduleName);
            RunManager run = RunSession.Create(1, parts, modules).RunManager;

            double actual = GreedyModuleStrategy.ExpectedRoundOutput(run.PreviewModule(module), parts);

            Assert.AreEqual(expected, actual, Tolerance);
        }

        [Test]
        public void ExpectedRoundOutput_AveragesPartsWithinGrade()
        {
            // N 부품이 1 과 2 → N 평균 1.5. 10회 × (0.72×1.5 + 0.25×3 + 0.03×10) = 21.3
            PartData[] mixed =
            {
                CreatePart("N1", Grade.N, 1),
                CreatePart("N2", Grade.N, 2),
                CreatePart("R1", Grade.R, 3),
                CreatePart("SR1", Grade.SR, 10),
            };
            ModuleData none = CreateModule("없음", ModuleAxis.Draws, ModifierType.ExtraDraws, GradeFilter.Any, 0f);
            RunManager run = RunSession.Create(1, mixed, modules).RunManager;

            double actual = GreedyModuleStrategy.ExpectedRoundOutput(run.PreviewModule(none), mixed);

            Assert.AreEqual(21.3, actual, Tolerance);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void GreedyStrategy_ChoosesHighestExpectedOutput(int rotation)
        {
            // 교정 21.94 > 과부하 21.24 > 코일 21.1. 놓인 자리와 상관없이 교정
            var cards = new List<ModuleData> { coil, overload, calibrator };
            for (int i = 0; i < rotation; i++)
            {
                cards.Add(cards[0]);
                cards.RemoveAt(0);
            }

            RunManager run = RunSession.Create(1, parts, modules).RunManager;

            Assert.AreSame(calibrator, new GreedyModuleStrategy(parts).Choose(cards, run, null));
        }

        [Test]
        public void GreedyStrategy_JudgesFromCurrentEquipment()
        {
            // 교정을 5개 달면 SR 이 상한 25% 라 교정을 더 달아도 늘지 않는다 → 코일
            RunManager run = RunSession.Create(1, parts, modules).RunManager;
            for (int i = 0; i < 5; i++)
            {
                run.EquipModule(calibrator);
            }

            var cards = new List<ModuleData> { calibrator, coil };

            Assert.AreSame(coil, new GreedyModuleStrategy(parts).Choose(cards, run, null));
        }

        [Test]
        public void Summary_ComputesIndicatorsFromRecords()
        {
            var records = new List<RunRecord>
            {
                Record(2, false), Record(3, false), Record(3, false), Record(4, false), Record(4, false),
                Record(4, false), Record(5, false), Record(6, false), Record(8, false), Record(8, true),
            };

            var summary = new SimulationSummary(records);

            Assert.AreEqual(10, summary.RunCount);
            Assert.AreEqual(4.7, summary.MeanFinalRound, Tolerance);
            Assert.AreEqual(4.0, summary.MedianFinalRound, Tolerance);
            Assert.AreEqual(0.1, summary.ClearRate, Tolerance);
            Assert.AreEqual(7, summary.ReachedCount(4));
            Assert.AreEqual(4, summary.PassedCount(4));
            Assert.AreEqual(4.0 / 7.0, summary.PassRate(4), Tolerance);
            Assert.AreEqual(1, summary.PassedCount(8));
            Assert.AreEqual(0.5, summary.PassRate(8), Tolerance);
            Assert.AreEqual(3, summary.EndedAtCount(4, false));
            Assert.AreEqual(1, summary.EndedAtCount(8, false));
            Assert.AreEqual(1, summary.EndedAtCount(8, true));
            Assert.AreEqual(6.0, summary.LuckSpread, Tolerance);
            Assert.IsTrue(double.IsNaN(summary.PassRate(9)));
        }

        [Test]
        public void Summary_UsesTenPercentTailsAndMedian()
        {
            var records = new List<RunRecord> { Record(2, false), Record(2, false), Record(7, false) };
            for (int i = 0; i < 16; i++)
            {
                records.Add(Record(3, false));
            }

            records.Add(Record(8, true));
            var summary = new SimulationSummary(records);

            Assert.AreEqual(20, summary.RunCount);
            Assert.AreEqual(7.5 - 2.0, summary.LuckSpread, Tolerance);

            var odd = new SimulationSummary(new List<RunRecord> { Record(7, false), Record(3, false), Record(5, false) });
            Assert.AreEqual(5.0, odd.MedianFinalRound, Tolerance);

            var even = new SimulationSummary(new List<RunRecord> { Record(8, true), Record(3, false), Record(7, false), Record(5, false) });
            Assert.AreEqual(6.0, even.MedianFinalRound, Tolerance);
        }

        [Test]
        public void NoModuleStrategy_RoundPassRatesMatchAnalysis()
        {
            // 지표 ① 의 근거. 해석값 R2 96.26 · R3 44.14 · R4 13.36 (%) — 그 라운드에 도달한 런 기준
            List<RunRecord> records = RunSimulator.SimulateMany(1, 20000, parts, modules, new NoModuleStrategy());
            var summary = new SimulationSummary(records);

            Assert.AreEqual(20000, summary.ReachedCount(1));
            Assert.AreEqual(0.9626, summary.PassRate(2), 0.01);
            Assert.AreEqual(0.4414, summary.PassRate(3), 0.01);
            Assert.AreEqual(0.1336, summary.PassRate(4), 0.01);
        }
    }
}
