namespace Overclock.Core
{
    public readonly struct ShopVisitRecord
    {
        public ShopVisitRecord(int round, int creditsOnEntry, int availableOnEntry, bool boughtSupercharger, int spareCardsBought, bool decomposed, int creditsOnLeave)
        {
            Round = round;
            CreditsOnEntry = creditsOnEntry;
            AvailableOnEntry = availableOnEntry;
            BoughtSupercharger = boughtSupercharger;
            SpareCardsBought = spareCardsBought;
            Decomposed = decomposed;
            CreditsOnLeave = creditsOnLeave;
        }

        public int Round { get; }
        public int CreditsOnEntry { get; }

        // 들어간 순간 살 수 있던 (Available) 품목 수 — 지표 S4
        public int AvailableOnEntry { get; }
        public bool BoughtSupercharger { get; }
        public int SpareCardsBought { get; }
        public bool Decomposed { get; }
        public int CreditsOnLeave { get; }
    }
}
