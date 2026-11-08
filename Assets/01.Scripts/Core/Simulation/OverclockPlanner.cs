using System;
using System.Collections.Generic;

namespace Overclock.Core
{
    // 하네스 전용 최적 오버클럭 (11/03 A2 측정의 전수 DP · 11/08 사전고정 v2). 표는 라운드당 사용 횟수를 일반화해 두었고 지금 게임은 1회.
    // 이번 라운드 통과 확률이 켰을 때 더 높으면 켠다. 상태 = 남은 뽑기 · 천장 카운터 · 연속 · 오버클럭 (남은 사용 횟수, 걸린 횟수) · 남은 할당량
    internal sealed class OverclockPlanner
    {
        private const int StreakStates = 7;
        private const int MaxCachedTables = 256;
        private const int UsesPerRound = 1;

        private readonly IReadOnlyList<PartData> parts;
        private readonly Dictionary<string, Table> cache = new Dictionary<string, Table>();

        public OverclockPlanner(IReadOnlyList<PartData> parts)
        {
            this.parts = parts ?? throw new ArgumentNullException(nameof(parts));
        }

        public int TablesBuilt { get; private set; }

        public bool ShouldActivate(RunManager runManager, GachaEngine gachaEngine)
        {
            if (!runManager.CanActivateOverclock)
            {
                return false;
            }

            return PassChance(runManager, gachaEngine, true) > PassChance(runManager, gachaEngine, false) + 1e-12;
        }

        // 이번 라운드 통과 확률 (지금 켬 / 이번 뽑기는 안 켬 · 뒤에는 최적). 뽑기 중일 때만
        internal double PassChance(RunManager runManager, GachaEngine gachaEngine, bool activateNow)
        {
            if (runManager.State != RunState.Drawing || (activateNow && !runManager.CanActivateOverclock))
            {
                throw new InvalidOperationException("지금은 계산할 수 없습니다: " + runManager.State);
            }

            Table table = TableFor(runManager, gachaEngine);
            int d = runManager.DrawsRemaining;
            int p = gachaEngine.DrawsWithoutSuperRare;
            int s = StreakIndex(runManager.StreakGrade, runManager.StreakCount);
            int need = runManager.Quota - runManager.Production;
            int left = UsesPerRound - runManager.OverclockUseCount;
            if (runManager.OverclockDrawsLeft > 0)
            {
                // 걸려 있는 중: 지금 상태 그대로
                return table.Value(d, p, s, table.Active(left, runManager.OverclockDrawsLeft), need);
            }

            return activateNow
                ? table.Value(d, p, s, table.Active(left - 1, Math.Min(RunManager.OverclockDraws, d)), need)
                : table.DrawValue(d, p, s, false, table.Idle(left), need);
        }

        private Table TableFor(RunManager runManager, GachaEngine gachaEngine)
        {
            var ids = new List<string>();
            foreach (ModuleData module in runManager.EquippedModules)
            {
                ids.Add(module.Id);
            }

            ids.Sort(StringComparer.Ordinal);
            int uses = UsesPerRound;
            int maxDraws = runManager.DrawsRemaining;
            string key = string.Join(",", ids) + "|" + runManager.Quota + "|" + uses + "|" + maxDraws + "|" + runManager.OverclockMultiplierBonus;
            if (cache.TryGetValue(key, out Table table))
            {
                return table;
            }

            if (cache.Count >= MaxCachedTables)
            {
                cache.Clear();
            }

            table = new Table(runManager.Quota, uses, maxDraws, gachaEngine.PityThreshold);
            for (int g = 0; g < 3; g++)
            {
                table.Probability[g] = gachaEngine.GetProbability((Grade)g);
                table.Outputs[g] = new List<int>();
            }

            foreach (PartData part in parts)
            {
                List<int> outputs = table.Outputs[(int)part.Grade];
                outputs.Add(runManager.OutputFor(part, false, false));
                outputs.Add(runManager.OutputFor(part, true, false));
                outputs.Add(runManager.OutputFor(part, false, true));
                outputs.Add(runManager.OutputFor(part, true, true));
            }

            table.Compress();
            TablesBuilt++;
            cache[key] = table;
            return table;
        }

        // 연속 상태: 0 = 없음 · 1 + 등급 × 2 + (개수 − 1). RunManager.AdvanceStreak 와 같은 규칙 (3 개째에 터지고 0)
        internal static int StreakIndex(Grade grade, int count)
        {
            return count == 0 ? 0 : 1 + (int)grade * 2 + (count - 1);
        }

        internal static int AdvanceStreak(int state, int grade, out bool triggered)
        {
            int streakGrade = 0;
            int count = 0;
            if (state > 0)
            {
                streakGrade = (state - 1) / 2;
                count = (state - 1) % 2 + 1;
            }

            if (count > 0 && streakGrade == grade)
            {
                count++;
            }
            else
            {
                streakGrade = grade;
                count = 1;
            }

            triggered = count == 3;
            return triggered ? 0 : 1 + grade * 2 + (count - 1);
        }

        private sealed class Table
        {
            public readonly double[] Probability = new double[3];

            // 등급마다 부품별 (평소, 평소 연속, 오버, 오버 연속) 4칸씩
            public readonly List<int>[] Outputs = new List<int>[3];

            // 같은 생산량 묶음 (평소, 평소 연속, 오버, 오버 연속) 과 그 확률 — 같은 값을 내는 부품을 합쳐 계산을 줄인다
            private int[][] groupOutputs;
            private double[][] groupWeights;

            private readonly int quota;
            private readonly int uses;
            private readonly int maxDraws;
            private readonly int pity;
            private readonly int ocStates;
            private readonly double[][] values;

            public Table(int quota, int uses, int maxDraws, int pity)
            {
                this.quota = quota;
                this.uses = uses;
                this.maxDraws = maxDraws;
                this.pity = pity;
                // 쉬는 상태 (남은 횟수 0..uses) + 걸린 상태 (켠 뒤 남은 횟수 0..uses−1 × 걸린 뽑기 1..3)
                ocStates = uses + 1 + uses * RunManager.OverclockDraws;
                values = new double[maxDraws + 1][];
            }

            public int Idle(int left)
            {
                return left;
            }

            public int Active(int leftAfter, int drawsLeft)
            {
                return uses + 1 + leftAfter * RunManager.OverclockDraws + (drawsLeft - 1);
            }

            private bool IsActive(int state, out int leftAfter, out int drawsLeft)
            {
                if (state <= uses)
                {
                    leftAfter = state;
                    drawsLeft = 0;
                    return false;
                }

                int k = state - uses - 1;
                leftAfter = k / RunManager.OverclockDraws;
                drawsLeft = k % RunManager.OverclockDraws + 1;
                return true;
            }

            // 천장까지 d 보다 먼 카운터는 결과가 같다 → 하나로 묶는다
            private int Index(int d, int p, int s, int state, int need)
            {
                int low = pity - d;
                int pi = Math.Max(p, low) - low;
                return ((pi * StreakStates + s) * ocStates + state) * (quota + 1) + need;
            }

            // 필요한 칸만 그때 계산한다 (천장 카운터는 대부분 한두 값만 닿는다). 칸 값 = 확률 + 1 (0 = 아직 계산 안 함)
            public double Value(int d, int p, int s, int state, int need)
            {
                if (need <= 0)
                {
                    return 1.0;
                }

                if (d <= 0)
                {
                    return 0.0;
                }

                double[] row = values[d];
                if (row == null)
                {
                    row = new double[(d + 1) * StreakStates * ocStates * (quota + 1)];
                    values[d] = row;
                }

                int index = Index(d, p, s, state, Math.Min(need, quota));
                double stored = row[index];
                if (stored != 0.0)
                {
                    return stored - 1.0;
                }

                double value;
                if (IsActive(state, out int leftAfter, out int drawsLeft))
                {
                    int next = drawsLeft == 1 ? Idle(leftAfter) : Active(leftAfter, drawsLeft - 1);
                    value = DrawValue(d, p, s, true, next, need);
                }
                else
                {
                    value = DrawValue(d, p, s, false, Idle(leftAfter), need);
                    if (leftAfter > 0)
                    {
                        value = Math.Max(value, Value(d, p, s, Active(leftAfter - 1, Math.Min(RunManager.OverclockDraws, d)), need));
                    }
                }

                row[index] = value + 1.0;
                return value;
            }

            public void Compress()
            {
                groupOutputs = new int[3][];
                groupWeights = new double[3][];
                for (int g = 0; g < 3; g++)
                {
                    var keys = new List<long>();
                    var counts = new List<int>();
                    List<int> outputs = Outputs[g];
                    int parts = outputs.Count / 4;
                    for (int k = 0; k < parts; k++)
                    {
                        long key = 0;
                        for (int j = 0; j < 4; j++)
                        {
                            key = key * 100003 + outputs[k * 4 + j];
                        }

                        int at = keys.IndexOf(key);
                        if (at < 0)
                        {
                            keys.Add(key);
                            counts.Add(1);
                        }
                        else
                        {
                            counts[at]++;
                        }
                    }

                    groupOutputs[g] = new int[keys.Count * 4];
                    groupWeights[g] = new double[keys.Count];
                    int filled = 0;
                    for (int k = 0; k < parts; k++)
                    {
                        long key = 0;
                        for (int j = 0; j < 4; j++)
                        {
                            key = key * 100003 + outputs[k * 4 + j];
                        }

                        int at = keys.IndexOf(key);
                        if (groupWeights[g][at] == 0.0)
                        {
                            for (int j = 0; j < 4; j++)
                            {
                                groupOutputs[g][at * 4 + j] = outputs[k * 4 + j];
                            }

                            filled++;
                        }

                        groupWeights[g][at] = (double)counts[at] / parts;
                    }
                }
            }

            public double DrawValue(int d, int p, int s, bool active, int nextState, int need)
            {
                bool forced = p >= pity;
                double sum = 0.0;
                int column = (active ? 2 : 0);
                for (int g = 0; g < 3; g++)
                {
                    double pg = forced ? (g == 2 ? 1.0 : 0.0) : Probability[g];
                    if (pg <= 0.0)
                    {
                        continue;
                    }

                    int s2 = AdvanceStreak(s, g, out bool triggered);
                    int p2 = g == 2 ? 0 : p + 1;
                    int[] outputs = groupOutputs[g];
                    double[] weights = groupWeights[g];
                    int offset = column + (triggered ? 1 : 0);
                    for (int k = 0; k < weights.Length; k++)
                    {
                        sum += pg * weights[k] * Value(d - 1, p2, s2, nextState, need - outputs[k * 4 + offset]);
                    }
                }

                return sum;
            }
        }
    }
}
