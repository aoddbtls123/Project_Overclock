using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    public class ExpectedRoundOutputTests
    {
        private const double Tolerance = 1e-6;

        private readonly List<Object> createdObjects = new List<Object>();
        private PartData[] parts;

        [SetUp]
        public void CreateParts()
        {
            parts = new[]
            {
                CreatePart("볼트", Grade.N, 1),
                CreatePart("전선", Grade.N, 1),
                CreatePart("코일", Grade.R, 3),
                CreatePart("기어", Grade.R, 3),
                CreatePart("코어", Grade.SR, 10),
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
        public void Before_UsesCurrentEquipment_AfterAddsOfferedModule()
        {
            // 전: 코일 장착 = 10회 × (0.72×1 + 0.25×4 + 0.03×13) = 21.1
            // 후: 코일 + 교정 = 10회 × (0.72×0.92/0.97 × 1 + 0.25×0.92/0.97 × 4 + 0.08 × 13) = 26.7134020619
            RunManager run = CreateRun();
            run.EquipModule(CreateModule(ModifierType.OutputMultiplier, 0.3f));

            ModulePreview preview = run.PreviewModule(CreateModule(ModifierType.SuperRareChance, 0.05f));

            Assert.AreEqual(21.1, preview.ExpectedRoundOutputBefore(parts), Tolerance);
            Assert.AreEqual(26.713402061855670, preview.ExpectedRoundOutputAfter(parts), Tolerance);
        }

        [Test]
        public void BeforeAndAfter_UseTheirOwnDrawCounts()
        {
            // 1회 기대 1.77 → 전 10회 17.7 · 과부하 장착 후 12회 21.24
            RunManager run = CreateRun();

            ModulePreview preview = run.PreviewModule(CreateModule(ModifierType.ExtraDraws, 2f));

            Assert.AreEqual(17.7, preview.ExpectedRoundOutputBefore(parts), Tolerance);
            Assert.AreEqual(21.24, preview.ExpectedRoundOutputAfter(parts), Tolerance);
        }

        [Test]
        public void Before_IncludesStreakBonusAlreadyEquipped()
        {
            // 동기화 장착 상태의 '전' = 17.7 + 발동 기대 1.5655680623회 × 10 = 33.3556806232828
            RunManager run = CreateRun();
            run.EquipModule(CreateModule(ModifierType.StreakBonus, 10f));

            ModulePreview preview = run.PreviewModule(CreateModule(ModifierType.ExtraDraws, 0f));

            Assert.AreEqual(33.35568062328275, preview.ExpectedRoundOutputBefore(parts), Tolerance);
            Assert.AreEqual(33.35568062328275, preview.ExpectedRoundOutputAfter(parts), Tolerance);
        }

        private RunManager CreateRun()
        {
            return RunSession.Create(1, parts, new[] { CreateModule(ModifierType.ExtraDraws, 2f) }).RunManager;
        }

        private PartData CreatePart(string name, Grade grade, int output)
        {
            var part = ScriptableObject.CreateInstance<PartData>();
            part.InitializeForTests(name, grade, output);
            createdObjects.Add(part);
            return part;
        }

        private ModuleData CreateModule(ModifierType type, float value)
        {
            var module = ScriptableObject.CreateInstance<ModuleData>();
            module.InitializeForTests(type.ToString(), new ModifierEffect(type, GradeFilter.Any, value));
            createdObjects.Add(module);
            return module;
        }
    }
}
