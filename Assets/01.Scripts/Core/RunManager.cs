using System;

namespace Overclock.Core
{
    public sealed class RunManager
    {
        private const double BaseQuota = 8.0;
        private const double QuotaGrowth = 1.45;
        private const int StreakLength = 3;

        private readonly GachaEngine gachaEngine;
        private readonly PartCatalog partCatalog;
        private readonly int roundCount;
        private readonly int drawsPerRound;
        private readonly ModifierEngine modifierEngine;

        private int currentRound;
        private int production;
        private int drawsRemaining;
        private RunState state;
        private Grade streakGrade;
        private int streakCount;

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
            modifierEngine = new ModifierEngine(
                gachaEngine.GetProbability(Grade.N),
                gachaEngine.GetProbability(Grade.R),
                gachaEngine.GetProbability(Grade.SR));
            StartRound(1, 0);
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

        public DrawResult Draw()
        {
            if (state != RunState.Drawing)
            {
                throw new InvalidOperationException("뽑기 중이 아닙니다: " + state);
            }

            Grade grade = gachaEngine.Draw();
            PartData part = partCatalog.Pick(grade);
            bool streakTriggered = AdvanceStreak(grade);
            int output = modifierEngine.CalculateOutput(part, streakTriggered);
            production += output;
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

            return new DrawResult(part, output, streakTriggered);
        }

        public void EquipModule(ModuleData module)
        {
            if (state != RunState.Drawing && state != RunState.RoundCleared)
            {
                throw new InvalidOperationException("런이 끝났습니다: " + state);
            }

            modifierEngine.Equip(module);

            // GachaEngine 은 새로 만들지 않는다. 보증 카운터가 런 전체에서 이어져야 한다
            modifierEngine.GetProbabilities(out double n, out double r, out double sr);
            gachaEngine.SetProbabilities(n, r, sr);
        }

        public ModulePreview PreviewModule(ModuleData module)
        {
            // 실제 엔진은 건드리지 않는다. 전/후 모두 복제본으로 계산
            ModifierEngine after = modifierEngine.Clone();
            after.Equip(module);
            return new ModulePreview(modifierEngine.Clone(), after, drawsPerRound, drawsRemaining);
        }

        public void AdvanceRound()
        {
            if (state != RunState.RoundCleared)
            {
                throw new InvalidOperationException("돌파 상태가 아닙니다: " + state);
            }

            // 남은 뽑기는 새 라운드 횟수로 덮어쓰기 전에 읽어야 이월된다
            StartRound(currentRound + 1, drawsRemaining);
        }

        private void StartRound(int round, int carriedOverDraws)
        {
            currentRound = round;
            production = 0;
            drawsRemaining = modifierEngine.DrawsForNextRound(drawsPerRound, carriedOverDraws);
            streakCount = 0;
            state = RunState.Drawing;
        }

        private bool AdvanceStreak(Grade grade)
        {
            if (streakCount > 0 && grade == streakGrade)
            {
                streakCount++;
            }
            else
            {
                streakGrade = grade;
                streakCount = 1;
            }

            if (streakCount == StreakLength)
            {
                streakCount = 0;
                return true;
            }

            return false;
        }
    }
}
