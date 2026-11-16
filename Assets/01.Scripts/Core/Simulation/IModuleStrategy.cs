using System.Collections.Generic;

namespace Overclock.Core
{
    public interface IModuleStrategy
    {
        string Name { get; }

        // null 을 돌려주면 장착하지 않고 다음 라운드로 넘어감.
        // 선택에 난수가 필요하면 choiceRandom 만 씀 (게임 난수와 분리된 런별 선택 난수원)
        ModuleData Choose(IReadOnlyList<ModuleData> offer, RunManager runManager, RandomService choiceRandom);
    }
}
