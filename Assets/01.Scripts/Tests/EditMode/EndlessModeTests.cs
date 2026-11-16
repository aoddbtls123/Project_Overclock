using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    public class EndlessModeTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

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

        private ModuleData CreateModule(string displayName, ModuleAxis axis, ModifierType type, float value)
        {
            var module = ScriptableObject.CreateInstance<ModuleData>();
            module.InitializeForTests(displayName, axis, new ModifierEffect(type, GradeFilter.Any, value));
            createdObjects.Add(module);
            return module;
        }

        private PartData[] DesignParts()
        {
            return new[]
            {
                CreatePart("N1", Grade.N, 1),
                CreatePart("N2", Grade.N, 1),
                CreatePart("R1", Grade.R, 3),
                CreatePart("SR1", Grade.SR, 10),
            };
        }

        // 뽑기 한 번에 1000 → 라운드마다 몇 번 안에 반드시 돌파함 (R20 할당량 9313 < 10회 × 1000)
        private PartData[] HugeParts()
        {
            return new[]
            {
                CreatePart("N", Grade.N, 1000),
                CreatePart("R", Grade.R, 1000),
                CreatePart("SR", Grade.SR, 1000),
            };
        }

        private ModuleData[] Modules()
        {
            return new[]
            {
                CreateModule("교정", ModuleAxis.Probability, ModifierType.SuperRareChance, 0.05f),
                CreateModule("코일", ModuleAxis.Multiplier, ModifierType.OutputMultiplier, 0.3f),
                CreateModule("동기화", ModuleAxis.Conditional, ModifierType.StreakBonus, 10f),
                CreateModule("과부하", ModuleAxis.Draws, ModifierType.ExtraDraws, 2f),
            };
        }

        private static void DrawUntilRoundEnds(RunManager run)
        {
            while (run.State == RunState.Drawing)
            {
                run.Draw();
            }
        }

        [Test]
        public void Create_WithoutMode_IsNormal()
        {
            RunSession session = RunSession.Create(1, DesignParts(), Modules());

            Assert.AreEqual(RunMode.Normal, session.Mode);
            Assert.AreEqual(8, session.RunManager.RoundCount);
            Assert.AreEqual(RunMode.Endless, RunSession.Create(1, DesignParts(), Modules(), RunMode.Endless).Mode);
        }

        [Test]
        public void Create_RejectsUnknownMode()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => RunSession.Create(1, DesignParts(), Modules(), (RunMode)2));
        }

        [Test]
        public void Normal_ClearsAtRoundEight_EndlessKeepsOffering()
        {
            PartData[] parts = HugeParts();
            RunSession normal = RunSession.Create(7, parts, Modules(), RunMode.Normal);
            RunSession endless = RunSession.Create(7, parts, Modules(), RunMode.Endless);

            for (int round = 1; round <= 7; round++)
            {
                DrawUntilRoundEnds(normal.RunManager);
                DrawUntilRoundEnds(endless.RunManager);
                Assert.AreEqual(RunState.RoundCleared, normal.RunManager.State, "R" + round);
                Assert.AreEqual(RunState.RoundCleared, endless.RunManager.State, "R" + round);
                normal.RunManager.AdvanceRound();
                endless.RunManager.AdvanceRound();
            }

            DrawUntilRoundEnds(normal.RunManager);
            DrawUntilRoundEnds(endless.RunManager);
            Assert.AreEqual(RunState.RunCleared, normal.RunManager.State, "일반모드는 R8 돌파 = 클리어");
            Assert.AreEqual(RunState.RoundCleared, endless.RunManager.State, "무한모드는 R8 돌파 뒤에도 모듈 선택");

            RunManager run = endless.RunManager;
            for (int round = 9; round <= 15; round++)
            {
                Assert.AreEqual(3, endless.ModuleOffer.CreateOffer().Count, "R" + (round - 1) + " 돌파 제안");
                run.AdvanceRound();
                Assert.AreEqual(round, run.CurrentRound);
                Assert.AreEqual(new[] { 156, 227, 329, 477, 691, 1002, 1453 }[round - 9], run.Quota, "R" + round + " 할당량");
                DrawUntilRoundEnds(run);
                Assert.AreEqual(RunState.RoundCleared, run.State, "R" + round);
            }
        }

        [Test]
        public void Endless_EndsByFailingQuota()
        {
            // 할당량 8 을 못 채우는 부품 → R1 에서 셧다운. 무한모드도 실패 규칙은 같음
            var parts = new[] { CreatePart("N", Grade.N, 0), CreatePart("R", Grade.R, 0), CreatePart("SR", Grade.SR, 0) };
            RunSession endless = RunSession.Create(3, parts, Modules(), RunMode.Endless);

            DrawUntilRoundEnds(endless.RunManager);

            Assert.AreEqual(RunState.RunFailed, endless.RunManager.State);
            Assert.AreEqual(1, endless.RunManager.CurrentRound);
        }

        [Test]
        public void Endless_MatchesNormalThroughRoundEight()
        {
            // 같은 시드 · 같은 선택이면 R8 까지는 뽑기 결과와 진행 상태가 일반모드와 같아야 함 (난수 소비 · 계산이 같음).
            // 상태는 R8 돌파에서만 갈림 (일반 = 클리어, 무한 = 모듈 선택)
            PartData[] parts = DesignParts();
            ModuleData[] modules = Modules();
            int clearedRuns = 0;
            for (uint seed = 1; seed <= 300; seed++)
            {
                RunSession normal = RunSession.Create(seed, parts, modules, RunMode.Normal);
                RunSession endless = RunSession.Create(seed, parts, modules, RunMode.Endless);
                RunManager a = normal.RunManager;
                RunManager b = endless.RunManager;

                while (a.State == RunState.Drawing || a.State == RunState.RoundCleared)
                {
                    if (a.State == RunState.Drawing)
                    {
                        DrawResult resultA = a.Draw();
                        DrawResult resultB = b.Draw();
                        string at = "seed " + seed + " R" + a.CurrentRound;
                        Assert.AreSame(resultA.Part, resultB.Part, at);
                        Assert.AreEqual(resultA.Output, resultB.Output, at + " 생산량");
                        Assert.AreEqual(resultA.StreakTriggered, resultB.StreakTriggered, at + " 3연속");
                        Assert.AreEqual(a.Production, b.Production, at + " 누적 생산량");
                        Assert.AreEqual(a.DrawsRemaining, b.DrawsRemaining, at + " 남은 뽑기");
                        Assert.AreEqual(a.CurrentRound, b.CurrentRound, at);

                        if (a.State == RunState.RunCleared)
                        {
                            Assert.AreEqual(RunState.RoundCleared, b.State, at + " R8 돌파 뒤");
                        }
                        else
                        {
                            Assert.AreEqual(a.State, b.State, at);
                        }

                        continue;
                    }

                    List<ModuleData> offerA = normal.ModuleOffer.CreateOffer();
                    List<ModuleData> offerB = endless.ModuleOffer.CreateOffer();
                    Assert.AreEqual(offerA.Count, offerB.Count, "seed " + seed);
                    for (int i = 0; i < offerA.Count; i++)
                    {
                        Assert.AreSame(offerA[i], offerB[i], "seed " + seed);
                    }

                    a.EquipModule(offerA[0]);
                    b.EquipModule(offerB[0]);
                    a.AdvanceRound();
                    b.AdvanceRound();
                    Assert.AreEqual(a.DrawsRemaining, b.DrawsRemaining, "seed " + seed + " R" + a.CurrentRound + " 시작 뽑기");
                }

                if (a.State == RunState.RunCleared)
                {
                    clearedRuns++;
                }
            }

            Assert.GreaterOrEqual(clearedRuns, 1, "R8 을 돌파하는 시드가 하나는 있어야 R8 뒤 갈림을 검사한다");
        }

        [Test]
        public void QuotaFor_StaysWithinIntForLateRounds()
        {
            Assert.AreEqual(1968911081, RunManager.QuotaFor(53));
            Assert.AreEqual(int.MaxValue, RunManager.QuotaFor(54));
            Assert.AreEqual(int.MaxValue, RunManager.QuotaFor(200));

            for (int round = 2; round <= 200; round++)
            {
                Assert.GreaterOrEqual(RunManager.QuotaFor(round), RunManager.QuotaFor(round - 1), "R" + round);
            }
        }
    }
}
