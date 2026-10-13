using UnityEngine;

namespace Overclock.Core
{
    [CreateAssetMenu(fileName = "Module_", menuName = "Overclock/Module Data")]
    public sealed class ModuleData : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private string description;
        [SerializeField] private ModifierEffect[] effects = new ModifierEffect[0];

        public string DisplayName => displayName;
        public string Description => description;
        public int EffectCount => effects == null ? 0 : effects.Length;

        public ModifierEffect GetEffect(int index)
        {
            return effects[index];
        }

        internal void InitializeForTests(string displayName, params ModifierEffect[] effects)
        {
            this.displayName = displayName;
            this.effects = effects;
        }
    }
}
