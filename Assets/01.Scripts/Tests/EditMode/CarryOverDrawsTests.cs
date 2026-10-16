using System;
using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    public class CarryOverDrawsTests
    {
        private const int NoPity = int.MaxValue;
        private const double Tolerance = 1e-9;

        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();
        private PartData[] parts;

        [TearDown]
        public void DestroyCreatedObjects()
        {
            foreach (UnityEngine.Object created in createdObjects)
            {
                UnityEngine.Object.DestroyImmediate(created);
            }

            createdObjects.Clear();
        }

        // 설계안 2-1: 다음 라운드 뽑기 = min(기본 10 + 추가 + min(남은 뽑기, 이월 상한), 20)
        [TestCase(0, 5, 0, 10)]
        [TestCase(0, 5, 3, 13)]
        [TestCase(0, 5, 6, 15)]
        [TestCase(2, 5, 6, 17)]
        [TestCase(9, 5, 5, 20)]
        [TestCase(4, 10, 10, 20)]
        [TestCase(2, 0, 6, 12)]
        public void NextRoundDraws_FollowCarryRule(int extraDraws, int carryCap, int remaining, int expected)
        {
            var engine = new ModifierEngine(0.72, 0.25, 0.03);
            if (extraDraws > 0)
            {
                engine.Equip(CreateModule(ModifierType.ExtraDraws, extraDraws));
            }

            if (carryCap > 0)
            {
                engine.Equip(CreateModule(ModifierType.CarryOverDraws, carryCap));
            }

            Assert.AreEqual(expected, engine.DrawsForNextRound(10, remaining));
            // 첫 라운드처럼 남은 뽑기가 없으면 이월은 0
            Assert.AreEqual(Math.Min(10 + extraDraws, 20), engine.DrawsPerRound(10));
        }

        [Test]
        public void DuplicateCarryModules_AddTheirCaps()
        {
            var engine = new ModifierEngine(0.72, 0.25, 0.03);
            engine.Equip(CreateModule(ModifierType.CarryOverDraws, 5));
            engine.Equip(CreateModule(ModifierType.CarryOverDraws, 5));

            Assert.AreEqual(18, engine.DrawsForNextRound(10, 8));
        }

        [Test]
        public void NegativeRemaining_Throws()
        {
            var engine = new ModifierEngine(0.72, 0.25, 0.03);

            Assert.Throws<ArgumentOutOfRangeException>(() => engine.DrawsForNextRound(10, -1));
        }

        [Test]
        public void Run_CarriesLeftoverOfTheRoundWhereModuleWasChosen()
        {
            // N 만 나오는 표 · N 생산량 1 → R1 할당량 8 을 8번째에 돌파, 남은 뽑기 2
            RunManager run = CreateRun(1);
            DrawUntilCleared(run);
            Assert.AreEqual(2, run.DrawsRemaining);

            ModuleData carry = CreateModule(ModifierType.CarryOverDraws, 5);
            ModulePreview preview = run.PreviewModule(carry);
            Assert.AreEqual(10, preview.DrawsPerRoundBefore);
            Assert.AreEqual(12, preview.DrawsPerRoundAfter);
            Assert.AreEqual(2, preview.CarriedDrawsAfter);

            // 돌파 → 장착 → 진행 순서라 처음 고른 라운드의 잔여부터 넘어간다
            run.EquipModule(carry);
            run.AdvanceRound();
            Assert.AreEqual(12, run.DrawsRemaining);

            // R2 할당량 12 를 마지막 뽑기로 돌파 → 남은 0 → R3 은 기본 10
            DrawUntilCleared(run);
            Assert.AreEqual(0, run.DrawsRemaining);
            run.AdvanceRound();
            Assert.AreEqual(10, run.DrawsRemaining);
        }

        [Test]
        public void Run_CarryIsCappedAndRepeatsEveryRound()
        {
            // N 생산량 2 → R1 은 4번째에 돌파, 남은 6 → 이월 상한 5
            RunManager run = CreateRun(2);
            DrawUntilCleared(run);
            Assert.AreEqual(6, run.DrawsRemaining);

            run.EquipModule(CreateModule(ModifierType.CarryOverDraws, 5));
            run.AdvanceRound();
            Assert.AreEqual(15, run.DrawsRemaining);

            // R2 할당량 12 → 6번째에 돌파, 남은 9 → 다시 상한 5 만큼 이월
            DrawUntilCleared(run);
            Assert.AreEqual(9, run.DrawsRemaining);
            run.AdvanceRound();
            Assert.AreEqual(15, run.DrawsRemaining);
        }

        [Test]
        public void Run_WithoutCarryModule_LeftoverIsDropped()
        {
            RunManager run = CreateRun(2);
            DrawUntilCleared(run);
            Assert.AreEqual(6, run.DrawsRemaining);

            run.AdvanceRound();
            Assert.AreEqual(10, run.DrawsRemaining);
        }

        [Test]
        public void Preview_ExpectedOutputUsesCarriedDraws()
        {
            RunManager run = CreateRun(2);
            DrawUntilCleared(run);

            ModulePreview preview = run.PreviewModule(CreateModule(ModifierType.CarryOverDraws, 5));

            // N 만 나오고 생산량 2: 전 10회 × 2 = 20, 후 15회 × 2 = 30
            Assert.AreEqual(20.0, preview.ExpectedRoundOutputBefore(parts), Tolerance);
            Assert.AreEqual(30.0, preview.ExpectedRoundOutputAfter(parts), Tolerance);
        }

        [Test]
        public void Preview_BeforeKeepsCarryOfEquippedModule()
        {
            // 이미 이월 모듈을 낀 상태에서 다른 카드를 볼 때도 '전' 에 이월이 들어가야 한다
            RunManager run = CreateRun(2);
            DrawUntilCleared(run);
            run.EquipModule(CreateModule(ModifierType.CarryOverDraws, 5));
            run.AdvanceRound();

            // R2 는 15회 중 6번째에 돌파 → 남은 9
            DrawUntilCleared(run);
            Assert.AreEqual(9, run.DrawsRemaining);

            ModuleData extra = CreateModule(ModifierType.ExtraDraws, 2);
            ModulePreview preview = run.PreviewModule(extra);
            Assert.AreEqual(15, preview.DrawsPerRoundBefore);
            Assert.AreEqual(17, preview.DrawsPerRoundAfter);
            Assert.AreEqual(5, preview.CarriedDrawsAfter);
            Assert.AreEqual(30.0, preview.ExpectedRoundOutputBefore(parts), Tolerance);
            Assert.AreEqual(34.0, preview.ExpectedRoundOutputAfter(parts), Tolerance);

            // 새 카드를 장착해도 이월 효과가 남아 있어야 한다
            run.EquipModule(extra);
            run.AdvanceRound();
            Assert.AreEqual(17, run.DrawsRemaining);
        }

        private RunManager CreateRun(int normalOutput)
        {
            parts = new[]
            {
                CreatePart("N1", Grade.N, normalOutput),
                CreatePart("R1", Grade.R, 3),
                CreatePart("SR1", Grade.SR, 10),
            };
            var random = new RandomService(1);
            var gachaEngine = new GachaEngine(random, NoPity, 1.0, 0.0, 0.0);
            return new RunManager(gachaEngine, new PartCatalog(parts, random), 8, 10);
        }

        private static void DrawUntilCleared(RunManager run)
        {
            while (run.State == RunState.Drawing)
            {
                run.Draw();
            }

            Assert.AreEqual(RunState.RoundCleared, run.State);
        }

        private PartData CreatePart(string displayName, Grade grade, int baseOutput)
        {
            var part = ScriptableObject.CreateInstance<PartData>();
            part.InitializeForTests(displayName, grade, baseOutput);
            createdObjects.Add(part);
            return part;
        }

        private ModuleData CreateModule(ModifierType type, float value)
        {
            var module = ScriptableObject.CreateInstance<ModuleData>();
            module.InitializeForTests(type.ToString(), new ModifierEffect(type, GradeFilter.Any, value));
            createdObjects.Add(module);
            return module;
        }
    }
}
