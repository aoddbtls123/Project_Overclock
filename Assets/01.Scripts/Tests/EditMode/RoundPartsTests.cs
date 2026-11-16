using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    // 이번 라운드에 뽑은 부품 목록 (수확물 더미). 뽑을 때마다 순서대로 쌓이고 · 새 라운드에 비고 · 세이브에 id 로 들어가 그대로 돌아옴.
    // 목록을 기록한 세이브는 게임이 만들 수 있는 것만 받음 (뽑기 상한 · 연속 · 천장). 기록 안 한 옛 세이브 → 빈 더미 · 그 라운드는 저장 안 함
    public class RoundPartsTests
    {
        private const int PityThreshold = 50;

        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();
        private readonly List<string> createdFolders = new List<string>();

        private PartData[] parts;
        private ModuleData[] modules;
        private PartData normal;
        private PartData rare;
        private PartData superRare;

        [SetUp]
        public void CreateData()
        {
            normal = CreatePart("볼트", Grade.N, 1, PartTag.Mechanical);
            rare = CreatePart("코일", Grade.R, 3, PartTag.Power);
            superRare = CreatePart("코어", Grade.SR, 10, PartTag.Power);
            parts = new[]
            {
                normal,
                CreatePart("전선", Grade.N, 1, PartTag.Electronic),
                rare,
                CreatePart("기어", Grade.R, 3, PartTag.Mechanical),
                superRare,
            };

            modules = new[]
            {
                CreateModule("sr_up", ModuleAxis.Probability, ModifierType.SuperRareChance, 0.05f),
                CreateModule("coil", ModuleAxis.Multiplier, ModifierType.OutputMultiplier, 0.3f),
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
            foreach (string folder in createdFolders)
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }

            createdFolders.Clear();
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

        [Test]
        public void Draw_AddsEachPartInOrderAndNewRoundClears()
        {
            int clearedRounds = 0;
            int checkedDraws = 0;
            for (uint seed = 1; seed <= 30; seed++)
            {
                RunSession session = RunSession.Create(seed, parts, modules);
                RunManager runManager = session.RunManager;
                var expected = new List<PartData>();
                while (runManager.State == RunState.Drawing || runManager.State == RunState.RoundCleared)
                {
                    if (runManager.State == RunState.RoundCleared)
                    {
                        CollectionAssert.AreEqual(expected, runManager.RoundParts, "seed=" + seed + " 돌파 직후에도 그 라운드 부품이 남는다");
                        IReadOnlyList<ModuleData> offer = session.OpenOffer();
                        session.SelectModule(offer[0]);
                        Assert.AreEqual(0, runManager.RoundParts.Count, "seed=" + seed + " 새 라운드는 빈 목록");
                        expected.Clear();
                        clearedRounds++;
                        continue;
                    }

                    DrawResult result = runManager.Draw();
                    expected.Add(result.Part);
                    CollectionAssert.AreEqual(expected, runManager.RoundParts, "seed=" + seed + " 뽑은 순서");
                    checkedDraws++;
                }

                // 런이 끝나도 마지막 라운드 부품은 남음 (실패 화면에서 더미를 그대로 보여 줌)
                CollectionAssert.AreEqual(expected, runManager.RoundParts, "seed=" + seed + " 런 끝");
            }

            Assert.GreaterOrEqual(clearedRounds, 60, "돌파 → 새 라운드");
            Assert.GreaterOrEqual(checkedDraws, 500, "뽑기 수");
        }

        // 모든 단계 직후 저장 → 복원하면 같은 부품 목록이 돌아옴. 다시 저장한 id 도 같음
        [Test]
        public void RestoreAfterAnyStep_KeepsRoundParts()
        {
            int withParts = 0;
            int withPartsAtBreakthrough = 0;
            int withSuperRare = 0;
            for (uint seed = 1; seed <= 20; seed++)
            {
                RunSession session = RunSession.Create(seed, parts, modules);
                for (int step = 0; step < 500; step++)
                {
                    RunSaveData data = session.CaptureSaveData();
                    RunSession restored = RunSession.Restore(data, parts, modules);
                    string label = "seed=" + seed + " step=" + step;
                    CollectionAssert.AreEqual(session.RunManager.RoundParts, restored.RunManager.RoundParts, label);
                    CollectionAssert.AreEqual(data.roundPartIds, restored.CaptureSaveData().roundPartIds, label + " 다시 저장한 id");

                    if (data.roundPartIds.Length > 0)
                    {
                        withParts++;
                        if (data.state == RunState.RoundCleared)
                        {
                            withPartsAtBreakthrough++;
                        }

                        if (Contains(session.RunManager.RoundParts, Grade.SR))
                        {
                            withSuperRare++;
                        }
                    }

                    if (!Step(session))
                    {
                        break;
                    }
                }
            }

            Assert.GreaterOrEqual(withParts, 500, "부품이 있는 저장");
            Assert.GreaterOrEqual(withPartsAtBreakthrough, 50, "돌파 직후 저장");
            Assert.GreaterOrEqual(withSuperRare, 30, "SR 이 있는 라운드 저장 (천장 검사 분기)");
        }

        [Test]
        public void SaveFile_RoundTripsRoundParts()
        {
            string folder = Path.Combine(Path.GetTempPath(), "overclock_roundparts_" + Guid.NewGuid().ToString("N"));
            createdFolders.Add(folder);
            var file = new RunSaveFile(folder);

            RunSession session = RunSession.Create(5, parts, modules);
            session.RunManager.Draw();
            session.RunManager.Draw();
            session.RunManager.Draw();
            file.Save(session);

            Assert.IsTrue(file.TryLoad(parts, modules, out RunSession loaded, out string error), error);
            Assert.AreEqual(3, loaded.RunManager.RoundParts.Count);
            CollectionAssert.AreEqual(session.RunManager.RoundParts, loaded.RunManager.RoundParts);
        }

        // 부품 목록이 없는 옛 세이브 (기록 표시 false · 빈 목록 · null) 는 빈 더미로 복원하고, 판은 그대로 이어짐
        [Test]
        public void OldSaveWithoutRoundParts_RestoresWithEmptyList()
        {
            RunSession session = RunSession.Create(5, parts, modules);
            session.RunManager.Draw();
            session.RunManager.Draw();
            RunSaveData data = session.CaptureSaveData();
            Assert.IsTrue(data.roundPartsRecorded, "(전제) 새 세이브는 기록 표시가 있다");
            Assert.AreEqual(2, data.roundPartIds.Length, "(전제) 저장에 부품이 있다");

            RunSaveData old = AsOldSave(data);
            RunSession fromEmpty = RunSession.Restore(old, parts, modules);
            Assert.AreEqual(0, fromEmpty.RunManager.RoundParts.Count);
            Assert.IsFalse(fromEmpty.RunManager.RoundPartsComplete, "앞 부품을 모르는 라운드");

            old.roundPartIds = null;
            RunSession fromNull = RunSession.Restore(old, parts, modules);
            Assert.AreEqual(0, fromNull.RunManager.RoundParts.Count);

            DrawResult expected = session.RunManager.Draw();
            DrawResult actual = fromNull.RunManager.Draw();
            Assert.AreSame(expected.Part, actual.Part, "다음 뽑기는 같다");
            Assert.AreEqual(expected.Output, actual.Output);
            Assert.AreEqual(1, fromNull.RunManager.RoundParts.Count, "복원 뒤 뽑은 것부터 쌓인다");
        }

        // 옛 세이브로 이어 한 라운드에서 다시 저장 → 다시 복원해도 거부되지 않음 (앞 부품을 모르는 채 뒤 부품만 저장하면
        // 연속 · 천장 검사가 맞는 세이브를 거부함 — 교차검토 #32). 다음 라운드부터는 다시 목록을 저장함
        [Test]
        public void OldSave_ResaveAndRestoreKeepsWorkingUntilNextRound()
        {
            int checkedSaves = 0;
            int recordedAgain = 0;
            for (uint seed = 1; seed <= 20; seed++)
            {
                RunSession original = RunSession.Create(seed, parts, modules);
                original.RunManager.Draw();
                if (original.RunManager.State != RunState.Drawing)
                {
                    continue;
                }

                RunSession session = RunSession.Restore(AsOldSave(original.CaptureSaveData()), parts, modules);
                int startRound = session.RunManager.CurrentRound;
                for (int step = 0; step < 500; step++)
                {
                    RunSaveData data = session.CaptureSaveData();
                    string label = "seed=" + seed + " step=" + step;
                    RunSession restored = RunSession.Restore(data, parts, modules);
                    checkedSaves++;
                    if (session.RunManager.CurrentRound == startRound)
                    {
                        Assert.IsFalse(data.roundPartsRecorded, label + " 옛 세이브 라운드는 목록을 저장하지 않는다");
                        Assert.AreEqual(0, data.roundPartIds.Length, label);
                        Assert.AreEqual(0, restored.RunManager.RoundParts.Count, label);
                    }
                    else
                    {
                        Assert.IsTrue(data.roundPartsRecorded, label + " 다음 라운드부터 다시 저장");
                        CollectionAssert.AreEqual(session.RunManager.RoundParts, restored.RunManager.RoundParts, label);
                        if (data.roundPartIds.Length > 0)
                        {
                            recordedAgain++;
                        }
                    }

                    if (!Step(session))
                    {
                        break;
                    }
                }
            }

            Assert.GreaterOrEqual(checkedSaves, 200, "저장 횟수");
            Assert.GreaterOrEqual(recordedAgain, 50, "다음 라운드에서 부품이 있는 저장");
        }

        [Test]
        public void Restore_RejectsPartsInUnrecordedSaveAndMissingPartsInRecordedSave()
        {
            RunSession session = RunSession.Create(5, parts, modules);
            session.RunManager.Draw();
            session.RunManager.Draw();
            RunSaveData data = session.CaptureSaveData();
            Assert.IsTrue(data.production > 0, "(전제) 생산량이 있다");

            RunSaveData unrecordedWithParts = Copy(data);
            unrecordedWithParts.roundPartsRecorded = false;
            Assert.Throws<ArgumentException>(() => RunSession.Restore(unrecordedWithParts, parts, modules), "기록 안 함인데 부품이 있음");

            RunSaveData recordedEmpty = Copy(data);
            recordedEmpty.roundPartIds = new string[0];
            Assert.Throws<ArgumentException>(() => RunSession.Restore(recordedEmpty, parts, modules), "기록했다면서 뽑은 부품이 없음 (생산량 있음)");

            Assert.AreEqual(2, RunSession.Restore(Copy(data), parts, modules).RunManager.RoundParts.Count, "(대조) 그대로면 받는다");
        }

        [Test]
        public void RestoreRoundParts_EmptyListMeansRoundStart()
        {
            AssertRestore(CreateRestoredRun(10, Grade.N, 0, 5), new List<PartData>(), true, "(대조) 라운드 시작");
            AssertRestore(CreateRestoredRun(10, Grade.N, 1, 5), new List<PartData>(), false, "부품 없는데 연속 1");

            var random = new RandomService(1);
            var engine = new GachaEngine(random, PityThreshold, 0.72, 0.25, 0.03);
            var withProduction = new RunManager(engine, new PartCatalog(parts, random), 8, 10);
            withProduction.RestoreProgress(1, 3, 7, RunState.Drawing, Grade.N, 0);
            AssertRestore(withProduction, new List<PartData>(), false, "부품 없는데 생산량 3");
        }

        [Test]
        public void Restore_RejectsUnknownOrMissingPartId()
        {
            RunSession session = RunSession.Create(5, parts, modules);
            session.RunManager.Draw();
            RunSaveData data = session.CaptureSaveData();
            string kept = data.roundPartIds[0];

            // 맞는 id 하나 뒤에 붙임 — 모르는 id 를 건너뛰는 틀린 구현이면 남은 목록이 맞아서 통과해 버림
            data.roundPartIds = new[] { kept, "없는부품" };
            Assert.Throws<ArgumentException>(() => RunSession.Restore(data, parts, modules), "없는 id");

            data.roundPartIds = new[] { kept, null };
            Assert.Throws<ArgumentException>(() => RunSession.Restore(data, parts, modules), "null id");

            data.roundPartIds = new[] { kept };
            Assert.AreEqual(1, RunSession.Restore(data, parts, modules).RunManager.RoundParts.Count, "(대조) 원래 id 는 받는다");
        }

        [Test]
        public void Capture_RejectsDrawnPartWithoutId()
        {
            PartData withoutId = ScriptableObject.CreateInstance<PartData>();
            withoutId.InitializeForTests("", Grade.N, 1, PartTag.Mechanical);
            createdObjects.Add(withoutId);
            RunSession session = RunSession.Create(5, new[] { withoutId, rare, superRare }, modules);
            while (session.RunManager.RoundParts.Count == 0 || session.RunManager.RoundParts[session.RunManager.RoundParts.Count - 1] != withoutId)
            {
                session.RunManager.Draw();
            }

            Assert.Throws<InvalidOperationException>(() => session.CaptureSaveData());
        }

        // 경계: 이번 라운드 부품 수 + 남은 뽑기 ≤ 20. 20 은 받고 21 은 거부 (부품 패턴은 연속 · 천장이 맞게 고정)
        [TestCase(10, 10, true)]
        [TestCase(11, 9, true)]
        [TestCase(11, 10, false)]
        [TestCase(1, 20, false)]
        public void RestoreRoundParts_LimitsTotalDrawsInRound(int partCount, int drawsRemaining, bool accepted)
        {
            var drawn = new List<PartData>();
            for (int i = 0; i < partCount; i++)
            {
                drawn.Add(i % 2 == 0 ? rare : normal);
            }

            Grade lastGrade = drawn[drawn.Count - 1].Grade;
            RunManager runManager = CreateRestoredRun(drawsRemaining, lastGrade, 1, partCount);
            AssertRestore(runManager, drawn, accepted, "부품 " + partCount + " 남은 " + drawsRemaining);
        }

        [Test]
        public void RestoreRoundParts_RejectsStreakThatDoesNotMatchParts()
        {
            var twoNormals = new List<PartData> { normal, normal };
            AssertRestore(CreateRestoredRun(8, Grade.N, 2, 2), twoNormals, true, "(대조) N x2");
            AssertRestore(CreateRestoredRun(8, Grade.N, 1, 2), twoNormals, false, "연속 개수가 다름");
            AssertRestore(CreateRestoredRun(8, Grade.R, 2, 2), twoNormals, false, "연속 등급이 다름");

            // 3연속이 발동하면 0 으로 돌아감 (발동 뒤 다시 셈)
            var threeNormals = new List<PartData> { normal, normal, normal };
            AssertRestore(CreateRestoredRun(7, Grade.N, 0, 3), threeNormals, true, "(대조) 3연속 발동 → 0");
            AssertRestore(CreateRestoredRun(7, Grade.N, 2, 3), threeNormals, false, "발동했는데 연속이 남음");
            var fourNormals = new List<PartData> { normal, normal, normal, normal };
            AssertRestore(CreateRestoredRun(6, Grade.N, 1, 4), fourNormals, true, "(대조) 발동 뒤 N x1");
        }

        [Test]
        public void RestoreRoundParts_RejectsPityThatDoesNotMatchParts()
        {
            // SR 이 있으면 카운터 = 마지막 SR 뒤 뽑기 수
            var superRareThenNormal = new List<PartData> { superRare, normal };
            AssertRestore(CreateRestoredRun(8, Grade.N, 1, 1), superRareThenNormal, true, "(대조) SR 뒤 1");
            AssertRestore(CreateRestoredRun(8, Grade.N, 1, 2), superRareThenNormal, false, "SR 뒤 1 인데 카운터 2");
            AssertRestore(CreateRestoredRun(8, Grade.N, 1, 0), superRareThenNormal, false, "SR 뒤 1 인데 카운터 0");

            // SR 이 없으면 카운터는 적어도 부품 수 (앞 라운드에서 이어진 수가 더해질 수 있음)
            var normalThenRare = new List<PartData> { normal, rare };
            AssertRestore(CreateRestoredRun(8, Grade.R, 1, 2), normalThenRare, true, "(대조) 카운터 = 부품 수");
            AssertRestore(CreateRestoredRun(8, Grade.R, 1, 30), normalThenRare, true, "(대조) 앞 라운드에서 이어짐");
            AssertRestore(CreateRestoredRun(8, Grade.R, 1, 1), normalThenRare, false, "카운터가 부품 수보다 작음");
        }

        // R1 · 뽑기 중 · 생산량 0 으로 진행 상태를 맞춘 뒤 연속 · 천장만 바꿔 봄. 부품 목록 검사 하나만 결과를 가름
        private RunManager CreateRestoredRun(int drawsRemaining, Grade streakGrade, int streakCount, int pity)
        {
            var random = new RandomService(1);
            var engine = new GachaEngine(random, PityThreshold, 0.72, 0.25, 0.03);
            var runManager = new RunManager(engine, new PartCatalog(parts, random), 8, 10);
            runManager.RestoreProgress(1, 0, drawsRemaining, RunState.Drawing, streakGrade, streakCount);
            engine.RestorePityCounter(pity);
            return runManager;
        }

        private static RunSaveData AsOldSave(RunSaveData source)
        {
            RunSaveData old = Copy(source);
            old.roundPartsRecorded = false;
            old.roundPartIds = new string[0];
            return old;
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
                overclockUsed = source.overclockUsed,
                overclockDrawsLeft = source.overclockDrawsLeft,
                roundPartsRecorded = source.roundPartsRecorded,
                roundPartIds = (string[])source.roundPartIds.Clone(),
                equippedModuleIds = (string[])source.equippedModuleIds.Clone(),
                offeredModuleIds = (string[])source.offeredModuleIds.Clone(),
            };
        }

        private static void AssertRestore(RunManager runManager, List<PartData> drawn, bool accepted, string label)
        {
            if (accepted)
            {
                runManager.RestoreRoundParts(drawn);
                CollectionAssert.AreEqual(drawn, runManager.RoundParts, label);
                return;
            }

            Assert.Throws<ArgumentException>(() => runManager.RestoreRoundParts(drawn), label);
            Assert.AreEqual(0, runManager.RoundParts.Count, label + " 거부하면 목록이 비어 있다");
        }

        // 선택은 게임 난수를 쓰지 않는 고정 규칙. 끝나면 false
        private static bool Step(RunSession session)
        {
            RunManager runManager = session.RunManager;
            if (runManager.State == RunState.Drawing)
            {
                runManager.Draw();
                return true;
            }

            if (runManager.State != RunState.RoundCleared)
            {
                return false;
            }

            if (session.CurrentOffer.Count == 0)
            {
                session.OpenOffer();
                return true;
            }

            IReadOnlyList<ModuleData> cards = session.CurrentOffer;
            session.SelectModule(cards[runManager.CurrentRound % cards.Count]);
            return true;
        }

        private static bool Contains(IReadOnlyList<PartData> list, Grade grade)
        {
            foreach (PartData part in list)
            {
                if (part.Grade == grade)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
