using System;

namespace Overclock.Core
{
    public sealed class RunManager
    {
        private const double BaseQuota = 8.0;
        private const double QuotaGrowth = 1.45;

        private readonly GachaEngine gachaEngine;
        private readonly PartCatalog partCatalog;
        private readonly int roundCount;
        private readonly int drawsPerRound;

        private int currentRound;
        private int production;
        private int drawsRemaining;
        private RunState state;

        public RunManager(GachaEngine gachaEngine, PartCatalog partCatalog, int roundCount, int drawsPerRound)
        {
            if (gachaEngine == null)
            {
                throw new ArgumentNullException(nameof(gachaEngine));
            }

            if (partCatalog == null)
            {
                throw new ArgumentNullException(nameof(partCatalog));
            }

            if (roundCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(roundCount));
            }

            if (drawsPerRound < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(drawsPerRound));
            }

            this.gachaEngine = gachaEngine;
            this.partCatalog = partCatalog;
            this.roundCount = roundCount;
            this.drawsPerRound = drawsPerRound;
            StartRound(1);
        }

        public int RoundCount => roundCount;
        public int CurrentRound => currentRound;
        public int Quota => QuotaFor(currentRound);
        public int Production => production;
        public int DrawsRemaining => drawsRemaining;
        public RunState State => state;

        public static int QuotaFor(int round)
        {
            if (round < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(round));
            }

            return (int)Math.Round(BaseQuota * Math.Pow(QuotaGrowth, round - 1), MidpointRounding.AwayFromZero);
        }

        public PartData Draw()
        {
            if (state != RunState.Drawing)
            {
                throw new InvalidOperationException("뽑기 중이 아닙니다: " + state);
            }

            Grade grade = gachaEngine.Draw();
            PartData part = partCatalog.Pick(grade);
            production += part.BaseOutput;
            drawsRemaining--;

            // 돌파 판정이 소진 판정보다 먼저. 마지막 뽑기로 할당량에 도달하면 돌파
            if (production >= Quota)
            {
                state = currentRound == roundCount ? RunState.RunCleared : RunState.RoundCleared;
            }
            else if (drawsRemaining == 0)
            {
                state = RunState.RunFailed;
            }

            return part;
        }

        public void AdvanceRound()
        {
            if (state != RunState.RoundCleared)
            {
                throw new InvalidOperationException("돌파 상태가 아닙니다: " + state);
            }

            StartRound(currentRound + 1);
        }

        private void StartRound(int round)
        {
            currentRound = round;
            production = 0;
            drawsRemaining = drawsPerRound;
            state = RunState.Drawing;
        }
    }
}
