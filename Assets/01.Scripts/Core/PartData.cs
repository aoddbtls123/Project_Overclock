using UnityEngine;

namespace Overclock.Core
{
    [CreateAssetMenu(fileName = "Part_", menuName = "Overclock/Part Data")]
    public sealed class PartData : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private Grade grade;
        [SerializeField, Min(0)] private int baseOutput = 1;

        public string DisplayName => displayName;
        public Grade Grade => grade;
        public int BaseOutput => baseOutput;

        internal void InitializeForTests(string displayName, Grade grade, int baseOutput)
        {
            this.displayName = displayName;
            this.grade = grade;
            this.baseOutput = baseOutput;
        }
    }
}
