using System;
using System.Collections.Generic;

namespace Overclock.Core
{
    // 규칙 기반 (최적 아님 · 11/08 사전고정 v3): 과급기 → 분해 (가장 덜 아까운 모듈의 다음 라운드 기대 생산량 손실이
    // 다시 받기 기준 + 순이익 (환급 − 가격) 보다 작으면 · 대체 제안에서는 기대 생산량 최대) → 남은 돈으로 예비 카드
    public sealed class GreedyShopStrategy : IShopStrategy
    {
        private readonly IReadOnlyList<PartData> parts;
        private readonly GreedyModuleStrategy replacementChooser;

        public GreedyShopStrategy(IReadOnlyList<PartData> parts)
        {
            if (parts == null)
            {
                throw new ArgumentNullException(nameof(parts));
            }

            this.parts = parts;
            replacementChooser = new GreedyModuleStrategy(parts);
        }

        public string Name => "탐욕 구매";

        public void Visit(RunSession session, RandomService choiceRandom)
        {
            ShopItemData supercharger = session.FindShopItem(ShopItemEffect.Supercharger);
            if (supercharger != null && session.GetItemStatus(supercharger) == ShopItemStatus.Available)
            {
                session.BuyItem(supercharger);
            }

            ShopItemData decompose = session.FindShopItem(ShopItemEffect.Decompose);
            if (decompose != null && session.GetItemStatus(decompose) == ShopItemStatus.Available)
            {
                int index = LeastValuableModule(session.RunManager, out double loss);
                // 대체 제안의 가장 좋은 카드는 대개 다시 받기 기준 (하위 25% 근처) 이상을 준다 → 그만큼 + 순이익보다 덜 잃으면 바꾼다
                if (loss < SimulationPlayPolicy.DefaultRerollGainThreshold + decompose.Refund - decompose.Price)
                {
                    session.DecomposeModule(index);
                    session.SelectReplacement(replacementChooser.Choose(session.ReplacementOffer, session.RunManager, choiceRandom));
                }
            }

            ShopItemData spare = session.FindShopItem(ShopItemEffect.SpareCard);
            while (spare != null && session.GetItemStatus(spare) == ShopItemStatus.Available)
            {
                session.BuyItem(spare);
            }
        }

        // 빼도 다음 라운드 기대 생산량이 가장 덜 줄어드는 칸. 같으면 앞 칸
        private int LeastValuableModule(RunManager runManager, out double leastLoss)
        {
            int best = 0;
            double bestLoss = double.MaxValue;
            for (int i = 0; i < runManager.EquippedModules.Count; i++)
            {
                ModulePreview preview = runManager.PreviewRemoveModule(i);
                double loss = preview.ExpectedRoundOutputBefore(parts) - preview.ExpectedRoundOutputAfter(parts);
                if (loss < bestLoss)
                {
                    best = i;
                    bestLoss = loss;
                }
            }

            leastLoss = bestLoss;
            return best;
        }
    }
}
