using System;
using System.Collections.Generic;

namespace Overclock.Core
{
    public sealed class RunSession
    {
        private const int PityThreshold = 50;
        private const int RoundCount = 8;
        // 무한모드에는 마지막 라운드가 없다. 클리어 판정(현재 라운드 == 라운드 수)에 닿지 않는 값을 넘긴다
        private const int EndlessRoundCount = int.MaxValue;
        private const int DrawsPerRound = 10;
        private const double BaseN = 0.72;
        private const double BaseR = 0.25;
        private const double BaseSR = 0.03;

        private RunSession(RunMode mode, GachaEngine gachaEngine, RunManager runManager, ModuleOffer moduleOffer)
        {
            Mode = mode;
            GachaEngine = gachaEngine;
            RunManager = runManager;
            ModuleOffer = moduleOffer;
        }

        public RunMode Mode { get; }
        public GachaEngine GachaEngine { get; }
        public RunManager RunManager { get; }
        public ModuleOffer ModuleOffer { get; }

        public static RunSession Create(uint seed, IReadOnlyList<PartData> parts, IReadOnlyList<ModuleData> modules)
        {
            return Create(seed, parts, modules, RunMode.Normal);
        }

        public static RunSession Create(uint seed, IReadOnlyList<PartData> parts, IReadOnlyList<ModuleData> modules, RunMode mode)
        {
            if (mode != RunMode.Normal && mode != RunMode.Endless)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            int roundCount = mode == RunMode.Endless ? EndlessRoundCount : RoundCount;

            // 보증 카운터가 런 전체에서 이어져야 하므로 GachaEngine 은 런마다 한 번만 만든다
            // 같은 시드로 같은 판을 재현하려면 모든 난수 소비가 이 RandomService 하나를 거쳐야 한다
            var randomService = new RandomService(seed);
            var gachaEngine = new GachaEngine(randomService, PityThreshold, BaseN, BaseR, BaseSR);
            var partCatalog = new PartCatalog(parts, randomService);
            var runManager = new RunManager(gachaEngine, partCatalog, roundCount, DrawsPerRound);
            var moduleOffer = new ModuleOffer(modules, randomService);
            return new RunSession(mode, gachaEngine, runManager, moduleOffer);
        }
    }
}
