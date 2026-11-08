using System;
using System.Collections.Generic;

namespace Overclock.Core
{
    // 하네스가 오버클럭 · 다시 받기를 쓰는 규칙 (11/08 사전고정 v2). None = 지금 하네스와 같음 (둘 다 안 씀).
    // 최적 오버클럭은 표를 모아 두므로 같은 부품 목록의 여러 판에 한 인스턴스를 쓴다
    public sealed class SimulationPlayPolicy
    {
        public const double DefaultRerollGainThreshold = 8.0;

        public static readonly SimulationPlayPolicy None = new SimulationPlayPolicy("없음", null, false, 0.0);

        private readonly OverclockPlanner planner;

        // overclockParts 가 null 이 아니면 그 부품 목록으로 최적 오버클럭을 쓴다
        public SimulationPlayPolicy(string name, IReadOnlyList<PartData> overclockParts, bool useReroll, double rerollGainThreshold)
        {
            Name = name;
            planner = overclockParts == null ? null : new OverclockPlanner(overclockParts);
            UseReroll = useReroll;
            RerollGainThreshold = rerollGainThreshold;
        }

        public static SimulationPlayPolicy OptimalOverclockAndReroll(IReadOnlyList<PartData> parts)
        {
            if (parts == null)
            {
                throw new ArgumentNullException(nameof(parts));
            }

            return new SimulationPlayPolicy("최적 오버클럭 + 다시 받기", parts, true, DefaultRerollGainThreshold);
        }

        public string Name { get; }
        public bool UseOverclock => planner != null;
        public bool UseReroll { get; }

        // 가장 좋은 카드의 기대 이득 (다음 라운드 기대 생산량 후 − 전) 이 이 값보다 작으면 다시 받는다
        public double RerollGainThreshold { get; }

        internal int OverclockTablesBuilt => planner == null ? 0 : planner.TablesBuilt;

        // 이번 라운드 통과 확률이 켰을 때 더 높으면 켠다 (OverclockPlanner)
        internal bool ShouldActivateOverclock(RunManager runManager, GachaEngine gachaEngine)
        {
            return planner != null && planner.ShouldActivate(runManager, gachaEngine);
        }

        internal bool ShouldReroll(RunSession session, IReadOnlyList<ModuleData> offer, IReadOnlyList<PartData> parts)
        {
            if (!UseReroll || !session.CanRerollOffer)
            {
                return false;
            }

            return BestGain(offer, session.RunManager, parts) < RerollGainThreshold;
        }

        internal static double BestGain(IReadOnlyList<ModuleData> offer, RunManager runManager, IReadOnlyList<PartData> parts)
        {
            double best = double.MinValue;
            foreach (ModuleData module in offer)
            {
                ModulePreview preview = runManager.PreviewModule(module);
                best = Math.Max(best, preview.ExpectedRoundOutputAfter(parts) - preview.ExpectedRoundOutputBefore(parts));
            }

            return best;
        }
    }
}
