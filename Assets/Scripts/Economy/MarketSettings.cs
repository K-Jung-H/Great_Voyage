using UnityEngine;

namespace GreatVoyage
{
    [CreateAssetMenu(menuName = "Great Voyage/Market Settings")]
    public sealed class MarketSettings : ScriptableObject
    {
        [Tooltip("일별 가격 변동 비율.")]
        [Range(0, .8f)] public float dailyVariation = .2f;
        [Tooltip("통상 판매가/구매가 비율.")]
        [Range(.1f, 1)] public float saleRatio = .75f;
        [Tooltip("하루 항구별 주민 요청 최대 개수.")]
        public int maximumRequests = 3;
        [Tooltip("요청의 임시 만료 게임 초. 미확정 원문 단위를 확정하지 않는다.")]
        public float requestLifetime = 240;
        [Tooltip("퀘스트의 수주 후 만료 게임 초.")]
        public float questLifetime = 1200;
        [Tooltip("퀘스트 성공/실패에 적용할 현지 평판 변화량.")]
        public float questStanding = 5;
    }
}
