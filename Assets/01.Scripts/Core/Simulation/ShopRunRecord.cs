using System.Collections.Generic;

namespace Overclock.Core
{
    public sealed class ShopRunRecord
    {
        public ShopRunRecord(RunRecord run, IReadOnlyList<ShopVisitRecord> visits, int finalCredits, int rerollsUsed, int overclocksUsed)
        {
            Run = run;
            Visits = visits;
            FinalCredits = finalCredits;
            RerollsUsed = rerollsUsed;
            OverclocksUsed = overclocksUsed;
        }

        public RunRecord Run { get; }
        public IReadOnlyList<ShopVisitRecord> Visits { get; }
        public int FinalCredits { get; }
        public int RerollsUsed { get; }
        public int OverclocksUsed { get; }
    }
}
