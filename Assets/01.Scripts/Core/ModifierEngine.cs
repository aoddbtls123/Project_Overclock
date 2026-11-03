using System;
using System.Collections.Generic;

namespace Overclock.Core
{
    public sealed class ModifierEngine
    {
        public const double MaxSuperRareChance = 0.25;
        public const double MaxRareChance = 0.5;
        public const double MaxOutputMultiplier = 8.0;
        public const int MaxDrawsPerRound = 20;
        // 오버클럭 중 R · SR 배수 증가분. 모듈 배수와 더해서 합치고 ×8 상한 안에서만 (N 은 0)
        public const double OverclockMultiplierBonus = 1.0;

        private readonly double baseN;
        private readonly double baseR;
        private readonly double baseSR;
        private readonly List<ModifierEffect> effects = new List<ModifierEffect>();

        public ModifierEngine(double baseN, double baseR, double baseSR)
        {
            this.baseN = baseN;
            this.baseR = baseR;
            this.baseSR = baseSR;
        }

        public ModifierEngine Clone()
        {
            var clone = new ModifierEngine(baseN, baseR, baseSR);
            clone.effects.AddRange(effects);
            return clone;
        }

        public void Equip(ModuleData module)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            for (int i = 0; i < module.EffectCount; i++)
            {
                effects.Add(module.GetEffect(i));
            }
        }

        public void GetProbabilities(out double n, out double r, out double sr)
        {
            // 효과를 전부 더한 뒤 한 번만 계산한다. 그래야 장착 순서가 결과를 바꾸지 않는다
            double superRareBonus = SumValues(ModifierType.SuperRareChance);
            double rareBonus = SumValues(ModifierType.RareChance);

            // 상한은 모듈이 올리는 양에만 건다. 기본표의 값을 깎지 않는다
            sr = Math.Min(baseSR + superRareBonus, Math.Max(MaxSuperRareChance, baseSR));
            r = Math.Min(baseR + rareBonus, Math.Max(MaxRareChance, baseR));
            bool superRareRaised = sr != baseSR;
            bool rareRaised = r != baseR;
            if (!superRareRaised && !rareRaised)
            {
                n = baseN;
                r = baseR;
                sr = baseSR;
                return;
            }

            // 올린 등급은 올린 값으로 두고, 올리지 않은 등급이 남은 몫을 원래 비율대로 나눠 갖는다
            double raisedSum = 0.0;
            double raisedBaseSum = 0.0;
            if (superRareRaised)
            {
                raisedSum += sr;
                raisedBaseSum += baseSR;
            }

            if (rareRaised)
            {
                raisedSum += r;
                raisedBaseSum += baseR;
            }

            double scale = (1.0 - raisedSum) / (1.0 - raisedBaseSum);
            n = baseN * scale;
            if (!rareRaised)
            {
                r = baseR * scale;
            }

            if (!superRareRaised)
            {
                sr = baseSR * scale;
            }
        }

        public int CalculateOutput(PartData part, bool streakTriggered)
        {
            if (part == null)
            {
                throw new ArgumentNullException(nameof(part));
            }

            return CalculateOutput(part.Grade, part.Tag, part.BaseOutput, streakTriggered);
        }

        /// <summary>오버클럭 중 생산량. N 은 0 (고정 · 연속 보너스 포함), R · SR 은 배수 증가분 +1 (×8 상한 안).</summary>
        public int CalculateOverclockedOutput(PartData part, bool streakTriggered)
        {
            if (part == null)
            {
                throw new ArgumentNullException(nameof(part));
            }

            return CalculateOverclockedOutput(part.Grade, part.Tag, part.BaseOutput, streakTriggered);
        }

        internal int CalculateOverclockedOutput(Grade grade, PartTag tag, int baseOutput, bool streakTriggered)
        {
            if (grade == Grade.N)
            {
                return 0;
            }

            return CalculateOutput(grade, tag, baseOutput, streakTriggered, OverclockMultiplierBonus);
        }

        internal int CalculateOutput(Grade grade, PartTag tag, int baseOutput, bool streakTriggered)
        {
            return CalculateOutput(grade, tag, baseOutput, streakTriggered, 0.0);
        }

        private int CalculateOutput(Grade grade, PartTag tag, int baseOutput, bool streakTriggered, double extraMultiplier)
        {
            double bonus = SumValues(ModifierType.FlatBonus, grade, tag);
            if (streakTriggered)
            {
                bonus += SumValues(ModifierType.StreakBonus, grade, tag);
            }

            double multiplier = Math.Min(1.0 + SumValues(ModifierType.OutputMultiplier, grade, tag) + extraMultiplier, MaxOutputMultiplier);

            // 가산(고정 보너스, 연속 보너스) → 승산 순서 고정. 반올림은 부품 1개마다
            return (int)Math.Round((baseOutput + bonus) * multiplier, MidpointRounding.AwayFromZero);
        }

        public int DrawsPerRound(int baseDraws)
        {
            return DrawsForNextRound(baseDraws, 0);
        }

        // 다음 라운드 뽑기 = 기본 + 추가 + min(남은 뽑기, 이월 상한 합). 20회 상한은 마지막에 한 번만 건다
        public int DrawsForNextRound(int baseDraws, int remainingDraws)
        {
            if (remainingDraws < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(remainingDraws));
            }

            int extra = (int)Math.Round(SumValues(ModifierType.ExtraDraws), MidpointRounding.AwayFromZero);
            int carryCap = (int)Math.Round(SumValues(ModifierType.CarryOverDraws), MidpointRounding.AwayFromZero);
            int carried = Math.Min(remainingDraws, carryCap);
            return Math.Min(baseDraws + extra + carried, MaxDrawsPerRound);
        }

        private double SumValues(ModifierType type)
        {
            double sum = 0.0;
            foreach (ModifierEffect effect in effects)
            {
                if (effect.Type == type)
                {
                    sum += effect.Value;
                }
            }

            return sum;
        }

        private double SumValues(ModifierType type, Grade grade, PartTag tag)
        {
            double sum = 0.0;
            foreach (ModifierEffect effect in effects)
            {
                if (effect.Type == type && effect.AppliesTo(grade, tag))
                {
                    sum += effect.Value;
                }
            }

            return sum;
        }
    }
}
