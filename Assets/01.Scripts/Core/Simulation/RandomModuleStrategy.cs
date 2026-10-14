using System.Collections.Generic;

namespace Overclock.Core
{
    public sealed class RandomModuleStrategy : IModuleStrategy
    {
        public string Name => "무작위";

        public ModuleData Choose(IReadOnlyList<ModuleData> offer, RunManager runManager, RandomService choiceRandom)
        {
            int index = (int)(choiceRandom.NextDouble() * offer.Count);
            return offer[index];
        }
    }
}
