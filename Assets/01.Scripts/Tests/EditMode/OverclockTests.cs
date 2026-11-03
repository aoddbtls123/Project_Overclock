using System;
using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    // 오버클럭 (A2): 라운드마다 1번 · 켠 뒤 3회 · N 0 · R · SR 배수 증가분 +1 (×8 상한 안) · 천장 영향 없음 · 난수 미사용.
    // 기대값은 기획 숫자 리터럴로 쓴다 (구현 상수 OverclockDraws · OverclockMultiplierBonus 를 쓰지 않는다)
    public class OverclockTests
    {
        private const int NoPity = int.MaxValue;

        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

        private PartData normal;
        private PartData rare;
        private PartData superRare;

        [SetUp]
        public void CreateParts()
        {
            normal = CreatePart("N1", Grade.N, 1, PartTag.Mechanical);
            rare = CreatePart("R1", Grade.R, 3, PartTag.Power);
            superRare = CreatePart("SR1", Grade.SR, 10, PartTag.Power);
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

        private PartData CreatePart(string displayName, Grade grade, int baseOutput, PartTag tag)
        {
            var part = ScriptableObject.CreateInstance<PartData>();
            part.InitializeForTests(displayName, grade, baseOutput, tag);
            createdObjects.Add(part);
            return part;
        }

        private ModuleData CreateModule(string id, ModuleAxis axis, ModifierType type, GradeFilter target, float value)
        {
            var module = ScriptableObject.CreateInstance<ModuleData>();
            module.InitializeForTests(id, axis, new ModifierEffect(type, target, value));
            module.SetIdForTests(id);
            createdObjects.Add(module);
            return module;
        }

        private RunManager CreateFixedGradeRun(double n, double r, double sr, int drawsPerRound)
        {
            var random = new RandomService(1);
            var parts = new List<PartData> { normal, rare, superRare };
            var engine = new GachaEngine(random, NoPity, n, r, sr);
            return new RunManager(engine, new PartCatalog(parts, random), 8, drawsPerRound);
        }

        [Test]
        public void Normal_GivesZeroForThreeDrawsThenNormalOutput()
        {
            RunManager run = CreateFixedGradeRun(1.0, 0.0, 0.0, 10);
            Assert.IsTrue(run.CanActivateOverclock);

            run.ActivateOverclock();
            Assert.AreEqual(3, run.OverclockDrawsLeft);

            var outputs = new List<int>();
            var flags = new List<bool>();
            while (run.State == RunState.Drawing)
            {
                DrawResult result = run.Draw();
                outputs.Add(result.Output);
                flags.Add(result.Overclocked);
            }

            CollectionAssert.AreEqual(new[] { 0, 0, 0, 1, 1, 1, 1, 1, 1, 1 }, outputs);
            CollectionAssert.AreEqual(new[] { true, true, true, false, false, false, false, false, false, false }, flags);
            Assert.AreEqual(7, run.Production);
            Assert.AreEqual(RunState.RunFailed, run.State);
            Assert.AreEqual(0, run.OverclockDrawsLeft);
        }

        [Test]
        public void Rare_DoublesWithoutModules()
        {
            RunManager run = CreateFixedGradeRun(0.0, 1.0, 0.0, 10);
            run.ActivateOverclock();

            DrawResult first = run.Draw();
            Assert.AreEqual(6, first.Output);
            Assert.IsTrue(first.Overclocked);
            Assert.AreEqual(2, run.OverclockDrawsLeft);
        }

        [Test]
        public void SuperRare_AddsToModuleMultiplier()
        {
            RunManager run = CreateFixedGradeRun(0.0, 0.0, 1.0, 10);
            run.EquipModule(CreateModule("coil", ModuleAxis.Multiplier, ModifierType.OutputMultiplier, GradeFilter.Any, 0.3f));

            Assert.AreEqual(13, run.Draw().Output, "켜기 전 10 × 1.3");
            run.AdvanceRound();
            run.ActivateOverclock();
            Assert.AreEqual(23, run.Draw().Output, "10 × (1 + 0.3 + 1)");
        }

        [Test]
        public void Multiplier_StaysWithinEightTimesCap()
        {
            RunManager run = CreateFixedGradeRun(0.0, 1.0, 0.0, 10);
            run.EquipModule(CreateModule("big", ModuleAxis.Multiplier, ModifierType.OutputMultiplier, GradeFilter.Any, 7f));
            run.ActivateOverclock();

            Assert.AreEqual(24, run.Draw().Output, "3 × min(1 + 7 + 1, 8)");
        }

        [Test]
        public void Normal_ZeroIncludesStreakBonusButStreakStillCounts()
        {
            RunManager run = CreateFixedGradeRun(1.0, 0.0, 0.0, 20);
            run.EquipModule(CreateModule("sync", ModuleAxis.Conditional, ModifierType.StreakBonus, GradeFilter.Any, 10f));
            run.ActivateOverclock();

            DrawResult first = run.Draw();
            DrawResult second = run.Draw();
            DrawResult third = run.Draw();
            Assert.IsTrue(third.StreakTriggered, "N 이 0 이어도 연속은 센다");
            Assert.AreEqual(0, first.Output + second.Output + third.Output, "3연속 보너스 포함 0");

            run.Draw();
            run.Draw();
            DrawResult sixth = run.Draw();
            Assert.IsTrue(sixth.StreakTriggered);
            Assert.IsFalse(sixth.Overclocked);
            Assert.AreEqual(11, sixth.Output, "끈 뒤에는 1 + 10");
        }

        [Test]
        public void OncePerRound_ThenAvailableAgainNextRound()
        {
            RunManager run = CreateFixedGradeRun(0.0, 1.0, 0.0, 10);
            run.ActivateOverclock();
            Assert.IsFalse(run.CanActivateOverclock);
            Assert.IsTrue(run.OverclockUsed);
            Assert.Throws<InvalidOperationException>(() => run.ActivateOverclock());

            run.Draw();
            run.Draw();
            Assert.AreEqual(RunState.RoundCleared, run.State, "6 + 6 ≥ 8");
            Assert.AreEqual(0, run.OverclockDrawsLeft, "돌파하면 남은 1회는 사라진다");
            Assert.IsFalse(run.CanActivateOverclock, "돌파 상태에서는 못 켠다");
            Assert.Throws<InvalidOperationException>(() => run.ActivateOverclock());

            run.AdvanceRound();
            Assert.IsFalse(run.OverclockUsed);
            Assert.IsTrue(run.CanActivateOverclock);
            Assert.AreEqual(3, run.Draw().Output, "새 라운드는 꺼진 채 시작");
        }

        // 이번 라운드에 안 켰어도 돌파 · 실패 상태에서는 못 켠다 (위 테스트는 이미 켠 라운드라 이 경우를 가리지 못함)
        [Test]
        public void Activate_RejectedWhenNotDrawingEvenIfUnused()
        {
            RunManager cleared = CreateFixedGradeRun(0.0, 1.0, 0.0, 10);
            cleared.Draw();
            cleared.Draw();
            cleared.Draw();
            Assert.AreEqual(RunState.RoundCleared, cleared.State, "(전제) 3 × 3 ≥ 8");
            Assert.IsFalse(cleared.OverclockUsed);
            Assert.IsFalse(cleared.CanActivateOverclock);
            Assert.Throws<InvalidOperationException>(() => cleared.ActivateOverclock());

            RunManager failed = CreateFixedGradeRun(1.0, 0.0, 0.0, 7);
            for (int i = 0; i < 7; i++)
            {
                failed.Draw();
            }

            Assert.AreEqual(RunState.RunFailed, failed.State, "(전제) 7 < 8");
            Assert.IsFalse(failed.CanActivateOverclock);
            Assert.Throws<InvalidOperationException>(() => failed.ActivateOverclock());
        }

        [Test]
        public void DrawsLeft_LimitedByRemainingDraws()
        {
            // 9회 중 7회를 쓰면 생산량 7 (할당량 8 미달) · 남은 뽑기 2
            RunManager run = CreateFixedGradeRun(1.0, 0.0, 0.0, 9);
            for (int i = 0; i < 7; i++)
            {
                run.Draw();
            }

            run.ActivateOverclock();
            Assert.AreEqual(2, run.OverclockDrawsLeft);
        }

        [Test]
        public void Activate_DoesNotUseRandomOrChangeParts()
        {
            PartData[] parts = { normal, rare, superRare };
            ModuleData[] modules = { CreateModule("extra", ModuleAxis.Draws, ModifierType.ExtraDraws, GradeFilter.Any, 2f) };
            RunSession plain = RunSession.Create(11, parts, modules);
            RunSession boosted = RunSession.Create(11, parts, modules);

            uint before = boosted.CaptureSaveData().randomState;
            boosted.RunManager.ActivateOverclock();
            Assert.AreEqual(before, boosted.CaptureSaveData().randomState);

            for (int i = 0; i < 3 && plain.RunManager.State == RunState.Drawing && boosted.RunManager.State == RunState.Drawing; i++)
            {
                Assert.AreSame(plain.RunManager.Draw().Part, boosted.RunManager.Draw().Part, "draw " + i);
            }

            Assert.AreEqual(plain.CaptureSaveData().randomState, boosted.CaptureSaveData().randomState);
            Assert.AreEqual(plain.GachaEngine.DrawsWithoutSuperRare, boosted.GachaEngine.DrawsWithoutSuperRare, "천장 카운터 영향 없음");
        }

        // 저장 → 복원한 판이 원래 판과 끝까지 같아야 한다. 오버클럭이 걸린 채, 쓴 뒤, 안 쓴 라운드 모두 저장해 본다
        [Test]
        public void RestoreAnywhere_ContinuesExactlyLikeOriginal()
        {
            PartData[] parts = { normal, rare, superRare };
            ModuleData[] modules =
            {
                CreateModule("coil", ModuleAxis.Multiplier, ModifierType.OutputMultiplier, GradeFilter.Any, 0.3f),
                CreateModule("sync", ModuleAxis.Conditional, ModifierType.StreakBonus, GradeFilter.Any, 10f),
                CreateModule("extra", ModuleAxis.Draws, ModifierType.ExtraDraws, GradeFilter.Any, 2f),
            };

            int checkpointsWhileActive = 0;
            int checkpointsUsedNotActive = 0;
            for (uint seed = 1; seed <= 20; seed++)
            {
                List<string> expected = RunToEnd(RunSession.Create(seed, parts, modules));
                RunSession original = RunSession.Create(seed, parts, modules);
                for (int step = 0; step < expected.Count; step++)
                {
                    RunSaveData data = original.CaptureSaveData();
                    RunSession restored = RunSession.Restore(data, parts, modules);
                    Assert.AreEqual(data.overclockUsed, restored.RunManager.OverclockUsed, "seed=" + seed + " step=" + step);
                    Assert.AreEqual(data.overclockDrawsLeft, restored.RunManager.OverclockDrawsLeft, "seed=" + seed + " step=" + step);

                    List<string> actual = RunToEnd(restored);
                    Assert.AreEqual(expected.Count - step, actual.Count, "seed=" + seed + " step=" + step + " 남은 단계 수");
                    for (int i = 0; i < actual.Count; i++)
                    {
                        Assert.AreEqual(expected[step + i], actual[i], "seed=" + seed + " step=" + step + " +" + i);
                    }

                    if (data.overclockDrawsLeft > 0)
                    {
                        checkpointsWhileActive++;
                    }
                    else if (data.overclockUsed)
                    {
                        checkpointsUsedNotActive++;
                    }

                    Step(original);
                }
            }

            Assert.GreaterOrEqual(checkpointsWhileActive, 50, "오버클럭이 걸린 채 저장");
            Assert.GreaterOrEqual(checkpointsUsedNotActive, 50, "쓴 뒤 저장");
        }

        [Test]
        public void Restore_RejectsImpossibleOverclockState()
        {
            PartData[] parts = { normal, rare, superRare };
            ModuleData[] modules = { CreateModule("extra", ModuleAxis.Draws, ModifierType.ExtraDraws, GradeFilter.Any, 2f) };
            RunSession session = RunSession.Create(5, parts, modules);
            session.RunManager.ActivateOverclock();
            session.RunManager.Draw();
            Assert.AreEqual(RunState.Drawing, session.RunManager.State, "(전제) 아직 뽑기 중");
            RunSaveData valid = session.CaptureSaveData();
            Assert.AreEqual(2, valid.overclockDrawsLeft);

            // 고치지 않은 값과 옛 세이브(값 없음 = false / 0)는 복원되어야 한다
            RunSession.Restore(Copy(valid, valid.overclockUsed, valid.overclockDrawsLeft), parts, modules);
            RunSession.Restore(Copy(valid, false, 0), parts, modules);

            AssertRejected(Copy(valid, true, 4), parts, modules, "남은 4");
            AssertRejected(Copy(valid, true, -1), parts, modules, "남은 -1");
            AssertRejected(Copy(valid, false, 2), parts, modules, "안 켰는데 남음");

            RunSaveData fewDraws = Copy(valid, true, 2);
            fewDraws.drawsRemaining = 1;
            AssertRejected(fewDraws, parts, modules, "남은 뽑기보다 많음");

            RunSaveData cleared = Copy(valid, true, 1);
            cleared.state = RunState.RoundCleared;
            cleared.production = RunManager.QuotaFor(cleared.round);
            AssertRejected(cleared, parts, modules, "돌파인데 남음");
        }

        private static void AssertRejected(RunSaveData data, PartData[] parts, ModuleData[] modules, string name)
        {
            // 범위 밖 값은 ArgumentOutOfRangeException (ArgumentException 의 하위) 이라 둘 다 받는다
            try
            {
                RunSession.Restore(data, parts, modules);
            }
            catch (ArgumentException)
            {
                return;
            }

            Assert.Fail("복원되면 안 됨: " + name);
        }

        private static RunSaveData Copy(RunSaveData source, bool used, int drawsLeft)
        {
            return new RunSaveData
            {
                version = source.version,
                mode = source.mode,
                seed = source.seed,
                randomState = source.randomState,
                drawsWithoutSuperRare = source.drawsWithoutSuperRare,
                round = source.round,
                production = source.production,
                drawsRemaining = source.drawsRemaining,
                state = source.state,
                streakGrade = source.streakGrade,
                streakCount = source.streakCount,
                overclockUsed = used,
                overclockDrawsLeft = drawsLeft,
                equippedModuleIds = (string[])source.equippedModuleIds.Clone(),
                offeredModuleIds = (string[])source.offeredModuleIds.Clone(),
            };
        }

        // 정책: 라운드마다 첫 뽑기 뒤에 켠다 (켜기 전 · 켠 직후 · 걸린 중 · 쓴 뒤가 모두 저장 시점에 나오게)
        private static List<string> RunToEnd(RunSession session)
        {
            var trace = new List<string>();
            while (true)
            {
                string line = Step(session);
                if (line == null)
                {
                    return trace;
                }

                trace.Add(line);
            }
        }

        private static string Step(RunSession session)
        {
            RunManager run = session.RunManager;
            if (run.State == RunState.Drawing)
            {
                if (run.CanActivateOverclock && run.Production > 0)
                {
                    run.ActivateOverclock();
                    return "켬 R" + run.CurrentRound + " 남은 " + run.OverclockDrawsLeft;
                }

                DrawResult result = run.Draw();
                return "뽑기 " + result.Part.DisplayName + " +" + result.Output + " " + result.StreakTriggered + " " + result.Overclocked
                    + " | R" + run.CurrentRound + " 생산 " + run.Production + " 남은 " + run.DrawsRemaining + " " + run.State + " 오버 " + run.OverclockDrawsLeft;
            }

            if (run.State == RunState.RoundCleared)
            {
                IReadOnlyList<ModuleData> offer = session.OpenOffer();
                session.SelectModule(offer[0]);
                return "선택 " + offer[0].Id + " | R" + run.CurrentRound + " 남은 " + run.DrawsRemaining;
            }

            return null;
        }
    }
}
