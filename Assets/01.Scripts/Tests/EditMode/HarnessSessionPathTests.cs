using System;
using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    // 하네스가 화면과 같은 RunSession 길 (OpenOffer → SelectModule) 로 도는지, 고르지 않는 길 (DeclineOffer) 이 맞는지
    public class HarnessSessionPathTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

        private PartData[] parts;
        private ModuleData[] modules;

        private sealed class SlotStrategy : IModuleStrategy
        {
            private readonly int slot;

            public SlotStrategy(int slot)
            {
                this.slot = slot;
            }

            public string Name => "칸 " + slot;

            public ModuleData Choose(IReadOnlyList<ModuleData> offer, RunManager runManager, RandomService choiceRandom)
            {
                return slot < 0 ? null : offer[Math.Min(slot, offer.Count - 1)];
            }
        }

        [SetUp]
        public void CreateData()
        {
            parts = new[]
            {
                CreatePart("N1", Grade.N, 1),
                CreatePart("R1", Grade.R, 3),
                CreatePart("SR1", Grade.SR, 10),
            };
            modules = new[]
            {
                CreateModule("교정", ModuleAxis.Probability, ModifierType.SuperRareChance, GradeFilter.Any, 0.05f),
                CreateModule("코일", ModuleAxis.Multiplier, ModifierType.OutputMultiplier, GradeFilter.Any, 0.3f),
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

        private static void DrawUntilNotDrawing(RunManager run)
        {
            while (run.State == RunState.Drawing)
            {
                run.Draw();
            }
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(2)]
        public void Simulate_MatchesSessionPathAndEndState(int slot)
        {
            // 같은 시드를 세션 길로 직접 돌린 판과 기록 · 마지막 세이브 값 (난수 상태 · 천장 · 라운드) 이 같아야 한다
            var strategy = new SlotStrategy(slot);
            for (uint seed = 1; seed <= 200; seed++)
            {
                RunRecord record = RunSimulator.Simulate(seed, parts, modules, strategy);

                RunSession mirror = RunSession.Create(seed, parts, modules);
                RunManager run = mirror.RunManager;
                while (true)
                {
                    DrawUntilNotDrawing(run);
                    if (run.State != RunState.RoundCleared)
                    {
                        break;
                    }

                    IReadOnlyList<ModuleData> offer = mirror.OpenOffer();
                    if (slot < 0)
                    {
                        mirror.DeclineOffer();
                    }
                    else
                    {
                        mirror.SelectModule(offer[Math.Min(slot, offer.Count - 1)]);
                    }
                }

                Assert.AreEqual(run.CurrentRound, record.FinalRound, "seed " + seed);
                Assert.AreEqual(run.State == RunState.RunCleared, record.Cleared, "seed " + seed);
                Assert.AreEqual(run.EquippedModules.Count, record.EquippedModules.Count, "seed " + seed);
                for (int i = 0; i < run.EquippedModules.Count; i++)
                {
                    Assert.AreSame(run.EquippedModules[i], record.EquippedModules[i], "seed " + seed);
                }
            }
        }

        [Test]
        public void Simulate_RecordsOneModulePerBreakthrough()
        {
            // 기록은 끝난 런의 장착 목록이다 (고른 수만큼)
            var strategy = new SlotStrategy(0);
            for (uint seed = 1; seed <= 50; seed++)
            {
                RunRecord record = RunSimulator.Simulate(seed, parts, modules, strategy);
                // 마지막 라운드 돌파 (클리어) 에는 제안이 없다 → 장착 수 = 끝난 라운드 − 1
                Assert.AreEqual(record.FinalRound - 1, record.EquippedModules.Count, "seed " + seed);
            }
        }

        [Test]
        public void DeclineOffer_AdvancesWithoutEquipping()
        {
            RunSession session = RunSession.Create(3, parts, modules);
            DrawUntilNotDrawing(session.RunManager);
            Assert.AreEqual(RunState.RoundCleared, session.RunManager.State);

            session.OpenOffer();
            session.DeclineOffer();

            Assert.AreEqual(2, session.RunManager.CurrentRound);
            Assert.AreEqual(RunState.Drawing, session.RunManager.State);
            Assert.AreEqual(0, session.RunManager.EquippedModules.Count);
            Assert.AreEqual(0, session.CurrentOffer.Count);
            Assert.IsFalse(session.CanRerollOffer);
            Assert.AreEqual(10, session.RunManager.DrawsRemaining);
        }

        [Test]
        public void DeclineOffer_WithoutOpenOffer_Throws()
        {
            RunSession session = RunSession.Create(3, parts, modules);
            Assert.Throws<InvalidOperationException>(() => session.DeclineOffer());

            DrawUntilNotDrawing(session.RunManager);
            Assert.Throws<InvalidOperationException>(() => session.DeclineOffer());
            Assert.AreEqual(1, session.RunManager.CurrentRound);
        }

        [Test]
        public void DeclineOffer_ResetsRerollForNextBreakthrough()
        {
            // 다시 받은 뒤 고르지 않고 넘어가도 다음 돌파에는 다시 받을 수 있다 (R2 도 돌파하는 첫 시드로 확인)
            bool checkedOnce = false;
            for (uint seed = 1; seed <= 50 && !checkedOnce; seed++)
            {
                RunSession session = RunSession.Create(seed, parts, modules);
                DrawUntilNotDrawing(session.RunManager);
                session.OpenOffer();
                session.RerollOffer();
                Assert.IsFalse(session.CanRerollOffer);
                session.DeclineOffer();
                Assert.IsFalse(session.OfferRerolled);

                DrawUntilNotDrawing(session.RunManager);
                if (session.RunManager.State != RunState.RoundCleared)
                {
                    continue;
                }

                session.OpenOffer();
                Assert.IsTrue(session.CanRerollOffer, "seed " + seed);
                checkedOnce = true;
            }

            Assert.IsTrue(checkedOnce, "R2 를 돌파하는 시드가 없음");
        }
    }
}
