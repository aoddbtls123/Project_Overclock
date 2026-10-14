using System.Collections.Generic;

namespace Overclock.Core
{
    public sealed class NoModuleStrategy : IModuleStrategy
    {
        public string Name => "모듈 없음";

        public ModuleData Choose(IReadOnlyList<ModuleData> offer, RunManager runManager, RandomService choiceRandom)
        {
            return null;
        }
    }
}
