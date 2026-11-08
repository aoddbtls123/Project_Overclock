using System.Collections.Generic;

namespace Overclock.Core
{
    // 살 수 있는 품목과 "나가기" 중 하나를 고르게 고르기를 나가기가 나올 때까지 반복한다. 분해할 모듈 · 대체 카드도 무작위
    public sealed class RandomShopStrategy : IShopStrategy
    {
        public string Name => "무작위 구매";

        public void Visit(RunSession session, RandomService choiceRandom)
        {
            var available = new List<ShopItemData>();
            while (true)
            {
                available.Clear();
                foreach (ShopItemData item in session.ShopItems)
                {
                    if (session.GetItemStatus(item) == ShopItemStatus.Available)
                    {
                        available.Add(item);
                    }
                }

                int pick = (int)(choiceRandom.NextDouble() * (available.Count + 1));
                if (pick == available.Count)
                {
                    return;
                }

                ShopItemData chosen = available[pick];
                if (chosen.Effect == ShopItemEffect.Decompose)
                {
                    int index = (int)(choiceRandom.NextDouble() * session.RunManager.EquippedModules.Count);
                    session.DecomposeModule(index);
                    IReadOnlyList<ModuleData> replacement = session.ReplacementOffer;
                    session.SelectReplacement(replacement[(int)(choiceRandom.NextDouble() * replacement.Count)]);
                }
                else
                {
                    session.BuyItem(chosen);
                }
            }
        }
    }
}
