using System;
using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    public class SaveRestoreTests
    {
        private const int StepLimit = 3000;

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

            // 효과 7종을 모두 넣는다. 복원 뒤 다시 계산하는 값(확률표 · 생산량 · 뽑기 횟수 · 이월)이 전부 결과에 드러나게 한다
            modules = new[]
            {
                CreateModule("sr_up", ModuleAxis.Probability, ModifierType.SuperRareChance, 0.05f),
                CreateModule("r_up", ModuleAxis.Probability, ModifierType.RareChance, 0.1f),
                CreateModule("coil", ModuleAxis.Multiplier, ModifierType.OutputMultiplier, 0.3f),
                CreateModule("flat", ModuleAxis.Multiplier, ModifierType.FlatBonus, 1f),
                CreateModule("sync", ModuleAxis.Conditional, ModifierType.StreakBonus, 10f),
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

        // 저장 → 복원한 판을 같은 선택으로 끝까지 돌리면 원래 판과 매 단계가 같아야 한다.
        // 단계 = 뽑기 1회 / 제안 열기 / 모듈 선택. 모든 단계 직후에 저장해 본다 (돌파 직후 · 카드가 열린 상태 포함)
        [TestCase(RunMode.Normal)]
        [TestCase(RunMode.Endless)]
        public void RestoreAfterAnyStep_ContinuesExactlyLikeOriginal(RunMode mode)
        {
            int checkpoints = 0;
            int checkpointsWithOpenOffer = 0;
            int checkpointsWithStreak = 0;
            int checkpointsAtPity = 0;
            for (uint seed = 1; seed <= 20; seed++)
            {
                List<string> expected = RunToEnd(RunSession.Create(seed, parts, modules, mode));

                RunSession original = RunSession.Create(seed, parts, modules, mode);
                for (int step = 0; step < expected.Count; step++)
                {
                    RunSaveData data = original.CaptureSaveData();
                    RunSession restored = RunSession.Restore(data, parts, modules);

                    Assert.AreEqual(Describe(data), Describe(restored.CaptureSaveData()), "seed=" + seed + " step=" + step + " 다시 저장한 값");
                    List<string> actual = RunToEnd(restored);
                    AssertSameTrace(expected, step, actual, "seed=" + seed + " step=" + step);

                    checkpoints++;
                    if (data.offeredModuleIds.Length > 0)
                    {
                        checkpointsWithOpenOffer++;
                    }

                    if (data.streakCount > 0)
                    {
                        checkpointsWithStreak++;
                    }

                    if (data.drawsWithoutSuperRare >= 15)
                    {
                        checkpointsAtPity++;
                    }

                    Step(original);
                }
            }

            // 저장 시점이 실제로 덮였는지 확인한다. 이 값이 0 이면 그 상태의 복원은 검사되지 않은 것이다
            Assert.GreaterOrEqual(checkpointsWithOpenOffer, 50, "카드가 열린 상태 저장");
            Assert.GreaterOrEqual(checkpointsWithStreak, 200, "연속 진행 중 저장");
            Assert.GreaterOrEqual(checkpointsAtPity, 20, "보증 15 이상에서 저장");
            Assert.GreaterOrEqual(checkpoints, 1000, "저장 횟수");
        }

        [Test]
        public void SaveData_HoldsCurrentValues()
        {
            RunSession session = RunSession.Create(7, parts, modules);
            AdvanceToFirstOpenOffer(session);
            RunManager runManager = session.RunManager;
            IReadOnlyList<ModuleData> offer = session.CurrentOffer;
            session.SelectModule(offer[0]);
            runManager.Draw();
            runManager.Draw();

            RunSaveData data = session.CaptureSaveData();

            Assert.AreEqual(RunSaveData.CurrentVersion, data.version);
            Assert.AreEqual(2, data.version);
            Assert.AreEqual(RunMode.Normal, data.mode);
            Assert.AreEqual(7u, data.seed);
            Assert.AreEqual(2, data.round);
            Assert.AreEqual(runManager.Production, data.production);
            Assert.AreEqual(runManager.DrawsRemaining, data.drawsRemaining);
            Assert.AreEqual(RunState.Drawing, data.state);
            Assert.AreEqual(session.GachaEngine.DrawsWithoutSuperRare, data.drawsWithoutSuperRare);
            Assert.AreEqual(runManager.StreakGrade, data.streakGrade);
            Assert.AreEqual(runManager.StreakCount, data.streakCount);
            Assert.AreEqual(1, data.equippedModuleIds.Length);
            Assert.AreEqual(offer[0].Id, data.equippedModuleIds[0]);
            Assert.AreEqual(0, data.offeredModuleIds.Length);
        }

        [Test]
        public void SaveData_RandomStateIsNextRandomValueSource()
        {
            RunSession session = RunSession.Create(11, parts, modules);
            session.RunManager.Draw();
            RunSaveData data = session.CaptureSaveData();

            // 같은 상태에서 시작한 난수원이 다음 뽑기 난수를 그대로 낸다 = 저장한 값이 실제 게임 난수 상태다
            var mirror = new RandomService(1);
            mirror.RestoreState(data.randomState);
            double nextRoll = mirror.NextDouble();
            var gradeMirror = new GachaEngine(new RandomService(1), 50, 0.72, 0.25, 0.03);

            DrawResult result = session.RunManager.Draw();
            Assert.AreEqual(gradeMirror.Resolve(nextRoll), result.Part.Grade);

            Assert.AreNotEqual(data.randomState, session.CaptureSaveData().randomState);
        }

        [Test]
        public void OpenOffer_SecondCallReturnsSameCardsWithoutUsingRandom()
        {
            RunSession session = RunSession.Create(3, parts, modules);
            IReadOnlyList<ModuleData> first = AdvanceToFirstOpenOffer(session);
            uint stateAfterFirst = session.CaptureSaveData().randomState;

            IReadOnlyList<ModuleData> second = session.OpenOffer();

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreSame(first[i], second[i]);
            }

            Assert.AreEqual(stateAfterFirst, session.CaptureSaveData().randomState);
        }

        [Test]
        public void OpenOffer_UsesSameRandomAsModuleOffer()
        {
            RunSession session = RunSession.Create(5, parts, modules);
            RunSession mirror = RunSession.Create(5, parts, modules);
            while (session.RunManager.State == RunState.Drawing)
            {
                session.RunManager.Draw();
                mirror.RunManager.Draw();
            }

            IReadOnlyList<ModuleData> offer = session.OpenOffer();
            List<ModuleData> expected = mirror.ModuleOffer.CreateOffer();

            Assert.AreEqual(expected.Count, offer.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreSame(expected[i], offer[i]);
            }
        }

        [Test]
        public void OpenOffer_RejectsWhenRoundIsNotCleared()
        {
            RunSession session = RunSession.Create(3, parts, modules);

            Assert.Throws<InvalidOperationException>(() => session.OpenOffer());
        }

        [Test]
        public void SelectModule_RequiresOpenOfferAndCardFromIt()
        {
            RunSession session = RunSession.Create(3, parts, modules);
            while (session.RunManager.State == RunState.Drawing)
            {
                session.RunManager.Draw();
            }

            Assert.Throws<InvalidOperationException>(() => session.SelectModule(modules[0]));

            IReadOnlyList<ModuleData> offer = session.OpenOffer();
            ModuleData notOffered = null;
            foreach (ModuleData module in modules)
            {
                if (!Contains(offer, module))
                {
                    notOffered = module;
                    break;
                }
            }

            Assert.Throws<ArgumentException>(() => session.SelectModule(notOffered));
            Assert.AreEqual(RunState.RoundCleared, session.RunManager.State);

            session.SelectModule(offer[1]);

            Assert.AreEqual(2, session.RunManager.CurrentRound);
            Assert.AreEqual(RunState.Drawing, session.RunManager.State);
            Assert.AreEqual(0, session.CurrentOffer.Count);
            Assert.AreSame(offer[1], session.RunManager.EquippedModules[0]);
        }

        // 화면이 하던 순서(장착 → 라운드 진행)를 그대로 옮겼는지. 순서가 바뀌면 추가 뽑기가 한 라운드 늦게 적용된다
        [Test]
        public void SelectModule_MatchesEquipThenAdvance()
        {
            int drawModuleChecks = 0;
            for (uint seed = 1; seed <= 30; seed++)
            {
                for (int card = 0; card < ModuleOffer.OfferSize; card++)
                {
                    RunSession session = RunSession.Create(seed, parts, modules);
                    RunSession mirror = RunSession.Create(seed, parts, modules);
                    while (session.RunManager.State == RunState.Drawing)
                    {
                        session.RunManager.Draw();
                        mirror.RunManager.Draw();
                    }

                    ModuleData choice = session.OpenOffer()[card];
                    session.SelectModule(choice);
                    List<ModuleData> mirrorOffer = mirror.ModuleOffer.CreateOffer();
                    mirror.RunManager.EquipModule(mirrorOffer[card]);
                    mirror.RunManager.AdvanceRound();

                    Assert.AreEqual(Describe(mirror), Describe(session), "seed=" + seed + " card=" + card);
                    if (choice.GetEffect(0).Type == ModifierType.ExtraDraws || choice.GetEffect(0).Type == ModifierType.CarryOverDraws)
                    {
                        drawModuleChecks++;
                    }
                }
            }

            Assert.GreaterOrEqual(drawModuleChecks, 5, "뽑기 횟수 모듈을 고른 경우");
        }

        [Test]
        public void EquippedModules_KeepOrderAndDuplicates()
        {
            RunSession session = RunSession.Create(3, parts, modules);
            RunManager runManager = session.RunManager;

            runManager.EquipModule(modules[2]);
            runManager.EquipModule(modules[4]);
            runManager.EquipModule(modules[2]);

            Assert.AreEqual(3, runManager.EquippedModules.Count);
            Assert.AreSame(modules[2], runManager.EquippedModules[0]);
            Assert.AreSame(modules[4], runManager.EquippedModules[1]);
            Assert.AreSame(modules[2], runManager.EquippedModules[2]);
        }

        [Test]
        public void Restore_KeepsOpenOfferCards()
        {
            RunSession session = RunSession.Create(9, parts, modules);
            IReadOnlyList<ModuleData> offer = AdvanceToFirstOpenOffer(session);

            RunSession restored = RunSession.Restore(session.CaptureSaveData(), parts, modules);
            IReadOnlyList<ModuleData> restoredOffer = restored.OpenOffer();

            Assert.AreEqual(offer.Count, restoredOffer.Count);
            for (int i = 0; i < offer.Count; i++)
            {
                Assert.AreSame(offer[i], restoredOffer[i]);
            }

            Assert.AreEqual(session.CaptureSaveData().randomState, restored.CaptureSaveData().randomState);
        }

        [Test]
        public void Restore_RejectsBrokenData()
        {
            RunSession session = RunSession.Create(7, parts, modules);
            AdvanceToFirstOpenOffer(session);
            session.SelectModule(session.CurrentOffer[0]);
            session.RunManager.Draw();
            RunSaveData valid = session.CaptureSaveData();
            int quota = RunManager.QuotaFor(valid.round);
            Assert.AreEqual(RunState.Drawing, valid.state);
            Assert.AreEqual(2, valid.round);

            // 고치지 않은 값은 복원되어야 한다. 그래야 아래 거부가 각 항목 때문이라고 말할 수 있다
            RunSession.Restore(Copy(valid), parts, modules);

            var cases = new List<KeyValuePair<string, Action<RunSaveData>>>
            {
                Case("버전 0", d => d.version = 0),
                Case("버전 3", d => d.version = 3),
                Case("모드 범위 밖", d => d.mode = (RunMode)5),
                Case("시드 uint 최댓값", d => d.seed = uint.MaxValue),
                Case("난수 상태 0", d => d.randomState = 0),
                Case("보증 -1", d => d.drawsWithoutSuperRare = -1),
                Case("보증 51", d => d.drawsWithoutSuperRare = 51),
                Case("라운드 0", d => d.round = 0),
                Case("일반모드 라운드 9", d => d.round = 9),
                Case("생산량 음수", d => d.production = -1),
                Case("남은 뽑기 21", d => d.drawsRemaining = 21),
                Case("뽑기 중인데 남은 뽑기 0", d => d.drawsRemaining = 0),
                Case("뽑기 중인데 할당량 도달", d => d.production = quota),
                Case("돌파인데 할당량 미달", d => d.state = RunState.RoundCleared),
                Case("실패인데 남은 뽑기 있음", d => d.state = RunState.RunFailed),
                Case("클리어인데 마지막 라운드 아님", d => { d.state = RunState.RunCleared; d.production = quota; }),
                Case("상태 범위 밖", d => { d.state = (RunState)9; d.drawsRemaining = 0; }),
                Case("연속 등급 범위 밖", d => d.streakGrade = (Grade)7),
                Case("연속 3", d => d.streakCount = 3),
                Case("연속 -1", d => d.streakCount = -1),
                Case("없는 장착 모듈 id", d => d.equippedModuleIds = new[] { "missing" }),
                Case("장착 모듈 id null", d => d.equippedModuleIds = new string[] { null }),
                Case("뽑기 중인데 제안 카드 (조합은 맞음)", d => d.offeredModuleIds = new[] { modules[0].Id, modules[2].Id, modules[4].Id }),
                Case("없는 제안 모듈 id", d => { d.state = RunState.RoundCleared; d.production = quota; d.offeredModuleIds = new[] { "missing" }; }),
            };

            foreach (KeyValuePair<string, Action<RunSaveData>> broken in cases)
            {
                RunSaveData data = Copy(valid);
                broken.Value(data);
                AssertRejected(data, broken.Key);
            }

            AssertRejected(null, "null");
        }

        // 제안 카드는 ModuleOffer 가 만들 수 있는 조합만 받는다: 카드 수 = min(3, 모듈이 있는 축 수), 축은 서로 다르다
        [Test]
        public void Restore_RejectsImpossibleOfferCards()
        {
            RunSession session = RunSession.Create(9, parts, modules);
            IReadOnlyList<ModuleData> offer = AdvanceToFirstOpenOffer(session);
            RunSaveData valid = session.CaptureSaveData();
            Assert.AreEqual(3, valid.offeredModuleIds.Length);

            // 고치지 않은 카드는 복원되어야 한다
            RunSession.Restore(Copy(valid), parts, modules);

            ModuleData unusedAxisModule = null;
            ModuleData sameAxisSibling = null;
            int siblingSlot = -1;
            foreach (ModuleData module in modules)
            {
                bool axisUsed = false;
                for (int i = 0; i < offer.Count; i++)
                {
                    if (offer[i].Axis == module.Axis)
                    {
                        axisUsed = true;
                        if (offer[i] != module && sameAxisSibling == null)
                        {
                            sameAxisSibling = module;
                            siblingSlot = i;
                        }
                    }
                }

                if (!axisUsed && unusedAxisModule == null)
                {
                    unusedAxisModule = module;
                }
            }

            Assert.IsTrue(unusedAxisModule != null && sameAxisSibling != null, "검사용 모듈을 찾지 못함");

            string[] ids = valid.offeredModuleIds;
            string otherSlotId = ids[siblingSlot == 0 ? 1 : 0];
            var cases = new List<KeyValuePair<string, string[]>>
            {
                new KeyValuePair<string, string[]>("카드 2장", new[] { ids[0], ids[1] }),
                new KeyValuePair<string, string[]>("카드 1장", new[] { ids[0] }),
                new KeyValuePair<string, string[]>("카드 4장 (남은 축 추가)", new[] { ids[0], ids[1], ids[2], unusedAxisModule.Id }),
                new KeyValuePair<string, string[]>("같은 카드 3장", new[] { ids[0], ids[0], ids[0] }),
                new KeyValuePair<string, string[]>("같은 카드 2장", new[] { ids[0], ids[0], ids[2] }),
                new KeyValuePair<string, string[]>("다른 카드지만 같은 축", new[] { otherSlotId, ids[siblingSlot], sameAxisSibling.Id }),
            };

            foreach (KeyValuePair<string, string[]> broken in cases)
            {
                RunSaveData data = Copy(valid);
                data.offeredModuleIds = broken.Value;
                AssertRejected(data, broken.Key);
            }
        }

        // 모듈이 있는 축이 2개뿐이면 제안도 2장이다. 이 경우를 3장 기준으로 거부하면 정상 세이브를 못 읽는다
        [Test]
        public void Restore_AcceptsTwoCardOfferFromTwoAxisPool()
        {
            var smallPool = new[] { modules[0], modules[1], modules[5] };
            RunSession session = RunSession.Create(9, parts, smallPool);
            IReadOnlyList<ModuleData> offer = AdvanceToFirstOpenOffer(session);
            Assert.AreEqual(2, offer.Count);

            RunSession restored = RunSession.Restore(session.CaptureSaveData(), parts, smallPool);
            IReadOnlyList<ModuleData> restoredOffer = restored.OpenOffer();

            Assert.AreEqual(2, restoredOffer.Count);
            Assert.AreSame(offer[0], restoredOffer[0]);
            Assert.AreSame(offer[1], restoredOffer[1]);

            RunSaveData threeCards = session.CaptureSaveData();
            threeCards.offeredModuleIds = new[] { offer[0].Id, offer[1].Id, offer[1].Id };
            AssertRejected(threeCards, "축 2개인데 카드 3장", smallPool);
        }

        [Test]
        public void Restore_RejectsDuplicateModuleIdsInPool()
        {
            RunSaveData data = RunSession.Create(7, parts, modules).CaptureSaveData();
            var pool = new List<ModuleData>(modules);
            pool.Add(CreateModule(modules[0].Id, ModuleAxis.Draws, ModifierType.ExtraDraws, 1f));

            Assert.Throws<ArgumentException>(() => RunSession.Restore(data, parts, pool));
        }

        [Test]
        public void Capture_RejectsEquippedModuleWithoutId()
        {
            RunSession session = RunSession.Create(7, parts, modules);
            ModuleData withoutId = CreateModule("임시", ModuleAxis.Draws, ModifierType.ExtraDraws, 1f);
            withoutId.SetIdForTests("");
            session.RunManager.EquipModule(withoutId);

            Assert.Throws<InvalidOperationException>(() => session.CaptureSaveData());
        }

        private static KeyValuePair<string, Action<RunSaveData>> Case(string name, Action<RunSaveData> change)
        {
            return new KeyValuePair<string, Action<RunSaveData>>(name, change);
        }

        private void AssertRejected(RunSaveData data, string name)
        {
            AssertRejected(data, name, modules);
        }

        private void AssertRejected(RunSaveData data, string name, IReadOnlyList<ModuleData> pool)
        {
            try
            {
                RunSession.Restore(data, parts, pool);
            }
            catch (ArgumentException)
            {
                return;
            }

            Assert.Fail("거부해야 함: " + name);
        }

        private static RunSaveData Copy(RunSaveData source)
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
                equippedModuleIds = (string[])source.equippedModuleIds.Clone(),
                offeredModuleIds = (string[])source.offeredModuleIds.Clone(),
            };
        }

        private static IReadOnlyList<ModuleData> AdvanceToFirstOpenOffer(RunSession session)
        {
            while (session.RunManager.State == RunState.Drawing)
            {
                session.RunManager.Draw();
            }

            return session.OpenOffer();
        }

        private static List<string> RunToEnd(RunSession session)
        {
            var trace = new List<string>();
            for (int i = 0; i < StepLimit; i++)
            {
                string step = Step(session);
                if (step == null)
                {
                    return trace;
                }

                trace.Add(step);
            }

            Assert.Fail("런이 끝나지 않음");
            return trace;
        }

        // 선택은 게임 난수를 쓰지 않는 고정 규칙. 원래 판과 복원한 판이 같은 상황이면 같은 카드를 고른다
        private static string Step(RunSession session)
        {
            RunManager runManager = session.RunManager;
            if (runManager.State == RunState.Drawing)
            {
                DrawResult result = runManager.Draw();
                return "뽑기 " + result.Part.DisplayName + " +" + result.Output + " 연속 " + result.StreakTriggered + " | " + Describe(session);
            }

            if (runManager.State != RunState.RoundCleared)
            {
                return null;
            }

            if (session.CurrentOffer.Count == 0)
            {
                IReadOnlyList<ModuleData> offer = session.OpenOffer();
                return "제안 " + JoinIds(offer) + " | " + Describe(session);
            }

            IReadOnlyList<ModuleData> cards = session.CurrentOffer;
            ModuleData choice = cards[(runManager.CurrentRound + runManager.EquippedModules.Count) % cards.Count];
            session.SelectModule(choice);
            return "선택 " + choice.Id + " | " + Describe(session);
        }

        private static string Describe(RunSession session)
        {
            RunManager runManager = session.RunManager;
            GachaEngine gachaEngine = session.GachaEngine;
            return "R" + runManager.CurrentRound + " 생산 " + runManager.Production + " 남은 " + runManager.DrawsRemaining + " " + runManager.State
                + " 보증 " + gachaEngine.DrawsWithoutSuperRare
                + " 확률 " + gachaEngine.GetProbability(Grade.N).ToString("R") + "/" + gachaEngine.GetProbability(Grade.R).ToString("R") + "/" + gachaEngine.GetProbability(Grade.SR).ToString("R")
                + " 장착 " + JoinIds(runManager.EquippedModules);
        }

        private static string Describe(RunSaveData data)
        {
            return "v" + data.version + " " + data.mode + " 시드 " + data.seed + " 난수 " + data.randomState + " 보증 " + data.drawsWithoutSuperRare
                + " R" + data.round + " 생산 " + data.production + " 남은 " + data.drawsRemaining + " " + data.state
                + " 연속 " + data.streakGrade + "x" + data.streakCount
                + " 장착 " + string.Join(",", data.equippedModuleIds) + " 제안 " + string.Join(",", data.offeredModuleIds);
        }

        private static string JoinIds(IReadOnlyList<ModuleData> modules)
        {
            var ids = new string[modules.Count];
            for (int i = 0; i < modules.Count; i++)
            {
                ids[i] = modules[i].Id;
            }

            return string.Join(",", ids);
        }

        private static bool Contains(IReadOnlyList<ModuleData> modules, ModuleData target)
        {
            foreach (ModuleData module in modules)
            {
                if (module == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertSameTrace(List<string> expected, int start, List<string> actual, string label)
        {
            Assert.AreEqual(expected.Count - start, actual.Count, label + " 남은 단계 수");
            for (int i = 0; i < actual.Count; i++)
            {
                Assert.AreEqual(expected[start + i], actual[i], label + " 이후 " + i + "번째 단계");
            }
        }
    }
}
