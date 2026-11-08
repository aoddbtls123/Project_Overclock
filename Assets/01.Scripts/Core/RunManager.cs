using System;
using System.Collections.Generic;

namespace Overclock.Core
{
    public sealed class RunManager
    {
        private const double BaseQuota = 8.0;
        private const double QuotaGrowth = 1.45;
        private const int StreakLength = 3;
        public const int OverclockDraws = 3;

        private readonly GachaEngine gachaEngine;
        private readonly PartCatalog partCatalog;
        private readonly int roundCount;
        private readonly int drawsPerRound;
        private readonly double baseN;
        private readonly double baseR;
        private readonly double baseSR;
        private ModifierEngine modifierEngine;
        private readonly List<ModuleData> equippedModules = new List<ModuleData>();
        private readonly List<PartData> roundParts = new List<PartData>(ModifierEngine.MaxDrawsPerRound);

        private int currentRound;
        private int production;
        private int drawsRemaining;
        private RunState state;
        private Grade streakGrade;
        private int streakCount;
        private int overclockUseCount;
        private double overclockMultiplierBonus = ModifierEngine.OverclockMultiplierBonus;
        private bool roundPartsComplete = true;
        private int overclockDrawsLeft;

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
            // 모듈 분해 때 처음부터 다시 계산하려면 모듈 없는 기본표가 필요하다 (GachaEngine 의 값은 장착하면 바뀐다)
            baseN = gachaEngine.GetProbability(Grade.N);
            baseR = gachaEngine.GetProbability(Grade.R);
            baseSR = gachaEngine.GetProbability(Grade.SR);
            modifierEngine = new ModifierEngine(baseN, baseR, baseSR);
            StartRound(1, 0);
        }

        public int RoundCount => roundCount;
        public int CurrentRound => currentRound;
        public int Quota => QuotaFor(currentRound);
        public int Production => production;
        public int DrawsRemaining => drawsRemaining;
        public RunState State => state;
        public IReadOnlyList<ModuleData> EquippedModules => equippedModules;

        /// <summary>이번 라운드에 뽑은 부품 (뽑은 순서). 새 라운드가 시작되면 비운다. 화면의 수확물 더미가 쓴다.</summary>
        public IReadOnlyList<PartData> RoundParts => roundParts;

        /// <summary>
        /// RoundParts 가 이번 라운드 처음부터 빠짐없는가. 부품 목록이 없는 옛 세이브로 이어 한 라운드는 false — 앞 부품을 모르므로 저장하지 않는다 (다음 라운드부터 true).
        /// </summary>
        public bool RoundPartsComplete => roundPartsComplete;
        internal Grade StreakGrade => streakGrade;
        internal int StreakCount => streakCount;

        /// <summary>이번 라운드에 오버클럭을 한 번이라도 켰는가. 라운드가 바뀌면 다시 false.</summary>
        public bool OverclockUsed => overclockUseCount > 0;

        /// <summary>이번 라운드에 오버클럭을 켠 횟수. 라운드가 바뀌면 0.</summary>
        public int OverclockUseCount => overclockUseCount;

        /// <summary>오버클럭 중 R · SR 배수 증가분 (기본 +1 · 과급기 +2).</summary>
        public double OverclockMultiplierBonus => overclockMultiplierBonus;

        /// <summary>
        /// 돌파 순간 (RoundCleared · RunCleared 가 된 뽑기) 에 초과분 (생산량 − 할당량) 을 넘긴다. 크레딧 지급은 RunSession 이 한다.
        /// </summary>
        internal Action<int> BreakthroughReached;

        /// <summary>오버클럭이 걸려 있는 남은 뽑기 수 (0 = 꺼짐).</summary>
        public int OverclockDrawsLeft => overclockDrawsLeft;

        // 걸려 있는 횟수가 끝나야 다음 오버클럭을 켤 수 있다 (겹치거나 늘리지 않음 · 11/08)
        public bool CanActivateOverclock => state == RunState.Drawing && overclockUseCount == 0;

        public static int QuotaFor(int round)
        {
            if (round < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(round));
            }

            double quota = Math.Round(BaseQuota * Math.Pow(QuotaGrowth, round - 1), MidpointRounding.AwayFromZero);

            // 무한모드에서 int 를 넘는 라운드(R54~)는 int 최댓값으로 둔다. 넘긴 채 변환하면 음수가 되어 바로 돌파한다
            return quota >= int.MaxValue ? int.MaxValue : (int)quota;
        }

        public DrawResult Draw()
        {
            if (state != RunState.Drawing)
            {
                throw new InvalidOperationException("뽑기 중이 아닙니다: " + state);
            }

            Grade grade = gachaEngine.Draw();
            PartData part = partCatalog.Pick(grade);
            roundParts.Add(part);
            bool streakTriggered = AdvanceStreak(grade);
            bool overclocked = overclockDrawsLeft > 0;
            int output = overclocked
                ? modifierEngine.CalculateOverclockedOutput(part, streakTriggered, overclockMultiplierBonus)
                : modifierEngine.CalculateOutput(part, streakTriggered);
            production += output;
            drawsRemaining--;
            if (overclocked)
            {
                overclockDrawsLeft--;
            }

            // 돌파 판정이 소진 판정보다 먼저. 마지막 뽑기로 할당량에 도달하면 돌파
            if (production >= Quota)
            {
                state = currentRound == roundCount ? RunState.RunCleared : RunState.RoundCleared;
            }
            else if (drawsRemaining == 0)
            {
                state = RunState.RunFailed;
            }

            // 돌파 · 런 끝이면 남은 오버클럭 횟수는 사라진다
            if (state != RunState.Drawing)
            {
                overclockDrawsLeft = 0;
            }

            if (state == RunState.RoundCleared || state == RunState.RunCleared)
            {
                BreakthroughReached?.Invoke(production - Quota);
            }

            return new DrawResult(part, output, streakTriggered, overclocked);
        }

        /// <summary>
        /// 오버클럭을 켠다. 라운드마다 1번 · 다음 뽑기부터 3회 (남은 뽑기가 적으면 그만큼). 난수는 쓰지 않는다.
        /// </summary>
        public void ActivateOverclock()
        {
            if (!CanActivateOverclock)
            {
                throw new InvalidOperationException("오버클럭을 켤 수 없습니다: " + state + " 사용 " + overclockUseCount + " 남은 " + overclockDrawsLeft);
            }

            overclockUseCount++;
            overclockDrawsLeft = Math.Min(OverclockDraws, drawsRemaining);
        }

        // 과급기 (상점 부품) 가 부른다. 기본값보다 낮출 수 없다
        internal void SetOverclockMultiplierBonus(double bonus)
        {
            if (bonus < ModifierEngine.OverclockMultiplierBonus)
            {
                throw new ArgumentOutOfRangeException(nameof(bonus));
            }

            overclockMultiplierBonus = bonus;
        }

        public void EquipModule(ModuleData module)
        {
            if (state != RunState.Drawing && state != RunState.RoundCleared)
            {
                throw new InvalidOperationException("런이 끝났습니다: " + state);
            }

            modifierEngine.Equip(module);
            equippedModules.Add(module);

            // GachaEngine 은 새로 만들지 않는다. 보증 카운터가 런 전체에서 이어져야 한다
            modifierEngine.GetProbabilities(out double n, out double r, out double sr);
            gachaEngine.SetProbabilities(n, r, sr);
        }

        /// <summary>
        /// 장착 모듈 하나를 뺀다 (상점 모듈 분해). 남은 모듈로 확률표를 처음부터 다시 계산한다. 뽑기 수는 다음 라운드 시작 때 반영된다.
        /// </summary>
        public ModuleData RemoveModule(int equippedIndex)
        {
            if (state != RunState.Drawing && state != RunState.RoundCleared)
            {
                throw new InvalidOperationException("런이 끝났습니다: " + state);
            }

            if (equippedIndex < 0 || equippedIndex >= equippedModules.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(equippedIndex));
            }

            ModuleData removed = equippedModules[equippedIndex];
            equippedModules.RemoveAt(equippedIndex);

            // 효과를 하나씩 빼지 않고 다시 만든다. 장착과 같은 계산 길 하나만 남기기 위해서다
            modifierEngine = new ModifierEngine(baseN, baseR, baseSR);
            foreach (ModuleData module in equippedModules)
            {
                modifierEngine.Equip(module);
            }

            modifierEngine.GetProbabilities(out double n, out double r, out double sr);
            gachaEngine.SetProbabilities(n, r, sr);
            return removed;
        }

        // 하네스 최적 오버클럭 (OverclockPlanner) 전용: 지금 장착 상태로 이 부품이 내는 생산량
        internal int OutputFor(PartData part, bool streakTriggered, bool overclocked)
        {
            return overclocked
                ? modifierEngine.CalculateOverclockedOutput(part, streakTriggered, overclockMultiplierBonus)
                : modifierEngine.CalculateOutput(part, streakTriggered);
        }

        /// <summary>장착 모듈 하나를 뺐을 때의 미리보기 (분해 전 확인 · 하네스 탐욕 구매). 실제 엔진은 건드리지 않는다.</summary>
        public ModulePreview PreviewRemoveModule(int equippedIndex)
        {
            if (equippedIndex < 0 || equippedIndex >= equippedModules.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(equippedIndex));
            }

            var after = new ModifierEngine(baseN, baseR, baseSR);
            for (int i = 0; i < equippedModules.Count; i++)
            {
                if (i != equippedIndex)
                {
                    after.Equip(equippedModules[i]);
                }
            }

            return new ModulePreview(modifierEngine.Clone(), after, drawsPerRound, drawsRemaining);
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

        // 세이브 복원 전용. 장착 모듈은 EquipModule 로 먼저 다시 단 뒤에 부른다.
        // Draw 가 만들 수 있는 상태만 받는다. 고친 세이브로 끝나지 않는 라운드를 만들지 못하게 한다
        internal void RestoreProgress(int round, int production, int drawsRemaining, RunState state, Grade streakGrade, int streakCount)
        {
            if (round < 1 || round > roundCount)
            {
                throw new ArgumentOutOfRangeException(nameof(round));
            }

            if (production < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(production));
            }

            if (drawsRemaining < 0 || drawsRemaining > ModifierEngine.MaxDrawsPerRound)
            {
                throw new ArgumentOutOfRangeException(nameof(drawsRemaining));
            }

            if (!Enum.IsDefined(typeof(RunState), state))
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }

            if (!Enum.IsDefined(typeof(Grade), streakGrade))
            {
                throw new ArgumentOutOfRangeException(nameof(streakGrade));
            }

            if (streakCount < 0 || streakCount >= StreakLength)
            {
                throw new ArgumentOutOfRangeException(nameof(streakCount));
            }

            bool reachedQuota = production >= QuotaFor(round);
            bool valid;
            switch (state)
            {
                case RunState.Drawing:
                    valid = !reachedQuota && drawsRemaining > 0;
                    break;
                case RunState.RoundCleared:
                    valid = reachedQuota && round < roundCount;
                    break;
                case RunState.RunCleared:
                    valid = reachedQuota && round == roundCount;
                    break;
                default:
                    valid = !reachedQuota && drawsRemaining == 0;
                    break;
            }

            if (!valid)
            {
                throw new ArgumentException("진행 상태가 서로 맞지 않습니다: R" + round + " " + state + " 생산량 " + production + " 남은 뽑기 " + drawsRemaining);
            }

            currentRound = round;
            this.production = production;
            this.drawsRemaining = drawsRemaining;
            this.state = state;
            this.streakGrade = streakGrade;
            this.streakCount = streakCount;
        }

        // 세이브 복원 전용. RestoreProgress 다음에 부른다. ActivateOverclock 과 Draw 가 만들 수 있는 값만 받는다
        internal void RestoreOverclock(int useCount, int drawsLeft)
        {
            if (useCount < 0 || useCount > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(useCount));
            }

            if (drawsLeft < 0 || drawsLeft > OverclockDraws)
            {
                throw new ArgumentOutOfRangeException(nameof(drawsLeft));
            }

            if (drawsLeft > 0 && (useCount == 0 || state != RunState.Drawing || drawsLeft > drawsRemaining))
            {
                throw new ArgumentException("오버클럭 상태가 진행과 맞지 않습니다: 사용 " + useCount + " 남은 " + drawsLeft + " " + state + " 남은 뽑기 " + drawsRemaining);
            }

            overclockUseCount = useCount;
            overclockDrawsLeft = drawsLeft;
        }

        // 세이브 복원 전용 (옛 세이브). 이번 라운드에 이미 뽑은 부품을 모른다 → 빈 목록 · 이번 라운드는 목록을 저장하지 않는다.
        // 모르는 앞 부품이 연속 · 천장에 들어 있어서, 뒤에 쌓인 부품만으로 검사하면 맞는 세이브를 거부하게 된다 (교차검토 #32)
        internal void MarkRoundPartsUnknown()
        {
            roundParts.Clear();
            roundPartsComplete = false;
        }

        // 세이브 복원 전용. RestoreProgress · 보증 복원 다음에 부른다. 라운드 처음부터 빠짐없는 목록만 받는다 (빈 목록 = 아직 안 뽑음).
        // Draw 가 만들 수 있는 것만 받는다: 이번 라운드 뽑기 수 상한 · 뽑은 적 없으면 생산량 0 · 뽑기 중 · 연속 상태 · 보증 카운터가 목록과 맞아야 한다
        internal void RestoreRoundParts(IReadOnlyList<PartData> drawnParts)
        {
            roundParts.Clear();
            roundPartsComplete = true;
            if (drawnParts == null || drawnParts.Count == 0)
            {
                // 뽑지 않은 라운드: 생산량 0 · 뽑기 중 · 연속 0 (라운드 시작 상태) 이어야 한다
                if (production != 0 || state != RunState.Drawing || streakCount != 0)
                {
                    throw new ArgumentException("이번 라운드 부품이 없는데 진행이 있습니다: 생산량 " + production + " " + state + " 연속 " + streakCount);
                }

                return;
            }

            if (drawnParts.Count + drawsRemaining > ModifierEngine.MaxDrawsPerRound)
            {
                throw new ArgumentException("이번 라운드 부품 수가 뽑기 상한을 넘습니다: 부품 " + drawnParts.Count + " 남은 뽑기 " + drawsRemaining);
            }

            // 연속은 라운드 시작에 0 이므로 이번 라운드 부품만으로 다시 셀 수 있다 (AdvanceStreak 와 같은 규칙)
            Grade replayGrade = Grade.N;
            int replayCount = 0;
            int sinceSuperRare = 0;
            bool sawSuperRare = false;
            foreach (PartData part in drawnParts)
            {
                if (part == null)
                {
                    throw new ArgumentException("이번 라운드 부품에 빈 칸이 있습니다.");
                }

                if (replayCount > 0 && part.Grade == replayGrade)
                {
                    replayCount++;
                }
                else
                {
                    replayGrade = part.Grade;
                    replayCount = 1;
                }

                if (replayCount == StreakLength)
                {
                    replayCount = 0;
                }

                if (part.Grade == Grade.SR)
                {
                    sawSuperRare = true;
                    sinceSuperRare = 0;
                }
                else
                {
                    sinceSuperRare++;
                }
            }

            if (replayCount != streakCount || (streakCount > 0 && replayGrade != streakGrade))
            {
                throw new ArgumentException("이번 라운드 부품과 연속 상태가 맞지 않습니다: 다시 센 값 " + replayGrade + "x" + replayCount + " 저장 " + streakGrade + "x" + streakCount);
            }

            // 보증 카운터 = 마지막 SR 뒤 뽑기 수. 이번 라운드에 SR 이 있었으면 같아야 하고, 없었으면 적어도 부품 수만큼이다
            int pity = gachaEngine.DrawsWithoutSuperRare;
            if (sawSuperRare ? pity != sinceSuperRare : pity < sinceSuperRare)
            {
                throw new ArgumentException("이번 라운드 부품과 천장 카운터가 맞지 않습니다: 카운터 " + pity + " 마지막 SR 뒤 " + sinceSuperRare);
            }

            roundParts.AddRange(drawnParts);
        }

        private void StartRound(int round, int carriedOverDraws)
        {
            currentRound = round;
            production = 0;
            roundParts.Clear();
            roundPartsComplete = true;
            drawsRemaining = modifierEngine.DrawsForNextRound(drawsPerRound, carriedOverDraws);
            streakCount = 0;
            overclockUseCount = 0;
            overclockDrawsLeft = 0;
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
