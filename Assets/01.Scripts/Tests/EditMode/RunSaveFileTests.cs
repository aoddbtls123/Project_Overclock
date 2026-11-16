using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    // 실제 JsonUtility 와 파일 입출력을 씀. 임시 폴더에서만 읽고 씀
    public class RunSaveFileTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

        private string directory;
        private PartData[] parts;
        private ModuleData[] modules;

        [SetUp]
        public void CreateData()
        {
            directory = Path.Combine(Path.GetTempPath(), "OverclockSaveTests_" + Guid.NewGuid().ToString("N"));
            parts = new[]
            {
                CreatePart("볼트", Grade.N, 1, PartTag.Mechanical),
                CreatePart("전선", Grade.N, 1, PartTag.Electronic),
                CreatePart("코일", Grade.R, 3, PartTag.Power),
                CreatePart("코어", Grade.SR, 10, PartTag.Power),
            };
            modules = new[]
            {
                CreateModule("sr_up", ModuleAxis.Probability, ModifierType.SuperRareChance, 0.05f),
                CreateModule("coil", ModuleAxis.Multiplier, ModifierType.OutputMultiplier, 0.3f),
                CreateModule("sync", ModuleAxis.Conditional, ModifierType.StreakBonus, 10f),
                CreateModule("carry", ModuleAxis.Draws, ModifierType.CarryOverDraws, 5f),
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

        // 파일을 거쳐도 메모리 복원과 똑같이 이어지는지. 카드가 열린 상태 · 무한모드 · 장착 모듈이 있는 상태를 지남
        [TestCase(RunMode.Normal)]
        [TestCase(RunMode.Endless)]
        public void SaveThenLoad_ContinuesExactlyLikeOriginal(RunMode mode)
        {
            var file = new RunSaveFile(directory);
            int checkedWithOffer = 0;
            for (uint seed = 1; seed <= 5; seed++)
            {
                RunSession original = RunSession.Create(seed, parts, modules, mode);
                for (int step = 0; step < 400 && Step(original) != null; step++)
                {
                    file.Save(original);
                    Assert.IsTrue(file.TryLoad(parts, modules, out RunSession loaded, out string error), error);
                    Assert.AreEqual(Describe(original.CaptureSaveData()), Describe(loaded.CaptureSaveData()), "seed=" + seed + " step=" + step);
                    if (original.CurrentOffer.Count > 0)
                    {
                        checkedWithOffer++;
                    }

                    // 몇 단계 더 진행해 같은 결과가 나오는지 (난수 상태가 실제로 이어지는지)
                    RunSession originalCopy = RunSession.Restore(original.CaptureSaveData(), parts, modules);
                    for (int i = 0; i < 5; i++)
                    {
                        Assert.AreEqual(Step(originalCopy), Step(loaded), "seed=" + seed + " step=" + step + " 이후 " + i);
                    }
                }
            }

            Assert.GreaterOrEqual(checkedWithOffer, 10, "카드가 열린 상태 저장");
        }

        // 난수 상태는 uint 다. int 범위를 넘는 값이 음수나 0 으로 바뀌지 않고 그대로 돌아와야 함
        [Test]
        public void LargeRandomState_RoundTrips()
        {
            var file = new RunSaveFile(directory);
            RunSession session = RunSession.Create(3, parts, modules);
            while (session.CaptureSaveData().randomState <= int.MaxValue)
            {
                session.RunManager.Draw();
                if (session.RunManager.State != RunState.Drawing)
                {
                    session = RunSession.Create(session.Seed + 1, parts, modules);
                }
            }

            uint state = session.CaptureSaveData().randomState;
            file.Save(session);

            Assert.IsTrue(file.TryLoad(parts, modules, out RunSession loaded, out string error), error);
            Assert.AreEqual(state, loaded.CaptureSaveData().randomState);
            Assert.IsTrue(File.ReadAllText(file.FilePath).Contains(state.ToString()), "파일에 부호 없는 값 그대로");
        }

        [Test]
        public void Save_OverwritesAndLeavesNoTempFile()
        {
            var file = new RunSaveFile(directory);
            RunSession session = RunSession.Create(3, parts, modules);
            file.Save(session);
            session.RunManager.Draw();
            file.Save(session);

            Assert.IsTrue(file.TryLoad(parts, modules, out RunSession loaded, out string error), error);
            Assert.IsTrue(error == null, "성공하면 error 는 null");
            Assert.AreEqual(Describe(session.CaptureSaveData()), Describe(loaded.CaptureSaveData()));
            Assert.AreEqual(1, Directory.GetFiles(directory).Length, "임시 파일이 남지 않음");
        }

        [Test]
        public void Save_CreatesMissingDirectory()
        {
            var file = new RunSaveFile(Path.Combine(directory, "sub"));

            file.Save(RunSession.Create(3, parts, modules));

            Assert.IsTrue(file.Exists);
        }

        [Test]
        public void TryLoad_FailsWithoutFile()
        {
            var file = new RunSaveFile(directory);

            Assert.IsFalse(file.Exists);
            Assert.IsFalse(file.TryLoad(parts, modules, out RunSession loaded, out string error));
            Assert.IsTrue(loaded == null);
            Assert.IsFalse(string.IsNullOrEmpty(error));
        }

        [Test]
        public void TryLoad_FailsOnBrokenFile()
        {
            var file = new RunSaveFile(directory);
            Directory.CreateDirectory(directory);
            var brokenTexts = new[] { "", "{ not json", "[1, 2, 3]", "{\"version\": \"abc\"}" };

            foreach (string text in brokenTexts)
            {
                File.WriteAllText(file.FilePath, text);
                Assert.IsFalse(file.TryLoad(parts, modules, out RunSession loaded, out string error), "읽으면 안 됨: " + text);
                Assert.IsTrue(loaded == null);
                Assert.IsFalse(string.IsNullOrEmpty(error));
            }
        }

        [Test]
        public void TryLoad_FailsOnDataThatCannotBeRestored()
        {
            var file = new RunSaveFile(directory);
            RunSaveData valid = RunSession.Create(3, parts, modules).CaptureSaveData();

            // 고치지 않은 값은 읽혀야 함
            file.Write(valid);
            Assert.IsTrue(file.TryLoad(parts, modules, out RunSession validSession, out string firstError), firstError);

            RunSaveData otherVersion = RunSession.Create(3, parts, modules).CaptureSaveData();
            otherVersion.version = RunSaveData.CurrentVersion + 1;
            file.Write(otherVersion);
            Assert.IsFalse(file.TryLoad(parts, modules, out RunSession versionSession, out string versionError));
            Assert.IsTrue(versionError.Contains("버전"), versionError);

            RunSaveData unknownModule = RunSession.Create(3, parts, modules).CaptureSaveData();
            unknownModule.equippedModuleIds = new[] { "removed_module" };
            file.Write(unknownModule);
            Assert.IsFalse(file.TryLoad(parts, modules, out RunSession moduleSession, out string moduleError));
            Assert.IsTrue(moduleError.Contains("removed_module"), moduleError);

            // 세이브는 그대로 둠. 지우는 것은 부르는 쪽이 정함
            Assert.IsTrue(file.Exists);
        }

        [Test]
        public void Delete_RemovesFileAndIgnoresMissingFile()
        {
            var file = new RunSaveFile(directory);
            file.Save(RunSession.Create(3, parts, modules));

            file.Delete();
            Assert.IsFalse(file.Exists);

            file.Delete();
            Assert.IsFalse(file.Exists);
        }

        [Test]
        public void Constructor_RejectsEmptyDirectory()
        {
            Assert.Throws<ArgumentException>(() => new RunSaveFile(""));
            Assert.Throws<ArgumentException>(() => new RunSaveFile(null));
        }

        private static string Step(RunSession session)
        {
            RunManager runManager = session.RunManager;
            if (runManager.State == RunState.Drawing)
            {
                DrawResult result = runManager.Draw();
                return "뽑기 " + result.Part.DisplayName + " +" + result.Output + " " + result.StreakTriggered + " | " + Describe(session.CaptureSaveData());
            }

            if (runManager.State != RunState.RoundCleared)
            {
                return null;
            }

            if (session.CurrentOffer.Count == 0)
            {
                session.OpenOffer();
                return "제안 | " + Describe(session.CaptureSaveData());
            }

            IReadOnlyList<ModuleData> cards = session.CurrentOffer;
            ModuleData choice = cards[runManager.CurrentRound % cards.Count];
            session.SelectModule(choice);
            return "선택 " + choice.Id + " | " + Describe(session.CaptureSaveData());
        }

        private static string Describe(RunSaveData data)
        {
            return "v" + data.version + " " + data.mode + " 시드 " + data.seed + " 난수 " + data.randomState + " 보증 " + data.drawsWithoutSuperRare
                + " R" + data.round + " 생산 " + data.production + " 남은 " + data.drawsRemaining + " " + data.state
                + " 연속 " + data.streakGrade + "x" + data.streakCount
                + " 장착 " + string.Join(",", data.equippedModuleIds) + " 제안 " + string.Join(",", data.offeredModuleIds);
        }
    }
}
