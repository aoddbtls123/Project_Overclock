using System;
using System.Collections.Generic;

namespace Overclock.Core
{
    internal static class RoundOutputEstimator
    {
        private const int GradeCount = 3;
        private const int StreakLength = 3;

        // 주어진 횟수를 전부 뽑았을 때의 기대 생산량. 할당량 도달 조기 종료와 보증 확정 SR 은 넣지 않는다
        public static double Estimate(ModifierEngine engine, IReadOnlyList<PartData> parts, int draws)
        {
            if (parts == null)
            {
                throw new ArgumentNullException(nameof(parts));
            }

            engine.GetProbabilities(out double n, out double r, out double sr);
            double[] probabilities = { n, r, sr };

            var plainOutput = new double[GradeCount];
            var streakOutput = new double[GradeCount];
            for (int grade = 0; grade < GradeCount; grade++)
            {
                plainOutput[grade] = AverageOutput(engine, parts, (Grade)grade, false);
                streakOutput[grade] = AverageOutput(engine, parts, (Grade)grade, true);
            }

            // 연속 상태별 확률: fresh = 이어지는 연속이 없음 (라운드 시작 · 발동 직후),
            // streak[등급, 개수] = 그 등급이 개수만큼 이어짐 (1 ~ StreakLength-1)
            double fresh = 1.0;
            var streak = new double[GradeCount, StreakLength];
            double expected = 0.0;

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
        private static double AverageOutput(ModifierEngine engine, IReadOnlyList<PartData> parts, Grade grade, bool streakTriggered)
        {
            double sum = 0.0;
            int count = 0;
            foreach (PartData part in parts)
            {
                if (part.Grade == grade)
                {
                    sum += engine.CalculateOutput(grade, part.BaseOutput, streakTriggered);
                    count++;
                }
            }

            return count == 0 ? 0.0 : sum / count;
        }
    }
}
