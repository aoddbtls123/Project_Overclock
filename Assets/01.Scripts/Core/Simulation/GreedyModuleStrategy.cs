using System;
using System.Collections.Generic;

namespace Overclock.Core
{
    public sealed class GreedyModuleStrategy : IModuleStrategy
    {
        private readonly IReadOnlyList<PartData> parts;

        public GreedyModuleStrategy(IReadOnlyList<PartData> parts)
        {
            if (parts == null)
            {
                throw new ArgumentNullException(nameof(parts));
            }

            this.parts = parts;
        }

        public string Name => "기대 생산량 최대";

        public ModuleData Choose(IReadOnlyList<ModuleData> offer, RunManager runManager, RandomService choiceRandom)
        {
            ModuleData best = null;
            double bestValue = double.MinValue;
            foreach (ModuleData module in offer)
            {
                double value = ExpectedRoundOutput(runManager.PreviewModule(module), parts);
                if (value > bestValue)
                {
                    best = module;
                    bestValue = value;
                }
            }

            return best;
        }

        internal static double ExpectedRoundOutput(ModulePreview preview, IReadOnlyList<PartData> parts)
        {
            return preview.ExpectedRoundOutputAfter(parts);
        }
    }
}
