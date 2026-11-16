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
        private readonly ModifierEngine modifierEngine;
        private readonly List<ModuleData> equippedModules = new List<ModuleData>();
        private readonly List<PartData> roundParts = new List<PartData>(ModifierEngine.MaxDrawsPerRound);

        private int currentRound;
        private int production;
        private int drawsRemaining;
        private RunState state;
        private Grade streakGrade;
        private int streakCount;
        private bool overclockUsed;
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
        public IReadOnlyList<ModuleData> EquippedModules => equippedModules;

        /// <summary>이번 라운드에 뽑은 부품 (뽑은 순서). 새 라운드가 시작되면 비움. 화면의 수확물 더미가 씀.</summary>
        public IReadOnlyList<PartData> RoundParts => roundParts;

        /// <summary>
        /// RoundParts 가 이번 라운드 처음부터 빠짐없는가. 부품 목록이 없는 옛 세이브로 이어 한 라운드는 false — 앞 부품을 모르므로 저장하지 않음 (다음 라운드부터 true).
        /// </summary>
        public bool RoundPartsComplete => roundPartsComplete;
        internal Grade StreakGrade => streakGrade;
        internal int StreakCount => streakCount;

        /// <summary>이번 라운드에 오버클럭을 이미 켰는가. 라운드가 바뀌면 다시 false.</summary>
        public bool OverclockUsed => overclockUsed;

        /// <summary>오버클럭이 걸려 있는 남은 뽑기 수 (0 = 꺼짐).</summary>
        public int OverclockDrawsLeft => overclockDrawsLeft;

        public bool CanActivateOverclock => state == RunState.Drawing && !overclockUsed;

        public static int QuotaFor(int round)
        {
            if (round < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(round));
            }

            double quota = Math.Round(BaseQuota * Math.Pow(QuotaGrowth, round - 1), MidpointRounding.AwayFromZero);

            // 무한모드에서 int 를 넘는 라운드(R54~)는 int 최댓값으로 둠. 넘긴 채 변환하면 음수가 되어 바로 돌파함
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
                ? modifierEngine.CalculateOverclockedOutput(part, streakTriggered)
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

            // 돌파 · 런 끝이면 남은 오버클럭 횟수는 사라짐
            if (state != RunState.Drawing)
            {
                overclockDrawsLeft = 0;
            }

            return new DrawResult(part, output, streakTriggered, overclocked);
        }

        /// <summary>
        /// 오버클럭을 켬. 라운드마다 1번 · 다음 뽑기부터 3회 (남은 뽑기가 적으면 그만큼). 난수는 쓰지 않음.
        /// </summary>
        public void ActivateOverclock()
        {
            if (!CanActivateOverclock)
            {
                throw new InvalidOperationException("오버클럭을 켤 수 없습니다: " + state + (overclockUsed ? " (이번 라운드 사용함)" : ""));
            }

            overclockUsed = true;
            overclockDrawsLeft = Math.Min(OverclockDraws, drawsRemaining);
        }

        public void EquipModule(ModuleData module)
        {
            if (state != RunState.Drawing && state != RunState.RoundCleared)
            {
                throw new InvalidOperationException("런이 끝났습니다: " + state);
            }

            modifierEngine.Equip(module);
            equippedModules.Add(module);

            // GachaEngine 은 새로 만들지 않음. 보증 카운터가 런 전체에서 이어져야 함
            modifierEngine.GetProbabilities(out double n, out double r, out double sr);
            gachaEngine.SetProbabilities(n, r, sr);
        }

        // 확률만 올리는 카드 중 장착 전후 확률이 완전히 같은 카드만 새 제안에서 제외.
        // 다른 효과가 섞였거나 R/SR 어느 한쪽이라도 남은 효과가 있으면 유지함.
        public bool CanOfferModule(ModuleData module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (module.EffectCount == 0) return true;
            for (int i = 0; i < module.EffectCount; i++)
            {
                ModifierType type = module.GetEffect(i).Type;
                if (type != ModifierType.RareChance && type != ModifierType.SuperRareChance)
                    return true;
            }

            ModifierEngine after = modifierEngine.Clone();
            after.Equip(module);
            modifierEngine.GetProbabilities(out double n, out double r, out double sr);
            after.GetProbabilities(out double nextN, out double nextR, out double nextSR);
            const double tolerance = 1e-9;
            return Math.Abs(n - nextN) > tolerance || Math.Abs(r - nextR) > tolerance || Math.Abs(sr - nextSR) > tolerance;
        }

        public ModulePreview PreviewModule(ModuleData module)
        {
            // 실제 엔진은 건드리지 않음. 전/후 모두 복제본으로 계산
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

            // 남은 뽑기는 새 라운드 횟수로 덮어쓰기 전에 읽어야 이월됨
            StartRound(currentRound + 1, drawsRemaining);
        }

        // 세이브 복원 전용. 장착 모듈은 EquipModule 로 먼저 다시 단 뒤에 부름.
        // Draw 가 만들 수 있는 상태만 받음. 고친 세이브로 끝나지 않는 라운드를 만들지 못하게 함
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

        // 세이브 복원 전용. RestoreProgress 다음에 부름. ActivateOverclock 과 Draw 가 만들 수 있는 값만 받음
        internal void RestoreOverclock(bool used, int drawsLeft)
        {
            if (drawsLeft < 0 || drawsLeft > OverclockDraws)
            {
                throw new ArgumentOutOfRangeException(nameof(drawsLeft));
            }

            if (drawsLeft > 0 && (!used || state != RunState.Drawing || drawsLeft > drawsRemaining))
            {
                throw new ArgumentException("오버클럭 상태가 진행과 맞지 않습니다: 사용 " + used + " 남은 " + drawsLeft + " " + state + " 남은 뽑기 " + drawsRemaining);
            }

            overclockUsed = used;
            overclockDrawsLeft = drawsLeft;
        }

        // 세이브 복원 전용 (옛 세이브). 이번 라운드에 이미 뽑은 부품을 모름 → 빈 목록 · 이번 라운드는 목록을 저장하지 않음.
        // 모르는 앞 부품이 연속 · 천장에 들어 있어서, 뒤에 쌓인 부품만으로 검사하면 맞는 세이브를 거부하게 됨 (교차검토 #32)
        internal void MarkRoundPartsUnknown()
        {
            roundParts.Clear();
            roundPartsComplete = false;
        }

        // 세이브 복원 전용. RestoreProgress · 보증 복원 다음에 부름. 라운드 처음부터 빠짐없는 목록만 받음 (빈 목록 = 아직 안 뽑음).
        // Draw 가 만들 수 있는 것만 받음: 이번 라운드 뽑기 수 상한 · 뽑은 적 없으면 생산량 0 · 뽑기 중 · 연속 상태 · 보증 카운터가 목록과 맞아야 함
        internal void RestoreRoundParts(IReadOnlyList<PartData> drawnParts)
        {
            roundParts.Clear();
            roundPartsComplete = true;
            if (drawnParts == null || drawnParts.Count == 0)
            {
                // 뽑지 않은 라운드: 생산량 0 · 뽑기 중 · 연속 0 (라운드 시작 상태) 이어야 함
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

            // 연속은 라운드 시작에 0 이므로 이번 라운드 부품만으로 다시 셀 수 있음 (AdvanceStreak 와 같은 규칙)
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

            // 보증 카운터 = 마지막 SR 뒤 뽑기 수. 이번 라운드에 SR 이 있었으면 같아야 하고, 없었으면 적어도 부품 수만큼임
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
            overclockUsed = false;
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
