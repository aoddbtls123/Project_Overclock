using System.Collections.Generic;

namespace Overclock.Core
{
    public sealed class ModulePreview
    {
        private readonly ModifierEngine before;
        private readonly ModifierEngine after;
        private readonly int baseDrawsPerRound;
        private readonly int remainingDraws;

        internal ModulePreview(ModifierEngine before, ModifierEngine after, int baseDrawsPerRound, int remainingDraws)
        {
            this.before = before;
            this.after = after;
            this.baseDrawsPerRound = baseDrawsPerRound;
            this.remainingDraws = remainingDraws;
        }

        // 다음 라운드 뽑기 횟수. 미리보기는 돌파 시점에 쓰므로 지금 남은 뽑기가 이월 대상이다
        public int DrawsPerRoundBefore => before.DrawsForNextRound(baseDrawsPerRound, remainingDraws);
        public int DrawsPerRoundAfter => after.DrawsForNextRound(baseDrawsPerRound, remainingDraws);

        // 이월 효과로 늘어나는 횟수 (20회 상한을 넘는 몫은 빠진 값)
        public int CarriedDrawsAfter => DrawsPerRoundAfter - after.DrawsPerRound(baseDrawsPerRound);

        public void GetProbabilitiesBefore(out double n, out double r, out double sr)
        {
            before.GetProbabilities(out n, out r, out sr);
        }

        public void GetProbabilitiesAfter(out double n, out double r, out double sr)
        {
            after.GetProbabilities(out n, out r, out sr);
        }

        public int OutputBefore(PartData part, bool streakTriggered)
        {
            return before.CalculateOutput(part, streakTriggered);
        }

        public int OutputAfter(PartData part, bool streakTriggered)
        {
            return after.CalculateOutput(part, streakTriggered);
        }

        public double ExpectedRoundOutputBefore(IReadOnlyList<PartData> parts)
        {
            return RoundOutputEstimator.Estimate(before, parts, DrawsPerRoundBefore);
        }

        public double ExpectedRoundOutputAfter(IReadOnlyList<PartData> parts)
        {
            return RoundOutputEstimator.Estimate(after, parts, DrawsPerRoundAfter);
        }
    }
}
