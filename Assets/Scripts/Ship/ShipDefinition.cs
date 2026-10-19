using System;
using UnityEngine;

namespace GreatVoyage
{
    [Serializable]
    public sealed class FloorDefinition
    {
        [Tooltip("저장과 계단 연결에 사용할 층 ID.")] public string id;
        [Tooltip("사용자에게 표시할 층 이름.")] public string displayName;
        [Tooltip("Cell 단위 배치 격자의 좌하단 좌표.")] public Vector2 origin = new Vector2(-6, -3);
        [Tooltip("배치 격자의 가로·세로 칸 수.")] public Vector2Int size = new Vector2Int(12, 6);
        [Tooltip("첫 진입 위치. Cell 단위 좌표.")] public Vector2 entry = new Vector2(-3.5f, -1.5f);
        [Tooltip("계단 진입점으로 연결되는 층 ID. 논리 이동과 실제 계단 연결을 일치시킨다.")] public string[] connectedFloors;
        [Tooltip("고정 사물 때문에 통행·배치할 수 없는 영역.")] public RectInt[] blocked;
        [Tooltip("계단 앞 등 통행은 가능하지만 시설 배치는 금지한 영역.")] public RectInt[] reserved;
    }

    [CreateAssetMenu(menuName = "Great Voyage/Ship")]
    public sealed class ShipDefinition : ScriptableObject
    {
        [Tooltip("함선 모델 ID.")] public string id = "starter-ship";
        [Tooltip("새 게임과 이전 버전 저장의 시작 층.")] public string startingFloor = "deck-2";
        [Tooltip("한 칸의 Unity 단위 크기.")] public float cellSize = 1f;
        [Tooltip("함선의 층별 공간 정의.")] public FloorDefinition[] floors;
        [Tooltip("이 함선에서 참조하는 시설 종류.")] public FacilityDefinition[] facilities;
        [Tooltip("새 게임에 복사해 사용할 초기 시설 배치.")] public FacilityState[] initialFacilities;
    }
}
