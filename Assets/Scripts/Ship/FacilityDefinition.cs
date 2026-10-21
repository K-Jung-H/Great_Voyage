using UnityEngine;

namespace GreatVoyage
{
    public enum FacilityRole { General, Sail, Armory, Cannon, Prison, Lookout, FishingSpot, Cargo, Toilet, Helm, Journal }
    [CreateAssetMenu(menuName = "Great Voyage/Facility")]
    public sealed class FacilityDefinition : ScriptableObject
    {
        [Tooltip("저장 데이터에서 참조할 시설 종류 ID.")]
        public string id;
        [Tooltip("시설 종류의 동작 분류. 표시 이름/ID로 규칙을 판정하지 않는다.")]
        public FacilityRole role;
        [Tooltip("수용 인원 등 시설 역할에 따른 용량.")]
        public int capacity = 1;
        [Tooltip("항구 구매 가격. 조정용 수치.")]
        public int purchasePrice = 40;
        [Tooltip("내구도 1당 조선소 수리 가격.")]
        public float repairPrice = .2f;
        [Tooltip("함선에 최소 한 개 필요하며 작동 상태로 출항해야 하는 시설.")]
        public bool essential;
        [Tooltip("조선소 업그레이드의 최대 단계.")]
        public int maximumLevel = 3;
        [Tooltip("시설 점유와 독립된 통행 분류. 기본값은 기존 통행 불가를 보존한다.")]
        public TilePassability passability = TilePassability.Impassable;
        [Tooltip("격자에서 차지하는 가로·세로 칸 수.")]
        public Vector2Int size = new Vector2Int(2, 1);
        [Tooltip("배치 가능한 층 ID 목록.")]
        public string[] allowedFloors;
        [Tooltip("시설의 최대 내구도.")]
        public float maxDurability = 100f;
        [Tooltip("정비 자격이 있는 선원이 부분 손상을 수리할 수 있는 시설.")]
        public bool repairable = true;
        [Tooltip("선원이 취침에 사용할 수 있는 침대 시설.")]
        public bool usableAsBed;
        [Tooltip("1×1 기준 영역에 정규화한 표현·Collider·Trigger Prefab. 점유 칸 수는 size로 적용한다.")]
        public GameObject prefab;
    }
}
