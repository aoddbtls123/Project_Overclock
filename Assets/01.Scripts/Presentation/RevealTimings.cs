using System;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Presentation
{
    [CreateAssetMenu(fileName = "RevealTimings", menuName = "Overclock/Reveal Timings")]
    public sealed class RevealTimings : ScriptableObject
    {
        [SerializeField, Min(0f)] private float normalSeconds = 0.3f;
        [SerializeField, Min(0f)] private float rareSeconds = 0.8f;
        [SerializeField, Min(0f)] private float superRareSeconds = 1.5f;

        public float GetSeconds(Grade grade)
        {
            switch (grade)
            {
                case Grade.N:
                    return normalSeconds;
                case Grade.R:
                    return rareSeconds;
                case Grade.SR:
                    return superRareSeconds;
                default:
                    throw new ArgumentOutOfRangeException(nameof(grade));
            }
        }
    }
}
