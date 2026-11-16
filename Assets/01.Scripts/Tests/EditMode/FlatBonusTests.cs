using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    public class FlatBonusTests
    {
        private const double Tolerance = 1e-6;

        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void DestroyCreatedObjects()
        {
            foreach (Object created in createdObjects)
            {
                Object.DestroyImmediate(created);
            }

            createdObjects.Clear();
        }

        [Test]
        public void ModifierType_KeepsSavedValues()
        {
            // 모듈 에셋은 효과 종류를 숫자로 저장함. 중간에 끼워 넣으면 기존 에셋의 효과가 조용히 다른 종류로 바뀜
            Assert.AreEqual(0, (int)ModifierType.SuperRareChance);
            Assert.AreEqual(1, (int)ModifierType.OutputMultiplier);
            Assert.AreEqual(2, (int)ModifierType.StreakBonus);
            Assert.AreEqual(3, (int)ModifierType.ExtraDraws);
            Assert.AreEqual(4, (int)ModifierType.FlatBonus);
            Assert.AreEqual(5, (int)ModifierType.RareChance);
            Assert.AreEqual(6, (int)ModifierType.CarryOverDraws);
        }

        [Test]
        public void FlatBonus_IsAddedBeforeMultiplier()
        {
            var engine = new ModifierEngine(0.72, 0.25, 0.03);
            engine.Equip(CreateModule(new ModifierEffect(ModifierType.FlatBonus, GradeFilter.Any, 2f)));
            engine.Equip(CreateModule(new ModifierEffect(ModifierType.OutputMultiplier, GradeFilter.Any, 0.3f)));
            engine.Equip(CreateModule(new ModifierEffect(ModifierType.StreakBonus, GradeFilter.Any, 10f)));

            // (1 + 2) × 1.3 = 3.9 → 4. 승산 뒤에 더하면 1 × 1.3 + 2 = 3.3 → 3
            Assert.AreEqual(4, engine.CalculateOutput(Grade.N, PartTag.Mechanical, 1, false));
            // (1 + 2 + 10) × 1.3 = 16.9 → 17. 고정 보너스는 연속이 아닐 때도, 연속일 때도 더함
            Assert.AreEqual(17, engine.CalculateOutput(Grade.N, PartTag.Mechanical, 1, true));
            // (10 + 2) × 1.3 = 15.6 → 16
            Assert.AreEqual(16, engine.CalculateOutput(Grade.SR, PartTag.Power, 10, false));
        }

        [Test]
        public void FlatBonus_AppliesOnlyToMatchingGradeAndTag()
        {
            var engine = new ModifierEngine(0.72, 0.25, 0.03);
            engine.Equip(CreateModule(new ModifierEffect(ModifierType.FlatBonus, GradeFilter.Any, PartTagFilter.Electronic, 2f)));
            engine.Equip(CreateModule(new ModifierEffect(ModifierType.FlatBonus, GradeFilter.R, PartTagFilter.Power, 5f)));

            Assert.AreEqual(3, engine.CalculateOutput(Grade.N, PartTag.Electronic, 1, false), "N 전자");
            Assert.AreEqual(1, engine.CalculateOutput(Grade.N, PartTag.Mechanical, 1, false), "N 기계");
            Assert.AreEqual(1, engine.CalculateOutput(Grade.N, PartTag.Power, 1, false), "N 동력 (R 전용 효과)");
            Assert.AreEqual(5, engine.CalculateOutput(Grade.R, PartTag.Electronic, 3, false), "R 전자");
            Assert.AreEqual(8, engine.CalculateOutput(Grade.R, PartTag.Power, 3, false), "R 동력");
            Assert.AreEqual(3, engine.CalculateOutput(Grade.R, PartTag.Mechanical, 3, false), "R 기계");
            Assert.AreEqual(12, engine.CalculateOutput(Grade.SR, PartTag.Electronic, 10, false), "SR 전자");
            Assert.AreEqual(10, engine.CalculateOutput(Grade.SR, PartTag.Power, 10, false), "SR 동력 (R 전용 효과)");
        }

        [Test]
        public void ExpectedRoundOutput_AveragesTagBonusWithinGrade()
        {
            PartData[] parts = CreateTwelveParts();
            var gachaEngine = new GachaEngine(new RandomService(1), 50, 0.72, 0.25, 0.03);
            var runManager = new RunManager(gachaEngine, new PartCatalog(parts, new RandomService(2)), 8, 10);
            ModuleData electronicBonus = CreateModule(new ModifierEffect(ModifierType.FlatBonus, GradeFilter.Any, PartTagFilter.Electronic, 2f));

            ModulePreview preview = runManager.PreviewModule(electronicBonus);

            // 전자 부품: N 6개 중 2개 · R 4개 중 1개 · SR 2개 중 1개
            // 10 × (0.72 × (1 + 2 × 2/6) + 0.25 × (3 + 2 × 1/4) + 0.03 × (10 + 2 × 1/2)) = 24.05
            Assert.AreEqual(17.7, preview.ExpectedRoundOutputBefore(parts), Tolerance);
            Assert.AreEqual(24.05, preview.ExpectedRoundOutputAfter(parts), Tolerance);
        }

        [Test]
        public void Preview_ShowsBonusOnlyForMatchingPart()
        {
            PartData[] parts = CreateTwelveParts();
            var gachaEngine = new GachaEngine(new RandomService(1), 50, 0.72, 0.25, 0.03);
            var runManager = new RunManager(gachaEngine, new PartCatalog(parts, new RandomService(2)), 8, 10);
            ModuleData powerBonus = CreateModule(new ModifierEffect(ModifierType.FlatBonus, GradeFilter.Any, PartTagFilter.Power, 2f));

            ModulePreview preview = runManager.PreviewModule(powerBonus);

            PartData fuse = parts[4];
            PartData wire = parts[0];
            Assert.AreEqual(1, preview.OutputBefore(fuse, false));
            Assert.AreEqual(3, preview.OutputAfter(fuse, false));
            Assert.AreEqual(1, preview.OutputAfter(wire, false));
        }

        private PartData[] CreateTwelveParts()
        {
            return new[]
            {
                CreatePart("전선", Grade.N, 1, PartTag.Electronic),
                CreatePart("저항", Grade.N, 1, PartTag.Electronic),
                CreatePart("볼트", Grade.N, 1, PartTag.Mechanical),
                CreatePart("나사", Grade.N, 1, PartTag.Mechanical),
                CreatePart("퓨즈", Grade.N, 1, PartTag.Power),
                CreatePart("스프링", Grade.N, 1, PartTag.Power),
                CreatePart("회로기판", Grade.R, 3, PartTag.Electronic),
                CreatePart("기어", Grade.R, 3, PartTag.Mechanical),
                CreatePart("코일", Grade.R, 3, PartTag.Power),
                CreatePart("배터리", Grade.R, 3, PartTag.Power),
                CreatePart("프로세서", Grade.SR, 10, PartTag.Electronic),
                CreatePart("코어", Grade.SR, 10, PartTag.Power),
            };
        }

        private PartData CreatePart(string displayName, Grade grade, int baseOutput, PartTag tag)
        {
            var part = ScriptableObject.CreateInstance<PartData>();
            part.InitializeForTests(displayName, grade, baseOutput, tag);
            createdObjects.Add(part);
            return part;
        }

        private ModuleData CreateModule(params ModifierEffect[] effects)
        {
            var module = ScriptableObject.CreateInstance<ModuleData>();
            module.InitializeForTests("테스트 모듈", effects);
            createdObjects.Add(module);
            return module;
        }
    }
}
