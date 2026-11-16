using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    public class ModifierCombinationTests
    {
        private const uint CombinationCount = 10000;
        private const int MaxModules = 7;
        private const int MaxEffectsPerModule = 2;
        private const int MinCasesPerBranch = 1000;
        private const double Tolerance = 1e-9;
        private const double FloatInputTolerance = 1e-6;

        private static readonly Grade[] Grades = { Grade.N, Grade.R, Grade.SR };
        private static readonly PartTag[] Tags = { PartTag.None, PartTag.Electronic, PartTag.Mechanical, PartTag.Power };
        private static readonly int[] DesignBaseOutputs = { 1, 3, 10 };

        // 이 조합 검사가 독립 기대값을 가진 효과 목록. enum 순서 그대로 둠 (시드별 조합이 바뀌지 않게).
        // ModifierType 에 새 효과를 추가하면 ① 이 목록 ② CreateValue 의 값 범위
        // ③ RandomCombinations_KeepTableValidAndFollowDesignRules 의 기대값 계산을 함께 보완해야 함
        private static readonly ModifierType[] CheckedTypes =
        {
            ModifierType.SuperRareChance,
            ModifierType.OutputMultiplier,
            ModifierType.StreakBonus,
            ModifierType.ExtraDraws,
            ModifierType.FlatBonus,
            ModifierType.RareChance,
            ModifierType.CarryOverDraws
        };

        private readonly List<ModuleData> createdModules = new List<ModuleData>();

        [TearDown]
        public void DestroyCreatedModules()
        {
            foreach (ModuleData module in createdModules)
            {
                UnityEngine.Object.DestroyImmediate(module);
            }

            createdModules.Clear();
        }

        [Test]
        public void CheckedTypes_CoverEveryModifierType()
        {
            Array allTypes = Enum.GetValues(typeof(ModifierType));
            var missing = new List<string>();
            foreach (ModifierType type in allTypes)
            {
                if (Array.IndexOf(CheckedTypes, type) < 0)
                {
                    missing.Add(type.ToString());
                }
            }

            Assert.IsTrue(missing.Count == 0, "조합 검사에 기대값이 없는 효과: " + string.Join(", ", missing)
                + " — CheckedTypes · CreateValue · 기대값 계산을 함께 보완");
            Assert.AreEqual(allTypes.Length, CheckedTypes.Length, "CheckedTypes 에 같은 효과가 두 번 있음");
        }

        [Test]
        public void RandomCombinations_KeepTableValidAndFollowDesignRules()
        {
            var modules = new ModuleData[MaxModules];
            for (int i = 0; i < MaxModules; i++)
            {
                modules[i] = ScriptableObject.CreateInstance<ModuleData>();
                createdModules.Add(modules[i]);
            }

            int superRareUntouched = 0;
            int superRareRaised = 0;
            int superRareCapped = 0;
            int multiplierRaised = 0;
            int multiplierCapped = 0;
            int drawsRaised = 0;
            int drawsCapped = 0;
            int flatApplied = 0;
            int rareUntouched = 0;
            int rareRaised = 0;
            int rareCapped = 0;
            int carryApplied = 0;

            for (uint seed = 1; seed <= CombinationCount; seed++)
            {
                List<ModifierEffect[]> combination = CreateCombination(seed);
                var engine = new ModifierEngine(0.72, 0.25, 0.03);
                for (int i = 0; i < combination.Count; i++)
                {
                    modules[i].InitializeForTests("Module" + i, combination[i]);
                    engine.Equip(modules[i]);
                }

                engine.GetProbabilities(out double n, out double r, out double sr);
                AssertValidTable(n, r, sr, seed, combination);

                // 기대값은 기획 규칙으로 따로 계산함. 엔진 상수를 쓰지 않음
                // 올린 등급(SR 상한 25%, R 상한 50%)은 그 값, 올리지 않은 등급은 기본표 72 : 25 : 3 비율로 남은 몫을 나눔
                double superRareBonus = SumValues(combination, ModifierType.SuperRareChance, null, null);
                double rareBonus = SumValues(combination, ModifierType.RareChance, null, null);
                double raisedSR = Math.Min(0.03 + superRareBonus, 0.25);
                double raisedR = Math.Min(0.25 + rareBonus, 0.5);
                double expectedN;
                double expectedR;
                double expectedSR;
                if (superRareBonus > 0.0 && rareBonus > 0.0)
                {
                    expectedSR = raisedSR;
                    expectedR = raisedR;
                    expectedN = 1.0 - raisedSR - raisedR;
                }
                else if (superRareBonus > 0.0)
                {
                    expectedSR = raisedSR;
                    expectedR = 0.25 * (1.0 - raisedSR) / 0.97;
                    expectedN = 0.72 * (1.0 - raisedSR) / 0.97;
                }
                else if (rareBonus > 0.0)
                {
                    expectedR = raisedR;
                    expectedSR = 0.03 * (1.0 - raisedR) / 0.75;
                    expectedN = 0.72 * (1.0 - raisedR) / 0.75;
                }
                else
                {
                    expectedN = 0.72;
                    expectedR = 0.25;
                    expectedSR = 0.03;
                }

                AssertClose("SR", expectedSR, sr, seed, combination);
                AssertClose("N", expectedN, n, seed, combination);
                AssertClose("R", expectedR, r, seed, combination);
                AssertGachaAccepts(n, r, sr, seed, combination);

                if (rareBonus == 0.0)
                {
                    rareUntouched++;
                }
                else if (0.25 + rareBonus < 0.5)
                {
                    rareRaised++;
                }
                else
                {
                    rareCapped++;
                }

                if (superRareBonus == 0.0)
                {
                    superRareUntouched++;
                }
                else if (0.03 + superRareBonus < 0.25)
                {
                    superRareRaised++;
                }
                else
                {
                    superRareCapped++;
                }

                bool anyMultiplierCapped = false;
                bool anyMultiplierRaised = false;
                bool anyFlatApplied = false;
                foreach (Grade grade in Grades)
                {
                    foreach (PartTag tag in Tags)
                    {
                        double increase = SumValues(combination, ModifierType.OutputMultiplier, grade, tag);
                        double multiplier = Math.Min(1.0 + increase, 8.0);
                        double flatBonus = SumValues(combination, ModifierType.FlatBonus, grade, tag);
                        double streakBonus = SumValues(combination, ModifierType.StreakBonus, grade, tag);
                        int baseOutput = DesignBaseOutputs[(int)grade];
                        string label = grade + "/" + tag;

                        AssertOutput(label + " 생산량", RoundHalfAway((baseOutput + flatBonus) * multiplier),
                            engine.CalculateOutput(grade, tag, baseOutput, false), seed, combination);
                        AssertOutput(label + " 3연속 생산량", RoundHalfAway((baseOutput + flatBonus + streakBonus) * multiplier),
                            engine.CalculateOutput(grade, tag, baseOutput, true), seed, combination);

                        int largeOutput = engine.CalculateOutput(grade, tag, 1000, false);
                        if (largeOutput > RoundHalfAway((1000 + flatBonus) * 8.0))
                        {
                            Assert.Fail(label + " 배수 상한 ×8 초과: 생산량 1000 → " + largeOutput + " | " + Describe(seed, combination));
                        }

                        if (1.0 + increase >= 8.0)
                        {
                            anyMultiplierCapped = true;
                        }
                        else if (increase > 0.0)
                        {
                            anyMultiplierRaised = true;
                        }

                        if (flatBonus > 0.0)
                        {
                            anyFlatApplied = true;
                        }
                    }
                }

                if (anyFlatApplied)
                {
                    flatApplied++;
                }

                if (anyMultiplierCapped)
                {
                    multiplierCapped++;
                }
                else if (anyMultiplierRaised)
                {
                    multiplierRaised++;
                }

                int extraDraws = RoundHalfAway(SumValues(combination, ModifierType.ExtraDraws, null, null));
                int draws = engine.DrawsPerRound(10);
                if (draws > 20)
                {
                    Assert.Fail("뽑기 상한 20회 초과: " + draws + " | " + Describe(seed, combination));
                }

                AssertOutput("뽑기 횟수", Math.Min(10 + extraDraws, 20), draws, seed, combination);

                // 남은 뽑기는 시드에서 정함 (0 ~ 10). 생성기 난수를 더 쓰지 않아 기존 조합이 그대로임
                int remaining = (int)(seed % 11);
                int carryCap = RoundHalfAway(SumValues(combination, ModifierType.CarryOverDraws, null, null));
                int carried = Math.Min(remaining, carryCap);
                AssertOutput("다음 라운드 뽑기 (남은 " + remaining + ")", Math.Min(10 + extraDraws + carried, 20),
                    engine.DrawsForNextRound(10, remaining), seed, combination);
                if (carried > 0)
                {
                    carryApplied++;
                }

                if (10 + extraDraws >= 20)
                {
                    drawsCapped++;
                }
                else if (extraDraws > 0)
                {
                    drawsRaised++;
                }
            }

            string coverage = "SR 그대로 " + superRareUntouched + " · SR 상승 " + superRareRaised + " · SR 상한 " + superRareCapped
                + " · 배수 상승 " + multiplierRaised + " · 배수 상한 " + multiplierCapped
                + " · 뽑기 상승 " + drawsRaised + " · 뽑기 상한 " + drawsCapped + " · 고정 보너스 " + flatApplied
                + " · R 그대로 " + rareUntouched + " · R 상승 " + rareRaised + " · R 상한 " + rareCapped
                + " · 이월 " + carryApplied;
            Assert.IsTrue(superRareUntouched >= MinCasesPerBranch && superRareRaised >= MinCasesPerBranch
                && superRareCapped >= MinCasesPerBranch && multiplierRaised >= MinCasesPerBranch
                && multiplierCapped >= MinCasesPerBranch && drawsRaised >= MinCasesPerBranch
                && drawsCapped >= MinCasesPerBranch && flatApplied >= MinCasesPerBranch
                && rareUntouched >= MinCasesPerBranch && rareRaised >= MinCasesPerBranch && rareCapped >= MinCasesPerBranch
                && carryApplied >= MinCasesPerBranch,
                "조합이 상한 안/밖을 고루 지나지 않음: " + coverage);
        }

        [TestCase(0.21f, 0.24)]
        [TestCase(0.23f, 0.25)]
        public void SuperRareCap_AroundDesignLimit(float bonus, double expectedSR)
        {
            var engine = new ModifierEngine(0.72, 0.25, 0.03);
            engine.Equip(CreateModule(ModifierType.SuperRareChance, GradeFilter.Any, bonus));

            engine.GetProbabilities(out double n, out double r, out double sr);

            Assert.AreEqual(expectedSR, sr, FloatInputTolerance, "SR");
            Assert.AreEqual(0.72 * (1.0 - expectedSR) / 0.97, n, FloatInputTolerance, "N");
            Assert.AreEqual(0.25 * (1.0 - expectedSR) / 0.97, r, FloatInputTolerance, "R");
            Assert.AreEqual(1.0, n + r + sr, Tolerance, "합");
        }

        [TestCase(6.9f, 79)]
        [TestCase(7f, 80)]
        [TestCase(7.5f, 80)]
        public void MultiplierCap_AroundDesignLimit(float increase, int expectedSuperRareOutput)
        {
            var engine = new ModifierEngine(0.72, 0.25, 0.03);
            engine.Equip(CreateModule(ModifierType.OutputMultiplier, GradeFilter.Any, increase));

            Assert.AreEqual(expectedSuperRareOutput, engine.CalculateOutput(Grade.SR, PartTag.None, 10, false));
        }

        [TestCase(9f, 19)]
        [TestCase(10f, 20)]
        [TestCase(11f, 20)]
        public void DrawsCap_AroundDesignLimit(float extraDraws, int expectedDraws)
        {
            var engine = new ModifierEngine(0.72, 0.25, 0.03);
            engine.Equip(CreateModule(ModifierType.ExtraDraws, GradeFilter.Any, extraDraws));

            Assert.AreEqual(expectedDraws, engine.DrawsPerRound(10));
        }

        private ModuleData CreateModule(ModifierType type, GradeFilter target, float value)
        {
            var module = ScriptableObject.CreateInstance<ModuleData>();
            module.InitializeForTests(type.ToString(), new ModifierEffect(type, target, value));
            createdModules.Add(module);
            return module;
        }

        // 조합 하나 = 시드 하나. 실패 메시지의 시드만으로 같은 조합을 다시 만듦
        private static List<ModifierEffect[]> CreateCombination(uint seed)
        {
            var random = new RandomService(seed);
            int moduleCount = (int)(random.NextDouble() * (MaxModules + 1));
            var combination = new List<ModifierEffect[]>(moduleCount);

            for (int i = 0; i < moduleCount; i++)
            {
                int effectCount = 1 + (int)(random.NextDouble() * MaxEffectsPerModule);
                var effects = new ModifierEffect[effectCount];
                for (int j = 0; j < effectCount; j++)
                {
                    ModifierType type = CheckedTypes[(int)(random.NextDouble() * CheckedTypes.Length)];
                    var target = (GradeFilter)(int)(random.NextDouble() * 4);
                    var tagTarget = (PartTagFilter)(int)(random.NextDouble() * 4);
                    effects[j] = new ModifierEffect(type, target, tagTarget, CreateValue(type, random));
                }

                combination.Add(effects);
            }

            return combination;
        }

        private static float CreateValue(ModifierType type, RandomService random)
        {
            switch (type)
            {
                case ModifierType.SuperRareChance:
                    return (float)(random.NextDouble() * 0.25);
                case ModifierType.OutputMultiplier:
                    return (float)(random.NextDouble() * 8.0);
                case ModifierType.StreakBonus:
                    return (float)(random.NextDouble() * 20.0);
                case ModifierType.FlatBonus:
                    return (float)(random.NextDouble() * 5.0);
                case ModifierType.RareChance:
                    return (float)(random.NextDouble() * 0.3);
                case ModifierType.ExtraDraws:
                case ModifierType.CarryOverDraws:
                    return 1 + (int)(random.NextDouble() * 8);
                default:
                    // 값 범위를 정하지 않은 효과를 조용히 다른 효과의 값으로 만들지 않음
                    throw new ArgumentOutOfRangeException(nameof(type), type, "CreateValue 에 값 범위가 없는 효과 (CheckedTypes 주석 참고)");
            }
        }

        // 엔진과 따로 쓴 필터 판정. 등급 필터 값은 Any 다음이 N, R, SR 순서이고 태그 필터 값은 Any 다음이 전자, 기계, 동력 순서임 (PartTag 는 None 다음)
        private static double SumValues(List<ModifierEffect[]> combination, ModifierType type, Grade? grade, PartTag? tag)
        {
            double sum = 0.0;
            foreach (ModifierEffect[] effects in combination)
            {
                foreach (ModifierEffect effect in effects)
                {
                    if (effect.Type != type)
                    {
                        continue;
                    }

                    bool gradeMatches = grade == null || effect.Target == GradeFilter.Any || (int)effect.Target == (int)grade.Value + 1;
                    bool tagMatches = tag == null || effect.TagTarget == PartTagFilter.Any || (int)effect.TagTarget == (int)tag.Value;
                    if (gradeMatches && tagMatches)
                    {
                        sum += effect.Value;
                    }
                }
            }

            return sum;
        }

        private static int RoundHalfAway(double value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private static void AssertValidTable(double n, double r, double sr, uint seed, List<ModifierEffect[]> combination)
        {
            double[] values = { n, r, sr };
            string[] names = { "N", "R", "SR" };
            for (int i = 0; i < values.Length; i++)
            {
                if (double.IsNaN(values[i]) || double.IsInfinity(values[i]) || values[i] < 0.0 || values[i] > 1.0)
                {
                    Assert.Fail(names[i] + " 확률이 범위 밖: " + Format(values[i]) + " | " + Describe(seed, combination));
                }
            }

            double sum = n + r + sr;
            if (!(Math.Abs(sum - 1.0) <= Tolerance))
            {
                Assert.Fail("확률 합 " + Format(sum) + " (N " + Format(n) + " · R " + Format(r) + " · SR " + Format(sr) + ") | "
                    + Describe(seed, combination));
            }
        }

        private static void AssertClose(string label, double expected, double actual, uint seed, List<ModifierEffect[]> combination)
        {
            if (!(Math.Abs(expected - actual) <= Tolerance))
            {
                Assert.Fail(label + " 기대 " + Format(expected) + " 실제 " + Format(actual) + " | " + Describe(seed, combination));
            }
        }

        private static void AssertOutput(string label, int expected, int actual, uint seed, List<ModifierEffect[]> combination)
        {
            if (expected != actual)
            {
                Assert.Fail(label + " 기대 " + expected + " 실제 " + actual + " | " + Describe(seed, combination));
            }
        }

        private static void AssertGachaAccepts(double n, double r, double sr, uint seed, List<ModifierEffect[]> combination)
        {
            try
            {
                new GachaEngine(new RandomService(1), 50, n, r, sr);
            }
            catch (ArgumentException exception)
            {
                Assert.Fail("GachaEngine 이 확률표를 거부: " + exception.Message + " | " + Describe(seed, combination));
            }
        }

        private static string Describe(uint seed, List<ModifierEffect[]> combination)
        {
            var builder = new StringBuilder();
            builder.Append("seed=").Append(seed).Append(" 모듈 ").Append(combination.Count).Append("개");
            for (int i = 0; i < combination.Count; i++)
            {
                builder.Append(" [");
                for (int j = 0; j < combination[i].Length; j++)
                {
                    ModifierEffect effect = combination[i][j];
                    if (j > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(effect.Type).Append('/').Append(effect.Target).Append('/').Append(effect.TagTarget).Append('/')
                        .Append(effect.Value.ToString("R", CultureInfo.InvariantCulture));
                }

                builder.Append(']');
            }

            return builder.ToString();
        }

        private static string Format(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
