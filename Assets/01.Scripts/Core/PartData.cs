using UnityEngine;

namespace Overclock.Core
{
    [CreateAssetMenu(fileName = "Part_", menuName = "Overclock/Part Data")]
    public sealed class PartData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Grade grade;
        [SerializeField] private PartTag tag;
        [SerializeField, Min(0)] private int baseOutput = 1;
        [SerializeField] private Sprite icon;

        public string Id => id;
        public string DisplayName => displayName;
        public Grade Grade => grade;
        public PartTag Tag => tag;
        public int BaseOutput => baseOutput;
        public Sprite Icon => icon;

        internal void InitializeForTests(string displayName, Grade grade, int baseOutput, PartTag tag = PartTag.None)
        {
            this.displayName = displayName;
            this.grade = grade;
            this.baseOutput = baseOutput;
            this.tag = tag;
        }
    }
}
