using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    public class PartTagTests
    {
        private const double Tolerance = 1e-6;

        private static readonly GradeFilter[] GradeFilters = { GradeFilter.Any, GradeFilter.N, GradeFilter.R, GradeFilter.SR };
        private static readonly PartTagFilter[] TagFilters = { PartTagFilter.Any, PartTagFilter.Electronic, PartTagFilter.Mechanical, PartTagFilter.Power };
        private static readonly Grade[] Grades = { Grade.N, Grade.R, Grade.SR };
        private static readonly PartTag[] Tags = { PartTag.None, PartTag.Electronic, PartTag.Mechanical, PartTag.Power };

        private readonly List<Object> createdObjects = new List<Object>();
        private PartData[] parts;
        private PartData wire;
        private PartData bolt;

        [SetUp]
        public void CreateParts()
        {
            wire = CreatePart("전선", Grade.N, 1, PartTag.Electronic);
            bolt = CreatePart("볼트", Grade.N, 1, PartTag.Mechanical);
            parts = new[]
            {
                wire,
                bolt,
                CreatePart("회로기판", Grade.R, 3, PartTag.Electronic),
                CreatePart("기어", Grade.R, 3, PartTag.Mechanical),
                CreatePart("코어", Grade.SR, 10, PartTag.Power),
            };
        }

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
        public void NewPart_HasNoTag()
        {
            // 태그 칸이 없던 기존 에셋도 이 값으로 읽힘. 아무 태그로나 조용히 분류되면 안 됨
            var part = ScriptableObject.CreateInstance<PartData>();
            createdObjects.Add(part);

            Assert.AreEqual(PartTag.None, part.Tag);
        }

        [Test]
        public void Effects_ApplyOnlyWhenGradeAndTagBothMatch()
        {
            int appliedCount = 0;
            foreach (GradeFilter gradeFilter in GradeFilters)
            {
                foreach (PartTagFilter tagFilter in TagFilters)
                {
                    var engine = new ModifierEngine(0.72, 0.25, 0.03);
                    engine.Equip(CreateModule(
                        new ModifierEffect(ModifierType.OutputMultiplier, gradeFilter, tagFilter, 1f),
                        new ModifierEffect(ModifierType.StreakBonus, gradeFilter, tagFilter, 10f)));

                    foreach (Grade grade in Grades)
                    {
                        foreach (PartTag tag in Tags)
                        {
                            bool applies = ExpectedGrades(gradeFilter).Contains(grade) && ExpectedTags(tagFilter).Contains(tag);
                            string label = gradeFilter + "/" + tagFilter + " → " + grade + "/" + tag;

                            // 배수 ×2 · 연속 +10 → 가산 후 승산 (3 + 10) × 2 = 26
                            Assert.AreEqual(applies ? 6 : 3, engine.CalculateOutput(grade, tag, 3, false), label);
                            Assert.AreEqual(applies ? 26 : 3, engine.CalculateOutput(grade, tag, 3, true), label);

                            if (applies)
                            {
                                appliedCount++;
                            }
                        }
                    }
                }
            }

            // 등급 쪽 (3 + 1 + 1 + 1) × 태그 쪽 (4 + 1 + 1 + 1) = 42 / 전체 4 × 4 × 3 × 4 = 192
            Assert.AreEqual(42, appliedCount);
        }

        [Test]
        public void Draw_UsesTagOfDrawnPart()
        {
            int electronicCount = 0;
            int otherCount = 0;
            for (uint seed = 1; seed <= 30; seed++)
            {
                ModuleData electronicDouble = CreateModule(
                    new ModifierEffect(ModifierType.OutputMultiplier, GradeFilter.Any, PartTagFilter.Electronic, 1f));
                RunManager run = RunSession.Create(seed, parts, new[] { electronicDouble }).RunManager;
                run.EquipModule(electronicDouble);

                while (run.State == RunState.Drawing || run.State == RunState.RoundCleared)
                {
                    if (run.State == RunState.RoundCleared)
                    {
                        run.AdvanceRound();
                        continue;
                    }

                    DrawResult result = run.Draw();
                    bool electronic = result.Part.Tag == PartTag.Electronic;
                    int expected = electronic ? result.Part.BaseOutput * 2 : result.Part.BaseOutput;
                    Assert.AreEqual(expected, result.Output, result.Part.DisplayName + " seed=" + seed);

                    if (electronic)
                    {
                        electronicCount++;
                    }
                    else
                    {
                        otherCount++;
                    }
                }
            }

            Assert.GreaterOrEqual(electronicCount, 50);
            Assert.GreaterOrEqual(otherCount, 50);
        }

        [Test]
        public void Preview_UsesTagOfEachPart()
        {
            // 전자 ×2 → N 평균 (2 + 1) / 2 = 1.5 · R 평균 (6 + 3) / 2 = 4.5 · SR(동력) 10
            // 10회 × (0.72 × 1.5 + 0.25 × 4.5 + 0.03 × 10) = 25.05
            ModuleData electronicDouble = CreateModule(
                new ModifierEffect(ModifierType.OutputMultiplier, GradeFilter.Any, PartTagFilter.Electronic, 1f));
            RunManager run = RunSession.Create(1, parts, new[] { electronicDouble }).RunManager;

            ModulePreview preview = run.PreviewModule(electronicDouble);

            Assert.AreEqual(2, preview.OutputAfter(wire, false));
            Assert.AreEqual(1, preview.OutputAfter(bolt, false));
            Assert.AreEqual(1, preview.OutputBefore(wire, false));
            Assert.AreEqual(17.7, preview.ExpectedRoundOutputBefore(parts), Tolerance);
            Assert.AreEqual(25.05, preview.ExpectedRoundOutputAfter(parts), Tolerance);
        }

        private static List<Grade> ExpectedGrades(GradeFilter filter)
        {
            switch (filter)
            {
                case GradeFilter.Any:
                    return new List<Grade> { Grade.N, Grade.R, Grade.SR };
                case GradeFilter.N:
                    return new List<Grade> { Grade.N };
                case GradeFilter.R:
                    return new List<Grade> { Grade.R };
                default:
                    return new List<Grade> { Grade.SR };
            }
        }

        private static List<PartTag> ExpectedTags(PartTagFilter filter)
        {
            switch (filter)
            {
                case PartTagFilter.Any:
                    return new List<PartTag> { PartTag.None, PartTag.Electronic, PartTag.Mechanical, PartTag.Power };
                case PartTagFilter.Electronic:
                    return new List<PartTag> { PartTag.Electronic };
                case PartTagFilter.Mechanical:
                    return new List<PartTag> { PartTag.Mechanical };
                default:
                    return new List<PartTag> { PartTag.Power };
            }
        }

        private PartData CreatePart(string name, Grade grade, int output, PartTag tag)
        {
            var part = ScriptableObject.CreateInstance<PartData>();
            part.InitializeForTests(name, grade, output, tag);
            createdObjects.Add(part);
            return part;
        }

        private ModuleData CreateModule(params ModifierEffect[] effects)
        {
            var module = ScriptableObject.CreateInstance<ModuleData>();
            module.InitializeForTests("태그 검사", effects);
            createdObjects.Add(module);
            return module;
        }
    }
}
