using UnityEngine;

namespace Overclock.Core
{
    [CreateAssetMenu(fileName = "ShopItem_", menuName = "Overclock/Shop Item Data")]
    public sealed class ShopItemData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string description;
        [SerializeField] private ShopItemKind kind;
        [SerializeField] private ShopItemEffect effect;
        [SerializeField, Min(0)] private int price;
        // 모듈 분해만 쓴다. 분해 = 가격을 내고 환급액을 받는다
        [SerializeField, Min(0)] private int refund;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public ShopItemKind Kind => kind;
        public ShopItemEffect Effect => effect;
        public int Price => price;
        public int Refund => refund;

        internal void InitializeForTests(string id, ShopItemKind kind, ShopItemEffect effect, int price, int refund)
        {
            this.id = id;
            displayName = id;
            this.kind = kind;
            this.effect = effect;
            this.price = price;
            this.refund = refund;
        }
    }
}
