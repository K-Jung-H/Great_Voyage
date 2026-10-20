using UnityEngine;

namespace GreatVoyage
{
    public enum ItemUse
    {
        TradeGoods,
        Food,
        RepairTools
    }

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
    }
}
