using System;
using System.Collections.Generic;

namespace Overclock.Core
{
    public sealed class ModifierEngine
    {
        public const double MaxSuperRareChance = 0.25;
        public const double MaxOutputMultiplier = 8.0;
        public const int MaxDrawsPerRound = 20;

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
            double bonus = SumValues(ModifierType.SuperRareChance);

            // 상한은 모듈이 올리는 양에만 건다. 기본표의 SR 을 깎지 않는다
            sr = Math.Min(baseSR + bonus, Math.Max(MaxSuperRareChance, baseSR));
            if (sr == baseSR)
            {
                n = baseN;
                r = baseR;
                return;
            }

            // 비례 축소: N 과 R 의 비율을 유지한 채 (1 - SR) 을 나눠 갖는다
            double scale = (1.0 - sr) / (1.0 - baseSR);
            n = baseN * scale;
            r = baseR * scale;
        }

        public int CalculateOutput(PartData part, bool streakTriggered)
        {
            if (part == null)
            {
                throw new ArgumentNullException(nameof(part));
            }

            return CalculateOutput(part.Grade, part.Tag, part.BaseOutput, streakTriggered);
        }

        internal int CalculateOutput(Grade grade, PartTag tag, int baseOutput, bool streakTriggered)
        {
            double bonus = streakTriggered ? SumValues(ModifierType.StreakBonus, grade, tag) : 0.0;
            double multiplier = Math.Min(1.0 + SumValues(ModifierType.OutputMultiplier, grade, tag), MaxOutputMultiplier);

            // 가산 → 승산 순서 고정. 반올림은 부품 1개마다
            return (int)Math.Round((baseOutput + bonus) * multiplier, MidpointRounding.AwayFromZero);
        }

        public int DrawsPerRound(int baseDraws)
        {
            int extra = (int)Math.Round(SumValues(ModifierType.ExtraDraws), MidpointRounding.AwayFromZero);
            return Math.Min(baseDraws + extra, MaxDrawsPerRound);
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
