using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    // 기록 규칙 + 실제 JsonUtility 와 파일 입출력. 파일은 임시 폴더에서만 읽고 쓴다
    public class BestRecordsTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

        private string directory;
        private PartData[] parts;
        private ModuleData[] modules;

        [SetUp]
        public void CreateData()
        {
            directory = Path.Combine(Path.GetTempPath(), "OverclockRecordTests_" + Guid.NewGuid().ToString("N"));
            parts = new[]
            {
                CreatePart("볼트", Grade.N, 1),
                CreatePart("코일", Grade.R, 3),
                CreatePart("코어", Grade.SR, 10),
            };
            modules = new[]
            {
                CreateModule("sr_up", ModuleAxis.Probability, ModifierType.SuperRareChance, 0.2f),
                CreateModule("coil", ModuleAxis.Multiplier, ModifierType.OutputMultiplier, 1f),
                CreateModule("sync", ModuleAxis.Conditional, ModifierType.StreakBonus, 10f),
                CreateModule("over", ModuleAxis.Draws, ModifierType.ExtraDraws, 5f),
            };
        }

        [TearDown]
        public void DeleteData()
        {
            foreach (UnityEngine.Object created in createdObjects)
            {
                UnityEngine.Object.DestroyImmediate(created);
            }

            createdObjects.Clear();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        private PartData CreatePart(string displayName, Grade grade, int baseOutput)
        {
            var part = ScriptableObject.CreateInstance<PartData>();
            part.InitializeForTests(displayName, grade, baseOutput, PartTag.Mechanical);
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

        private RunSession PlayToEnd(uint seed, RunMode mode)
        {
            RunSession session = RunSession.Create(seed, parts, modules, mode);
            for (int step = 0; step < 2000; step++)
            {
                RunState state = session.RunManager.State;
                if (state == RunState.RunFailed || state == RunState.RunCleared)
                {
                    return session;
                }

                if (state == RunState.RoundCleared)
                {
                    session.SelectModule(session.OpenOffer()[0]);
                }
                else
                {
                    session.RunManager.Draw();
                }
            }

            Assert.Fail("런이 끝나지 않았습니다. seed=" + seed);
            return null;
        }

        [Test]
        public void NewRecords_StartEmpty()
        {
            var records = new BestRecords();

            Assert.AreEqual(0, records.BestRound(RunMode.Normal));
            Assert.AreEqual(0, records.BestRound(RunMode.Endless));
            Assert.IsFalse(records.IsCleared(RunMode.Normal));
            Assert.IsFalse(records.IsCleared(RunMode.Endless));
        }

        // 일반모드 순서: 낮은 라운드 < R8 실패 < R8 클리어. 같은 기록은 신기록이 아니다
        [Test]
        public void NormalSubmit_KeepsBestAndReportsOnlyImprovements()
        {
            var records = new BestRecords();

            Assert.IsTrue(records.Submit(RunMode.Normal, 5, false), "첫 기록");
            Assert.IsFalse(records.Submit(RunMode.Normal, 5, false), "같은 라운드");
            Assert.IsFalse(records.Submit(RunMode.Normal, 3, false), "낮은 라운드");
            Assert.AreEqual(5, records.BestRound(RunMode.Normal));

            Assert.IsTrue(records.Submit(RunMode.Normal, 8, false), "R8 실패");
            Assert.IsFalse(records.IsCleared(RunMode.Normal));
            Assert.IsTrue(records.Submit(RunMode.Normal, 8, true), "R8 클리어는 R8 실패보다 위");
            Assert.IsTrue(records.IsCleared(RunMode.Normal));
            Assert.IsFalse(records.Submit(RunMode.Normal, 8, false), "클리어 뒤 R8 실패");
            Assert.IsFalse(records.Submit(RunMode.Normal, 8, true), "클리어 뒤 다시 클리어");

            Assert.AreEqual(8, records.BestRound(RunMode.Normal));
            Assert.IsTrue(records.IsCleared(RunMode.Normal));
        }

        [Test]
        public void Modes_KeepSeparateRecords()
        {
            var records = new BestRecords();

            Assert.IsTrue(records.Submit(RunMode.Endless, 11, false));
            Assert.AreEqual(0, records.BestRound(RunMode.Normal), "무한 기록이 일반에 섞임");
            Assert.IsTrue(records.Submit(RunMode.Normal, 6, false), "일반 첫 기록은 무한 기록과 비교하지 않음");
            Assert.IsFalse(records.Submit(RunMode.Endless, 9, false));
            Assert.IsTrue(records.Submit(RunMode.Endless, 12, false));
            Assert.IsFalse(records.Submit(RunMode.Endless, 12, false), "무한 같은 라운드");

            Assert.AreEqual(6, records.BestRound(RunMode.Normal));
            Assert.AreEqual(12, records.BestRound(RunMode.Endless));
        }

        [TestCase(RunMode.Normal, 0, false)]
        [TestCase(RunMode.Normal, 9, false)]
        [TestCase(RunMode.Normal, 7, true)]
        [TestCase(RunMode.Endless, 0, false)]
        [TestCase(RunMode.Endless, 8, true)]
        [TestCase((RunMode)5, 3, false)]
        public void Submit_RejectsResultsTheGameCannotMake(RunMode mode, int round, bool cleared)
        {
            var records = new BestRecords();

            Assert.Catch<ArgumentException>(() => records.Submit(mode, round, cleared));
            Assert.AreEqual(0, records.BestRound(RunMode.Normal));
            Assert.AreEqual(0, records.BestRound(RunMode.Endless));
        }

        // 실제로 끝난 런의 라운드 · 클리어 여부가 그대로 들어간다
        [TestCase(RunMode.Normal)]
        [TestCase(RunMode.Endless)]
        public void SubmitSession_UsesFinalRoundOfFinishedRun(RunMode mode)
        {
            int cleared = 0;
            for (uint seed = 1; seed <= 30; seed++)
            {
                RunSession session = PlayToEnd(seed, mode);
                var records = new BestRecords();

                Assert.IsTrue(records.Submit(session), "seed=" + seed);
                Assert.AreEqual(session.RunManager.CurrentRound, records.BestRound(mode), "seed=" + seed);
                Assert.AreEqual(session.RunManager.State == RunState.RunCleared, records.IsCleared(mode), "seed=" + seed);
                if (records.IsCleared(mode))
                {
                    cleared++;
                }
            }

            if (mode == RunMode.Normal)
            {
                Assert.GreaterOrEqual(cleared, 1, "클리어한 런이 하나 이상 있어야 클리어 기록을 검사한다");
            }
        }

        [Test]
        public void SubmitSession_RejectsUnfinishedRun()
        {
            RunSession session = RunSession.Create(1, parts, modules);
            var records = new BestRecords();

            Assert.Throws<InvalidOperationException>(() => records.Submit(session), "뽑기 중");
            while (session.RunManager.State == RunState.Drawing)
            {
                session.RunManager.Draw();
            }

            Assert.AreEqual(RunState.RoundCleared, session.RunManager.State, "(전제) R1 은 항상 돌파");
            Assert.Throws<InvalidOperationException>(() => records.Submit(session), "돌파 상태");
            Assert.AreEqual(0, records.BestRound(RunMode.Normal));
        }

        [Test]
        public void Data_RoundTrips()
        {
            var records = new BestRecords();
            records.Submit(RunMode.Normal, 8, true);
            records.Submit(RunMode.Endless, 13, false);

            BestRecords copy = BestRecords.FromData(records.ToData());

            Assert.AreEqual(8, copy.BestRound(RunMode.Normal));
            Assert.IsTrue(copy.IsCleared(RunMode.Normal));
            Assert.AreEqual(13, copy.BestRound(RunMode.Endless));
            Assert.AreEqual(BestRecordData.CurrentVersion, records.ToData().version);
        }

        [TestCase(2, 5, false, 0, TestName = "버전 다름")]
        [TestCase(1, -1, false, 0, TestName = "일반 음수")]
        [TestCase(1, 9, false, 0, TestName = "일반 R9")]
        [TestCase(1, 7, true, 0, TestName = "R7 클리어")]
        [TestCase(1, 0, true, 0, TestName = "기록 없이 클리어")]
        [TestCase(1, 5, false, -2, TestName = "무한 음수")]
        public void FromData_RejectsValuesTheGameCannotMake(int version, int normalRound, bool normalCleared, int endlessRound)
        {
            var data = new BestRecordData
            {
                version = version,
                normalBestRound = normalRound,
                normalCleared = normalCleared,
                endlessBestRound = endlessRound,
            };

            Assert.Catch<ArgumentException>(() => BestRecords.FromData(data));
        }

        [Test]
        public void FromData_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => BestRecords.FromData(null));
        }

        [Test]
        public void File_SaveThenLoad_KeepsRecords()
        {
            var file = new BestRecordFile(directory);
            var records = new BestRecords();
            records.Submit(RunMode.Normal, 8, true);
            records.Submit(RunMode.Endless, 12, false);

            file.Save(records);
            Assert.IsTrue(file.TryLoad(out BestRecords loaded, out string error), error);

            Assert.AreEqual(8, loaded.BestRound(RunMode.Normal));
            Assert.IsTrue(loaded.IsCleared(RunMode.Normal));
            Assert.AreEqual(12, loaded.BestRound(RunMode.Endless));
            Assert.IsFalse(File.Exists(file.FilePath + ".tmp"), "임시 파일이 남음");
        }

        [Test]
        public void File_Overwrite_KeepsLatest()
        {
            var file = new BestRecordFile(directory);
            var records = new BestRecords();
            records.Submit(RunMode.Normal, 4, false);
            file.Save(records);
            records.Submit(RunMode.Normal, 6, false);
            file.Save(records);

            Assert.IsTrue(file.TryLoad(out BestRecords loaded, out string error), error);
            Assert.AreEqual(6, loaded.BestRound(RunMode.Normal));
            Assert.IsFalse(File.Exists(file.FilePath + ".tmp"), "임시 파일이 남음");
        }

        [Test]
        public void File_Missing_LoadsEmptyRecords()
        {
            var file = new BestRecordFile(directory);

            Assert.IsTrue(file.TryLoad(out BestRecords loaded, out string error));
            Assert.IsNull(error);
            Assert.AreEqual(0, loaded.BestRound(RunMode.Normal));
            Assert.AreEqual(0, loaded.BestRound(RunMode.Endless));
        }

        [TestCase("", TestName = "빈 파일")]
        [TestCase("{ 깨진", TestName = "형식 오류")]
        [TestCase("{\"version\":1,\"normalBestRound\":9,\"normalCleared\":false,\"endlessBestRound\":0}", TestName = "일반 R9")]
        [TestCase("{\"version\":3,\"normalBestRound\":2,\"normalCleared\":false,\"endlessBestRound\":0}", TestName = "버전 다름")]
        public void File_Unreadable_ReturnsFalseWithEmptyRecords(string content)
        {
            Directory.CreateDirectory(directory);
            var file = new BestRecordFile(directory);
            File.WriteAllText(file.FilePath, content);

            Assert.IsFalse(file.TryLoad(out BestRecords loaded, out string error));
            Assert.IsNotNull(error);
            Assert.AreEqual(0, loaded.BestRound(RunMode.Normal));
            Assert.IsTrue(File.Exists(file.FilePath), "읽을 수 없는 파일을 지우면 안 된다");
        }

        [Test]
        public void File_RejectsEmptyDirectory()
        {
            Assert.Throws<ArgumentException>(() => new BestRecordFile(""));
            Assert.Throws<ArgumentException>(() => new BestRecordFile(null));
        }
    }
}
