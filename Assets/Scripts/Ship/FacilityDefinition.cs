using UnityEngine;

namespace GreatVoyage
{
    [CreateAssetMenu(menuName = "Great Voyage/Facility")]
    public sealed class FacilityDefinition : ScriptableObject
    {
        [Tooltip("저장 데이터에서 참조할 시설 종류 ID.")]
        public string id;
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
