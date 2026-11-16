using System;
using System.Collections.Generic;

namespace Overclock.Core
{
    public sealed class SimulationSummary
    {
        private readonly IReadOnlyList<RunRecord> records;
        private readonly int[] sortedFinalRounds;

        public SimulationSummary(IReadOnlyList<RunRecord> records)
        {
            if (records == null)
            {
                throw new ArgumentNullException(nameof(records));
            }

            if (records.Count == 0)
            {
                throw new ArgumentException("런 기록이 없습니다.", nameof(records));
            }

            this.records = records;
            sortedFinalRounds = new int[records.Count];
            for (int i = 0; i < records.Count; i++)
            {
                sortedFinalRounds[i] = records[i].FinalRound;
            }

            Array.Sort(sortedFinalRounds);
        }

        public int RunCount => records.Count;

        public int ReachedCount(int round)
        {
            int count = 0;
            foreach (RunRecord record in records)
            {
                if (record.FinalRound >= round)
                {
                    count++;
                }
            }

            return count;
        }

        // 그 라운드를 넘긴 런. 마지막 라운드는 클리어한 런만 넘긴 것으로 셈
        public int PassedCount(int round)
        {
            int count = 0;
            foreach (RunRecord record in records)
            {
                if (record.FinalRound > round || (record.FinalRound == round && record.Cleared))
                {
                    count++;
                }
            }

            return count;
        }

        // 그 라운드에 도달한 런 중 통과한 비율. 도달한 런이 없으면 NaN
        public double PassRate(int round)
        {
            int reached = ReachedCount(round);
            return reached == 0 ? double.NaN : (double)PassedCount(round) / reached;
        }

        public int EndedAtCount(int round, bool cleared)
        {
            int count = 0;
            foreach (RunRecord record in records)
            {
                if (record.FinalRound == round && record.Cleared == cleared)
                {
                    count++;
                }
            }

            return count;
        }

        public double ClearRate
        {
            get
            {
                int cleared = 0;
                foreach (RunRecord record in records)
                {
                    if (record.Cleared)
                    {
                        cleared++;
                    }
                }

                return (double)cleared / records.Count;
            }
        }

        public double MeanFinalRound => Mean(0, sortedFinalRounds.Length);

        public double MedianFinalRound
        {
            get
            {
                int middle = sortedFinalRounds.Length / 2;
                if (sortedFinalRounds.Length % 2 == 1)
                {
                    return sortedFinalRounds[middle];
                }

                return (sortedFinalRounds[middle - 1] + sortedFinalRounds[middle]) / 2.0;
            }
        }

        // 지표 ⑤ 운의 영향력: 도달 라운드 상위 10% 평균 − 하위 10% 평균 (런이 10개 미만이면 1개씩)
        public double LuckSpread
        {
            get
            {
                int tail = Math.Max(1, sortedFinalRounds.Length / 10);
                return Mean(sortedFinalRounds.Length - tail, sortedFinalRounds.Length) - Mean(0, tail);
            }
        }

        private double Mean(int from, int to)
        {
            double sum = 0.0;
            for (int i = from; i < to; i++)
            {
                sum += sortedFinalRounds[i];
            }

            return sum / (to - from);
        }
    }
}
