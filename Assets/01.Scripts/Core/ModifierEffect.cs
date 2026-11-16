using System;
using UnityEngine;

namespace Overclock.Core
{
    [Serializable]
    public struct ModifierEffect
    {
        [SerializeField] private ModifierType type;
        [SerializeField] private GradeFilter target;
        [SerializeField] private PartTagFilter tagTarget;
        [SerializeField] private float value;

        public ModifierEffect(ModifierType type, GradeFilter target, float value)
            : this(type, target, PartTagFilter.Any, value)
        {
        }

        public ModifierEffect(ModifierType type, GradeFilter target, PartTagFilter tagTarget, float value)
        {
            this.type = type;
            this.target = target;
            this.tagTarget = tagTarget;
            this.value = value;
        }

        public ModifierType Type => type;
        public GradeFilter Target => target;
        public PartTagFilter TagTarget => tagTarget;
        public float Value => value;

        // 등급 조건과 태그 조건을 모두 만족해야 적용됨
        public bool AppliesTo(Grade grade, PartTag tag)
        {
            return MatchesGrade(grade) && MatchesTag(tag);
        }

        private bool MatchesGrade(Grade grade)
        {
            switch (target)
            {
                case GradeFilter.Any:
                    return true;
                case GradeFilter.N:
                    return grade == Grade.N;
                case GradeFilter.R:
                    return grade == Grade.R;
                case GradeFilter.SR:
                    return grade == Grade.SR;
                default:
                    return false;
            }
        }

        private bool MatchesTag(PartTag tag)
        {
            switch (tagTarget)
            {
                case PartTagFilter.Any:
                    return true;
                case PartTagFilter.Electronic:
                    return tag == PartTag.Electronic;
                case PartTagFilter.Mechanical:
                    return tag == PartTag.Mechanical;
                case PartTagFilter.Power:
                    return tag == PartTag.Power;
                default:
                    return false;
            }
        }
    }
}
