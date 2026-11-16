using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    // 제안 다시 받기 (11/06): 돌파마다 1번 · 대가 없음 · 처음 제안과 같은 규칙 · 세이브에 남음
    public class OfferRerollTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

        private PartData[] parts;
        private ModuleData[] modules;

        [SetUp]
        public void CreateData()
        {
            parts = new[]
            {
                CreatePart("볼트", Grade.N, 1, PartTag.Mechanical),
                CreatePart("전선", Grade.N, 1, PartTag.Electronic),
                CreatePart("코일", Grade.R, 3, PartTag.Power),
                CreatePart("기어", Grade.R, 3, PartTag.Mechanical),
                CreatePart("코어", Grade.SR, 10, PartTag.Power),
            };

            // 축 4개 · 축마다 2개 이상 → 다시 받으면 다른 조합이 나올 수 있음
            modules = new[]
            {
                CreateModule("sr_up", ModuleAxis.Probability, ModifierType.SuperRareChance, 0.05f),
                CreateModule("r_up", ModuleAxis.Probability, ModifierType.RareChance, 0.1f),
                CreateModule("coil", ModuleAxis.Multiplier, ModifierType.OutputMultiplier, 0.3f),
                CreateModule("flat", ModuleAxis.Multiplier, ModifierType.FlatBonus, 1f),
                CreateModule("sync", ModuleAxis.Conditional, ModifierType.StreakBonus, 10f),
                CreateModule("sync2", ModuleAxis.Conditional, ModifierType.StreakBonus, 5f),
                CreateModule("extra", ModuleAxis.Draws, ModifierType.ExtraDraws, 2f),
                CreateModule("carry", ModuleAxis.Draws, ModifierType.CarryOverDraws, 5f),
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

        [Test]
        public void Reroll_ReplacesOfferWithNextOfferFromSharedRandom()
        {
            RunSession session = CreateClearedSession(out uint seed);
            RunSession mirror = RunSession.Create(seed, parts, modules);
            DrawUntilNotDrawing(mirror);

            session.OpenOffer();
            mirror.OpenOffer();
            IReadOnlyList<ModuleData> rerolled = session.RerollOffer();

            // 다시 받은 카드 = 같은 난수 흐름에서 CreateOffer 를 한 번 더 부른 결과
            List<ModuleData> expected = mirror.ModuleOffer.CreateOffer();
            Assert.AreEqual(expected.Count, rerolled.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreSame(expected[i], rerolled[i], "카드 " + i);
            }

            // 카드만 맞고 난수를 더 쓰면 뒤 판이 달라짐 → 난수 상태도 같아야 함
            Assert.AreEqual(mirror.CaptureSaveData().randomState, session.CaptureSaveData().randomState, "난수 소비 = CreateOffer 한 번");
            Assert.AreSame(rerolled, session.CurrentOffer);
            Assert.IsTrue(session.ModuleOffer.IsPossibleOffer(rerolled), "규칙에 맞는 조합");
        }

        [Test]
        public void Reroll_OnlyOncePerBreakthrough_AndAvailableAgainNextBreakthrough()
        {
            RunSession session = FindSessionClearingRound(2);

            session.OpenOffer();
            Assert.IsTrue(session.CanRerollOffer);
            session.RerollOffer();
            Assert.IsFalse(session.CanRerollOffer);
            Assert.IsTrue(session.OfferRerolled);
            Assert.Throws<InvalidOperationException>(() => session.RerollOffer());

            // 같은 돌파에서 제안을 다시 열어도 (화면 갱신) 다시 받은 카드 그대로 · 횟수도 그대로
            IReadOnlyList<ModuleData> reopened = session.OpenOffer();
            Assert.AreSame(session.CurrentOffer, reopened);
            Assert.IsFalse(session.CanRerollOffer);

            session.SelectModule(session.CurrentOffer[0]);
            Assert.IsFalse(session.OfferRerolled, "고르면 초기화");
            Assert.IsFalse(session.CanRerollOffer, "다음 제안이 열리기 전에는 못 씀");

            DrawUntilNotDrawing(session);
            Assert.AreEqual(RunState.RoundCleared, session.RunManager.State, "(전제) R2 돌파");
            session.OpenOffer();
            Assert.IsTrue(session.CanRerollOffer, "다음 돌파에서 다시 1번");
        }

        [Test]
        public void Reroll_RequiresOpenOffer()
        {
            RunSession drawing = RunSession.Create(3, parts, modules);
            Assert.IsFalse(drawing.CanRerollOffer);
            Assert.Throws<InvalidOperationException>(() => drawing.RerollOffer());

            RunSession cleared = CreateClearedSession(out _);
            Assert.IsFalse(cleared.CanRerollOffer, "돌파했어도 제안을 열기 전");
            Assert.Throws<InvalidOperationException>(() => cleared.RerollOffer());
        }

        [Test]
        public void Reroll_HasNoCost()
        {
            // 대가 없음: 다시 받아도 진행 · 보증 · 장착 · 다음 라운드 뽑기가 같음
            RunSession session = CreateClearedSession(out uint seed);
            RunSession mirror = RunSession.Create(seed, parts, modules);
            DrawUntilNotDrawing(mirror);
            session.OpenOffer();
            mirror.OpenOffer();

            session.RerollOffer();
            Assert.AreEqual(mirror.RunManager.CurrentRound, session.RunManager.CurrentRound);
            Assert.AreEqual(mirror.RunManager.Production, session.RunManager.Production);
            Assert.AreEqual(mirror.RunManager.DrawsRemaining, session.RunManager.DrawsRemaining);
            Assert.AreEqual(mirror.GachaEngine.DrawsWithoutSuperRare, session.GachaEngine.DrawsWithoutSuperRare);
            Assert.AreEqual(RunState.RoundCleared, session.RunManager.State);

            // 다음 라운드 뽑기 = 미리보기 값 그대로 (미리보기는 다시 받기를 모름 → 깎였으면 달라짐)
            ModuleData choice = session.CurrentOffer[0];
            int expectedDraws = session.RunManager.PreviewModule(choice).DrawsPerRoundAfter;
            session.SelectModule(choice);
            Assert.AreEqual(expectedDraws, session.RunManager.DrawsRemaining, "다음 라운드 뽑기 그대로");
        }

        [Test]
        public void SaveAfterReroll_RestoresRerolledCardsAndUsedFlag()
        {
            RunSession session = CreateClearedSession(out _);
            session.OpenOffer();
            IReadOnlyList<ModuleData> rerolled = session.RerollOffer();

            RunSaveData data = session.CaptureSaveData();
            Assert.IsTrue(data.offerRerolled);
            Assert.AreEqual(rerolled.Count, data.offeredModuleIds.Length);

            RunSession restored = RunSession.Restore(data, parts, modules);
            Assert.IsTrue(restored.OfferRerolled);
            Assert.IsFalse(restored.CanRerollOffer, "복원해도 다시 못 받음 (껐다 켜서 횟수를 되살리지 못함)");
            for (int i = 0; i < rerolled.Count; i++)
            {
                Assert.AreSame(rerolled[i], restored.CurrentOffer[i]);
            }

            // 고른 뒤 이어지는 판도 원래 판과 같음
            session.SelectModule(session.CurrentOffer[1]);
            restored.SelectModule(restored.CurrentOffer[1]);
            Assert.AreEqual(session.CaptureSaveData().randomState, restored.CaptureSaveData().randomState);
            Assert.IsFalse(restored.CaptureSaveData().offerRerolled);
            for (int i = 0; i < 5 && session.RunManager.State == RunState.Drawing; i++)
            {
                Assert.AreEqual(session.RunManager.Draw().Output, restored.RunManager.Draw().Output, "뽑기 " + i);
            }
        }

        [Test]
        public void SaveBeforeReroll_KeepsRerollAvailable()
        {
            RunSession session = CreateClearedSession(out _);
            session.OpenOffer();

            RunSaveData data = session.CaptureSaveData();
            Assert.IsFalse(data.offerRerolled);
            RunSession restored = RunSession.Restore(data, parts, modules);
            Assert.IsTrue(restored.CanRerollOffer);
        }

        [Test]
        public void Restore_RejectsRerolledFlagWithoutOpenOffer()
        {
            // 돌파 직후 (카드를 열기 전) 저장 · 뽑기 중 저장에 "다시 받음" 이 붙어 있으면 고친 세이브임
            RunSession cleared = CreateClearedSession(out _);
            RunSaveData beforeOffer = cleared.CaptureSaveData();
            beforeOffer.offerRerolled = true;
            Assert.Throws<ArgumentException>(() => RunSession.Restore(beforeOffer, parts, modules));

            RunSaveData drawing = RunSession.Create(3, parts, modules).CaptureSaveData();
            drawing.offerRerolled = true;
            Assert.Throws<ArgumentException>(() => RunSession.Restore(drawing, parts, modules));
        }

        [Test]
        public void OldSaveWithoutField_ReadsAsNotRerolled()
        {
            RunSession session = CreateClearedSession(out _);
            session.OpenOffer();
            string json = JsonUtility.ToJson(session.CaptureSaveData(), true);
            string withoutField = Regex.Replace(json, ",\\s*\"offerRerolled\"\\s*:\\s*false", "");
            Assert.AreNotEqual(json, withoutField, "(전제) 필드를 지움");

            RunSaveData data = JsonUtility.FromJson<RunSaveData>(withoutField);
            RunSession restored = RunSession.Restore(data, parts, modules);
            Assert.IsFalse(restored.OfferRerolled);
            Assert.IsTrue(restored.CanRerollOffer);
        }

        private RunSession CreateClearedSession(out uint seed)
        {
            for (seed = 1; seed <= 200; seed++)
            {
                RunSession session = RunSession.Create(seed, parts, modules);
                DrawUntilNotDrawing(session);
                if (session.RunManager.State == RunState.RoundCleared)
                {
                    return session;
                }
            }

            Assert.Fail("R1 을 돌파하는 시드가 없음");
            return null;
        }

        // 1 라운드를 돌파한 뒤 (카드 0번을 골라) round 라운드까지 돌파하는 판의, R1 돌파 직후 상태
        private RunSession FindSessionClearingRound(int round)
        {
            for (uint seed = 1; seed <= 500; seed++)
            {
                RunSession probe = RunSession.Create(seed, parts, modules);
                bool cleared = true;
                for (int r = 1; r <= round; r++)
                {
                    DrawUntilNotDrawing(probe);
                    if (probe.RunManager.State != RunState.RoundCleared)
                    {
                        cleared = false;
                        break;
                    }

                    if (r == 1)
                    {
                        // 다시 받은 카드로 같은 선택을 해야 판이 같아짐 → 탐색도 다시 받은 뒤 0번을 고름
                        probe.OpenOffer();
                        probe.RerollOffer();
                        probe.SelectModule(probe.CurrentOffer[0]);
                    }
                    else if (r < round)
                    {
                        probe.OpenOffer();
                        probe.SelectModule(probe.CurrentOffer[0]);
                    }
                }

                if (cleared)
                {
                    RunSession session = RunSession.Create(seed, parts, modules);
                    DrawUntilNotDrawing(session);
                    return session;
                }
            }

            Assert.Fail("R" + round + " 까지 돌파하는 시드가 없음");
            return null;
        }

        private static void DrawUntilNotDrawing(RunSession session)
        {
            while (session.RunManager.State == RunState.Drawing)
            {
                session.RunManager.Draw();
            }
        }

        private PartData CreatePart(string displayName, Grade grade, int baseOutput, PartTag tag)
        {
            var part = ScriptableObject.CreateInstance<PartData>();
            part.InitializeForTests(displayName, grade, baseOutput, tag);
            createdObjects.Add(part);
            return part;
        }

        private ModuleData CreateModule(string id, ModuleAxis axis, ModifierType type, float value)
        {
            var module = ScriptableObject.CreateInstance<ModuleData>();
            module.InitializeForTests(id, axis, new ModifierEffect(type, GradeFilter.Any, value));
            module.SetIdForTests(id);
            createdObjects.Add(module);
            return module;
        }
    }
}
