using System;
using UnityEngine;

namespace Overclock.Core
{
    [Serializable]
    public struct ModifierEffect
    {
        [SerializeField] private ModifierType type;
        [SerializeField] private GradeFilter target;
        [SerializeField] private float value;

        public ModifierEffect(ModifierType type, GradeFilter target, float value)
        {
            this.type = type;
            this.target = target;
            this.value = value;
        }

        public ModifierType Type => type;
        public GradeFilter Target => target;
        public float Value => value;

        public bool AppliesTo(Grade grade)
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
    }
}
