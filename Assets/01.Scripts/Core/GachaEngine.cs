using System;

namespace Overclock.Core
{
    public sealed class GachaEngine
    {
        private const double SumTolerance = 1e-6;

        private readonly RandomService randomService;
        private readonly int pityThreshold;

        private double probabilityN;
        private double probabilityR;
        private double probabilitySR;
        private int drawsWithoutSuperRare;

        public GachaEngine(RandomService randomService, int pityThreshold, double n, double r, double sr)
        {
            if (randomService == null)
            {
                throw new ArgumentNullException(nameof(randomService));
            }

            if (pityThreshold < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pityThreshold));
            }

            this.randomService = randomService;
            this.pityThreshold = pityThreshold;
            SetProbabilities(n, r, sr);
        }

        public int DrawsWithoutSuperRare => drawsWithoutSuperRare;
        public int PityThreshold => pityThreshold;

        public double GetProbability(Grade grade)
        {
            switch (grade)
            {
                case Grade.N:
                    return probabilityN;
                case Grade.R:
                    return probabilityR;
                case Grade.SR:
                    return probabilitySR;
                default:
                    throw new ArgumentOutOfRangeException(nameof(grade));
            }
        }

        public void SetProbabilities(double n, double r, double sr)
        {
            if (n < 0 || r < 0 || sr < 0)
            {
                throw new ArgumentException("확률은 음수일 수 없습니다.");
            }

            double sum = n + r + sr;
            if (double.IsNaN(sum) || Math.Abs(sum - 1.0) > SumTolerance)
            {
                throw new ArgumentException("확률 합이 1이 아닙니다: " + sum);
            }

            probabilityN = n;
            probabilityR = r;
            probabilitySR = sr;
        }

        internal void RestorePityCounter(int drawsWithoutSuperRare)
        {
            // 보증 횟수와 같은 값은 가능하다. 다음 뽑기가 SR 확정인 상태다
            if (drawsWithoutSuperRare < 0 || drawsWithoutSuperRare > pityThreshold)
            {
                throw new ArgumentOutOfRangeException(nameof(drawsWithoutSuperRare));
            }

            this.drawsWithoutSuperRare = drawsWithoutSuperRare;
        }

        public Grade Draw()
        {
            Grade grade = drawsWithoutSuperRare >= pityThreshold
                ? Grade.SR
                : Resolve(randomService.NextDouble());

            drawsWithoutSuperRare = grade == Grade.SR ? 0 : drawsWithoutSuperRare + 1;
            return grade;
        }

        public Grade Resolve(double roll)
        {
            double cumulative = probabilityN;
            if (roll < cumulative)
            {
                return Grade.N;
            }

            cumulative += probabilityR;
            if (roll < cumulative)
            {
                return Grade.R;
            }

            return Grade.SR;
        }
    }
}
