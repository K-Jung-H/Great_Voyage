using System;
using UnityEngine;

namespace GreatVoyage
{
    [Serializable]
    public sealed class TradePortDefinition
    {
        public string portId;
        public int buyPrice = 20;
        public int sellPrice = 15;
        public int initialStock = 20;
    }

    [CreateAssetMenu(menuName = "Great Voyage/Economy Settings")]
    public sealed class EconomySettings : ScriptableObject
    {
        public string id = "prototype-economy";
        public string productId = "cloth";
        public string productName = "직물";
        [Tooltip("개발용 시작 금화. 최종 새 게임 밸런스가 아니다.")] public int initialGold = 200;
        [Tooltip("개발용 시작 명성.")] public float initialReputation = 100;
        [Tooltip("항구별 고정 개발 가격·재고. 일별 시장 변동은 후속 확장.")] public TradePortDefinition[] ports;
        [Tooltip("대표 고용 후보. 초기 선원 목록과 별개다.")] public CrewState candidate;
        [Tooltip("대표 후보의 임시 항해당 계약 금액.")] public int wagePerVoyage = 40;
        [Tooltip("대표 계약 화면의 최대 항해 횟수.")] public int maxVoyages = 5;
        [Tooltip("강제 매각 시 항구 구매 시장가에 적용할 비율.")] public float liquidationRate = .6f;
        [Tooltip("소멸시킬 미지급 금화 1당 명성 감소. 임시 밸런스.")] public float debtReputationRate = 1;
        [Tooltip("미지급 처리 후 이 값 이하면 게임 오버.")] public float gameOverThreshold;
    }
}
