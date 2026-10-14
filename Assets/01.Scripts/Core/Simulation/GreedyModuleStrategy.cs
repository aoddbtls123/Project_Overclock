using System;
using System.Collections.Generic;

namespace Overclock.Core
{
    public sealed class GreedyModuleStrategy : IModuleStrategy
    {
        private const int GradeCount = 3;
        private const int StreakLength = 3;

        private readonly IReadOnlyList<PartData> parts;

        public GreedyModuleStrategy(IReadOnlyList<PartData> parts)
        {
            if (parts == null)
            {
                throw new ArgumentNullException(nameof(parts));
            }

            this.parts = parts;
        }

        public string Name => "기대 생산량 최대";

        public ModuleData Choose(IReadOnlyList<ModuleData> offer, RunManager runManager, RandomService choiceRandom)
        {
            ModuleData best = null;
            double bestValue = double.MinValue;
            foreach (ModuleData module in offer)
            {
                double value = ExpectedRoundOutput(runManager.PreviewModule(module), parts);
                if (value > bestValue)
                {
                    best = module;
                    bestValue = value;
                }
            }

            return best;
        }

        // 장착 후 한 라운드의 기대 생산량. 보증 확정 SR 은 넣지 않는다
        internal static double ExpectedRoundOutput(ModulePreview preview, IReadOnlyList<PartData> parts)
        {
            preview.GetProbabilitiesAfter(out double n, out double r, out double sr);
            double[] probabilities = { n, r, sr };

            var plainOutput = new double[GradeCount];
            var streakOutput = new double[GradeCount];
            for (int grade = 0; grade < GradeCount; grade++)
            {
                plainOutput[grade] = AverageOutput(preview, parts, (Grade)grade, false);
                streakOutput[grade] = AverageOutput(preview, parts, (Grade)grade, true);
            }

            // 연속 상태별 확률: fresh = 이어지는 연속이 없음 (라운드 시작 · 발동 직후),
            // streak[등급, 개수] = 그 등급이 개수만큼 이어짐 (1 ~ StreakLength-1)
            double fresh = 1.0;
            var streak = new double[GradeCount, StreakLength];
            double expected = 0.0;

            int draws = preview.DrawsPerRoundAfter;
            for (int draw = 0; draw < draws; draw++)
            {
                double nextFresh = 0.0;
                var nextStreak = new double[GradeCount, StreakLength];

                for (int grade = 0; grade < GradeCount; grade++)
                {
                    double p = probabilities[grade];

                    double startMass = fresh * p;
                    nextStreak[grade, 1] += startMass;
                    expected += startMass * plainOutput[grade];

                    for (int previous = 0; previous < GradeCount; previous++)
                    {
                        for (int count = 1; count < StreakLength; count++)
                        {
                            double mass = streak[previous, count] * p;
                            if (mass == 0.0)
                            {
                                continue;
                            }

                            if (previous != grade)
                            {
                                nextStreak[grade, 1] += mass;
                                expected += mass * plainOutput[grade];
                            }
                            else if (count + 1 == StreakLength)
                            {
                                nextFresh += mass;
                                expected += mass * streakOutput[grade];
                            }
                            else
                            {
                                nextStreak[grade, count + 1] += mass;
                                expected += mass * plainOutput[grade];
                            }
                        }
                    }
                }

                fresh = nextFresh;
                streak = nextStreak;
            }

            return expected;
        }

        // 부품은 등급 안에서 균등하게 뽑히므로 부품별 생산량의 평균이 그 등급의 기대 생산량이다
        private static double AverageOutput(ModulePreview preview, IReadOnlyList<PartData> parts, Grade grade, bool streakTriggered)
        {
            double sum = 0.0;
            int count = 0;
            foreach (PartData part in parts)
            {
                if (part.Grade == grade)
                {
                    sum += preview.OutputAfter(grade, part.BaseOutput, streakTriggered);
                    count++;
                }
            }

            return count == 0 ? 0.0 : sum / count;
        }
    }
}
