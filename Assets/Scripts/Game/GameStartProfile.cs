using UnityEngine;

namespace GreatVoyage
{
    [CreateAssetMenu(menuName = "Great Voyage/Game Start Profile")]
    public sealed class GameStartProfile : ScriptableObject
    {
        [Tooltip("기존 ID를 유지하는 함선 정의.")]
        public ShipDefinition ship;
        [Tooltip("고정 해역과 항구 정의.")]
        public NavigationDefinition navigation;
        [Tooltip("이동·생활 조정값. 초기 선원은0명.")]
        public CrewSettings crew;
        [Tooltip("대표 상품과 고용 후보.")]
        public EconomySettings economy;
        [Tooltip("무역품·식량·수리도구의 정의.")]
        public ItemDefinition[] items;
        [Tooltip("기존 함선 ID를 유지해 읽을 정의 목록.")]
        public ShipDefinition[] legacyShips;
        [Tooltip("기존 선원 설정 ID를 유지해 읽을 목록.")]
        public CrewSettings[] legacyCrew;
        [Tooltip("재구성 전용 저장 슬롯. 기존 사용자 슬롯을 선택하지 않는다.")]
        public string saveSlot = "reconstruction-game";
        [Tooltip("명시 지정한 기존 저장 사본만 읽어 전환. 자동 탐색 없음.")]
        public string legacySourceCopy;
        [Tooltip("기획의 무작위 시작과 구분한 개발 고정 시작 프로필.")]
        public bool fixedDevelopmentStart = true;
    }
}
