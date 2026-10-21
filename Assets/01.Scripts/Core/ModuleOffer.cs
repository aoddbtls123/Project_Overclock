using System;
using System.Collections.Generic;

namespace Overclock.Core
{
    public sealed class ModuleOffer
    {
        public const int OfferSize = 3;

        private readonly RandomService randomService;
        private readonly List<ModuleData>[] modulesByAxis;

        public ModuleOffer(IReadOnlyList<ModuleData> pool, RandomService randomService)
        {
            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            if (randomService == null)
            {
                throw new ArgumentNullException(nameof(randomService));
            }

            if (pool.Count == 0)
            {
                throw new ArgumentException("모듈 목록이 비어 있습니다.", nameof(pool));
            }

            this.randomService = randomService;

            int axisCount = Enum.GetValues(typeof(ModuleAxis)).Length;
            modulesByAxis = new List<ModuleData>[axisCount];
            for (int i = 0; i < axisCount; i++)
            {
                modulesByAxis[i] = new List<ModuleData>();
            }

            foreach (ModuleData module in pool)
            {
                if (module == null)
                {
                    throw new ArgumentException("모듈 목록에 비어 있는 항목이 있습니다.", nameof(pool));
                }

                modulesByAxis[(int)module.Axis].Add(module);
            }
        }

        public List<ModuleData> CreateOffer()
        {
            var remainingAxes = new List<int>();
            for (int i = 0; i < modulesByAxis.Length; i++)
            {
                if (modulesByAxis[i].Count > 0)
                {
                    remainingAxes.Add(i);
                }
            }

            // 축을 먼저 뽑고 그 축에서 모듈을 뽑는다 → 카드끼리 축이 겹치지 않는다. 카드 1장당 난수 2개
            var offer = new List<ModuleData>();
            while (offer.Count < OfferSize && remainingAxes.Count > 0)
            {
                int axisSlot = (int)(randomService.NextDouble() * remainingAxes.Count);
                List<ModuleData> candidates = modulesByAxis[remainingAxes[axisSlot]];
                remainingAxes.RemoveAt(axisSlot);

                int moduleSlot = (int)(randomService.NextDouble() * candidates.Count);
                offer.Add(candidates[moduleSlot]);
            }

            return offer;
        }

        // 세이브에서 읽은 카드가 CreateOffer 가 만들 수 있는 조합인지. 카드 수 = min(3, 모듈이 있는 축 수), 축은 서로 다르다
        internal bool IsPossibleOffer(IReadOnlyList<ModuleData> offer)
        {
            int axesWithModules = 0;
            foreach (List<ModuleData> candidates in modulesByAxis)
            {
                if (candidates.Count > 0)
                {
                    axesWithModules++;
                }
            }

            if (offer.Count != Math.Min(OfferSize, axesWithModules))
            {
                return false;
            }

            var usedAxes = new bool[modulesByAxis.Length];
            foreach (ModuleData module in offer)
            {
                int axis = (int)module.Axis;
                if (usedAxes[axis])
                {
                    return false;
                }

                usedAxes[axis] = true;
            }

            return true;
        }
    }
}
