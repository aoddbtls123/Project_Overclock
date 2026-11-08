using System;
using System.Collections.Generic;
using UnityEngine;

namespace Overclock.Core
{
    // 기본급 · 품목 (진열 순서 그대로). 값은 전부 [가설] — 하네스 측정으로 정한다
    [CreateAssetMenu(fileName = "ShopSettings", menuName = "Overclock/Shop Settings")]
    public sealed class ShopSettings : ScriptableObject
    {
        [SerializeField, Min(0)] private int baseWage;
        [SerializeField] private ShopItemData[] items = new ShopItemData[0];

        public int BaseWage => baseWage;
        public IReadOnlyList<ShopItemData> Items => items ?? Array.Empty<ShopItemData>();

        internal void InitializeForTests(int baseWage, params ShopItemData[] items)
        {
            this.baseWage = baseWage;
            this.items = items;
        }

        // 세션을 만들 때 부른다. 고친 에셋으로 음수 값 · 같은 효과 두 개 · 빈 칸을 만들지 못하게 한다
        internal void Validate()
        {
            if (baseWage < 0)
            {
                throw new ArgumentException("기본급이 음수입니다: " + baseWage);
            }

            var effects = new HashSet<ShopItemEffect>();
            var ids = new HashSet<string>();
            foreach (ShopItemData item in Items)
            {
                if (item == null)
                {
                    throw new ArgumentException("상점 품목에 빈 칸이 있습니다.");
                }

                if (string.IsNullOrEmpty(item.Id) || !ids.Add(item.Id))
                {
                    throw new ArgumentException("상점 품목 id 가 비었거나 중복됩니다: " + item.Id);
                }

                if (!Enum.IsDefined(typeof(ShopItemEffect), item.Effect) || !effects.Add(item.Effect))
                {
                    throw new ArgumentException("상점 품목 효과가 잘못됐거나 중복됩니다: " + item.Effect);
                }

                if (item.Price < 0 || item.Refund < 0)
                {
                    throw new ArgumentException("상점 품목 값이 음수입니다: " + item.Id);
                }
            }
        }
    }
}
