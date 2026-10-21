using UnityEngine;

namespace GreatVoyage
{
    public enum ItemUse
    {
        TradeGoods,
        Food,
        RepairTools,
        Weapon,
        Medicine
    }
    public enum ProductCategory { Food, Vegetables, Meat, Fish, Clothing, Weapons, Medicine, Spices, Crafts }

    [CreateAssetMenu(menuName = "Great Voyage/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Tooltip("저장/재고가 참조하는 안정 상품 ID.")]
        public string id;
        [Tooltip("표시 이름.")]
        public string displayName;
        [Tooltip("소비 목적. 무역품 외형을 보고 식량으로 바꾸지 않는다.")]
        public ItemUse use;
        [Tooltip("무역 대상 여부.")]
        public bool tradeable;
        [Tooltip("상점 분류. 식량 소비 목적과 별개다.")]
        public ProductCategory category;
        [Tooltip("기본 구매 시장가.")]
        public int basePrice = 20;
        [Tooltip("날짜 변경 시 공급할 기본 재고.")]
        public int dailyStock = 20;
    }
}
