using System.Collections.Generic;

namespace Overclock.Core
{
    public sealed class RunSession
    {
        private const int PityThreshold = 50;
        private const int RoundCount = 8;
        private const int DrawsPerRound = 10;
        private const double BaseN = 0.72;
        private const double BaseR = 0.25;
        private const double BaseSR = 0.03;

        private RunSession(GachaEngine gachaEngine, RunManager runManager, ModuleOffer moduleOffer)
        {
            GachaEngine = gachaEngine;
            RunManager = runManager;
            ModuleOffer = moduleOffer;
        }

        public GachaEngine GachaEngine { get; }
        public RunManager RunManager { get; }
        public ModuleOffer ModuleOffer { get; }

        public static RunSession Create(uint seed, IReadOnlyList<PartData> parts, IReadOnlyList<ModuleData> modules)
        {
            // 보증 카운터가 런 전체에서 이어져야 하므로 GachaEngine 은 런마다 한 번만 만든다
            // 같은 시드로 같은 판을 재현하려면 모든 난수 소비가 이 RandomService 하나를 거쳐야 한다
            var randomService = new RandomService(seed);
            var gachaEngine = new GachaEngine(randomService, PityThreshold, BaseN, BaseR, BaseSR);
            var partCatalog = new PartCatalog(parts, randomService);
            var runManager = new RunManager(gachaEngine, partCatalog, RoundCount, DrawsPerRound);
            var moduleOffer = new ModuleOffer(modules, randomService);
            return new RunSession(gachaEngine, runManager, moduleOffer);
        }
    }
}
