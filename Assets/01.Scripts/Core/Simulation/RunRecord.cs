using System.Collections.Generic;

namespace Overclock.Core
{
    public readonly struct RunRecord
    {
        public RunRecord(uint seed, int finalRound, bool cleared, IReadOnlyList<ModuleData> equippedModules)
        {
            Seed = seed;
            FinalRound = finalRound;
            Cleared = cleared;
            EquippedModules = equippedModules;
        }

        public uint Seed { get; }

        // 도달 라운드 = 런이 끝난 라운드. 클리어도 마지막 라운드로 센다
        public int FinalRound { get; }
        public bool Cleared { get; }
        public IReadOnlyList<ModuleData> EquippedModules { get; }
    }
}
