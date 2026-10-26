using UnityEngine;

namespace Overclock.Core
{
    [CreateAssetMenu(fileName = "Module_", menuName = "Overclock/Module Data")]
    public sealed class ModuleData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string description;
        [SerializeField] private ModuleAxis axis;
        [SerializeField] private ModifierEffect[] effects = new ModifierEffect[0];
        [SerializeField] private Sprite icon;
        [SerializeField] private ModuleFamily family;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public ModuleAxis Axis => axis;
        public int EffectCount => effects == null ? 0 : effects.Length;
        public Sprite Icon => icon;
        public ModuleFamily Family => family;

        public ModifierEffect GetEffect(int index)
        {
            return effects[index];
        }

        internal void SetIdForTests(string id)
        {
            this.id = id;
        }

        internal void InitializeForTests(string displayName, params ModifierEffect[] effects)
        {
            this.displayName = displayName;
            this.effects = effects;
        }

        internal void InitializeForTests(string displayName, ModuleAxis axis, params ModifierEffect[] effects)
        {
            this.displayName = displayName;
            this.axis = axis;
            this.effects = effects;
        }
    }
}
