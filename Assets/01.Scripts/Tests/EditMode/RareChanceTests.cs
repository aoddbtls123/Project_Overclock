using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    public class RareChanceTests
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

        // 1차 빌드 설계안 3장 표. 올린 등급은 그 값, 나머지는 원래 비율(72 : 25 : 3)로 남은 몫을 나눈다
        [TestCase(0.10f, 0f, 0.624, 0.35, 0.026)]
        [TestCase(0.15f, 0f, 0.576, 0.40, 0.024)]
        [TestCase(0.10f, 0.05f, 0.57, 0.35, 0.08)]
        [TestCase(0f, 0.05f, 0.72 * 0.92 / 0.97, 0.25 * 0.92 / 0.97, 0.08)]
        public void Probabilities_FollowDesignTable(float rareBonus, float superRareBonus, double expectedN, double expectedR, double expectedSR)
        {
            var engine = new ModifierEngine(0.72, 0.25, 0.03);
            if (rareBonus > 0f)
            {
                engine.Equip(CreateModule(new ModifierEffect(ModifierType.RareChance, GradeFilter.Any, rareBonus)));
            }

            if (superRareBonus > 0f)
            {
                engine.Equip(CreateModule(new ModifierEffect(ModifierType.SuperRareChance, GradeFilter.Any, superRareBonus)));
            }

            engine.GetProbabilities(out double n, out double r, out double sr);

            Assert.AreEqual(expectedN, n, Tolerance, "N");
            Assert.AreEqual(expectedR, r, Tolerance, "R");
            Assert.AreEqual(expectedSR, sr, Tolerance, "SR");
            Assert.AreEqual(1.0, n + r + sr, 1e-9, "합");
        }

        [TestCase(0.24f, 0.49)]
        [TestCase(0.25f, 0.50)]
        [TestCase(0.30f, 0.50)]
        public void RareCap_AroundDesignLimit(float rareBonus, double expectedR)
        {
            var engine = new ModifierEngine(0.72, 0.25, 0.03);
            engine.Equip(CreateModule(new ModifierEffect(ModifierType.RareChance, GradeFilter.Any, rareBonus)));

            engine.GetProbabilities(out double n, out double r, out double sr);

            Assert.AreEqual(expectedR, r, Tolerance, "R");
            Assert.AreEqual(0.72 * (1.0 - expectedR) / 0.75, n, Tolerance, "N");
            Assert.AreEqual(0.03 * (1.0 - expectedR) / 0.75, sr, Tolerance, "SR");
        }

        [Test]
        public void RareCap_DoesNotCutBaseTableAboveCap()
        {
            // 상한은 모듈이 올리는 양에만 건다. 기본표 R 이 이미 50% 를 넘으면 그대로 둔다 (SR 의 교차검토 #06 ② 와 같은 경우)
            var engine = new ModifierEngine(0.2, 0.7, 0.1);
            engine.Equip(CreateModule(new ModifierEffect(ModifierType.RareChance, GradeFilter.Any, 0.1f)));

            engine.GetProbabilities(out double n, out double r, out double sr);

            Assert.AreEqual(0.2, n, Tolerance, "N");
            Assert.AreEqual(0.7, r, Tolerance, "R");
            Assert.AreEqual(0.1, sr, Tolerance, "SR");
        }

        [Test]
        public void EquipOrder_DoesNotChangeTable()
        {
            ModuleData rare = CreateModule(new ModifierEffect(ModifierType.RareChance, GradeFilter.Any, 0.1f));
            ModuleData superRare = CreateModule(new ModifierEffect(ModifierType.SuperRareChance, GradeFilter.Any, 0.05f));
            ModuleData bigRare = CreateModule(new ModifierEffect(ModifierType.RareChance, GradeFilter.Any, 0.2f));
            ModuleData[][] orders =
            {
                new[] { rare, superRare, bigRare },
                new[] { rare, bigRare, superRare },
                new[] { superRare, rare, bigRare },
                new[] { superRare, bigRare, rare },
                new[] { bigRare, rare, superRare },
                new[] { bigRare, superRare, rare },
            };

            foreach (ModuleData[] order in orders)
            {
                var engine = new ModifierEngine(0.72, 0.25, 0.03);
                foreach (ModuleData module in order)
                {
                    engine.Equip(module);
                }

                engine.GetProbabilities(out double n, out double r, out double sr);

                // R 0.25 + 0.3 = 0.55 → 상한 0.5 · SR 0.08 · N 은 남은 0.42
                Assert.AreEqual(0.42, n, Tolerance, "N");
                Assert.AreEqual(0.50, r, Tolerance, "R");
                Assert.AreEqual(0.08, sr, Tolerance, "SR");
            }
        }

        [Test]
        public void Preview_ExpectedOutputUsesRareTable()
        {
            var parts = new[]
            {
                CreatePart("볼트", Grade.N, 1),
                CreatePart("기어", Grade.R, 3),
                CreatePart("코어", Grade.SR, 10),
            };
            var gachaEngine = new GachaEngine(new RandomService(1), 50, 0.72, 0.25, 0.03);
            var runManager = new RunManager(gachaEngine, new PartCatalog(parts, new RandomService(2)), 8, 10);
            ModuleData rare = CreateModule(new ModifierEffect(ModifierType.RareChance, GradeFilter.Any, 0.1f));

            ModulePreview preview = runManager.PreviewModule(rare);

            // 10 × (0.624 × 1 + 0.35 × 3 + 0.026 × 10) = 19.34
            Assert.AreEqual(19.34, preview.ExpectedRoundOutputAfter(parts), Tolerance);

            // 장착하면 실제 추첨 확률표도 같은 값이 된다
            runManager.EquipModule(rare);
            Assert.AreEqual(0.35, gachaEngine.GetProbability(Grade.R), Tolerance);
            Assert.AreEqual(0.026, gachaEngine.GetProbability(Grade.SR), Tolerance);
        }

        private PartData CreatePart(string displayName, Grade grade, int baseOutput)
        {
            var part = ScriptableObject.CreateInstance<PartData>();
            part.InitializeForTests(displayName, grade, baseOutput);
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
