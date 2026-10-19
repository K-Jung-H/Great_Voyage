using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace GreatVoyage
{
    [Serializable]
    public sealed class FloorView
    {
        public string floorId;
        public GameObject root;
        public Transform facilityRoot;
    }

    public sealed class ShipLayout : MonoBehaviour
    {
        [SerializeField, Tooltip("고정 배치와 층 구성을 정의하는 함선 데이터.")]
        private ShipDefinition definition;
        [SerializeField, Tooltip("현재 층만 표시할 화면 루트. 시뮬레이션 상태는 이 루트 밖에 보관한다.")]
        private FloorView[] views;
        [SerializeField, Tooltip("층을 이동할 플레이어.")]
        private Rigidbody2D player;
        [SerializeField, Tooltip("정지 중 층 이동을 제한할 세션.")]
        private GameSession session;
        [SerializeField, Tooltip("현재 층 표시.")]
        private Text floorLabel;

        private ShipState state;
        private bool aboard = true;
        public string ActiveFloor => state.activeFloor;
        public float CellSize => definition.cellSize;
        private static readonly Vector2Int[] Steps = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

        // Role: 저장된 함선 상태를 검증하고 화면을 상태로부터 구성한다.
        public ShipState Initialize(ShipState saved)
        {
            ValidateDefinition();
            foreach (FloorView view in views)
                view.root.transform.localScale = Vector3.one * CellSize;
            state = saved ?? NewState();
            if (state.definitionId != definition.id || Floor(state.activeFloor) == null || state.facilities == null || state.visits == null)
                throw new InvalidDataException("함선 또는 현재 층 상태가 유효하지 않습니다.");
            var ids = new HashSet<string>();
            var accepted = new List<FacilityState>();
            foreach (FacilityState item in state.facilities)
            {
                if (item == null || string.IsNullOrEmpty(item.id) || !ids.Add(item.id))
                    throw new InvalidDataException("시설 ID가 누락되거나 중복됩니다.");
                if (!ValidatePlacement(item, accepted, out string reason)) throw new InvalidDataException(reason);
                accepted.Add(item);
            }
            foreach (FloorDefinition floor in definition.floors)
                if (!CanReachFacilities(floor, accepted)) throw new InvalidDataException("계단에서 접근할 수 없는 시설 배치입니다.");
            ids.Clear();
            foreach (FloorVisit visit in state.visits)
                if (visit == null || !ids.Add(visit.floorId) || !ValidPosition(visit.floorId, new Vector2(visit.x, visit.y)))
                    throw new InvalidDataException("층별 방문 위치가 유효하지 않습니다.");
            foreach (FacilityState item in state.facilities) CreateView(item);
            ShowFloor();
            return state;
        }

        // Role: 필수 층·시설 정의와 화면 참조를 검증한다.
        private void ValidateDefinition()
        {
            if (definition == null || float.IsNaN(definition.cellSize) || float.IsInfinity(definition.cellSize) || definition.cellSize <= 0 || definition.floors == null || definition.facilities == null || views == null || player == null)
                throw new InvalidDataException("함선 정의 또는 화면 참조가 연결되지 않았습니다.");
            var ids = new HashSet<string>();
            foreach (FloorDefinition floor in definition.floors)
            {
                if (floor == null || string.IsNullOrEmpty(floor.id) || !ids.Add(floor.id) || floor.size.x <= 0 || floor.size.y <= 0 || floor.size.x > 128 || floor.size.y > 128)
                    throw new InvalidDataException("층 정의가 유효하지 않습니다.");
                FloorView view = Array.Find(views, v => v.floorId == floor.id);
                if (view == null || view.root == null || view.facilityRoot == null)
                    throw new InvalidDataException("층 화면이 연결되지 않았습니다.");
            }
            ids.Clear();
            foreach (FacilityDefinition facility in definition.facilities)
                if (facility == null || string.IsNullOrEmpty(facility.id) || !ids.Add(facility.id) || facility.size.x <= 0 || facility.size.y <= 0 || facility.prefab == null || facility.maxDurability <= 0)
                    throw new InvalidDataException("시설 정의가 유효하지 않습니다.");
        }

        // Role: 정의 자산을 수정하지 않고 독립적인 새 함선 상태를 만든다.
        private ShipState NewState()
        {
            var result = new ShipState { definitionId = definition.id, activeFloor = definition.startingFloor };
            if (definition.initialFacilities != null)
                foreach (FacilityState item in definition.initialFacilities)
                    result.facilities.Add(JsonUtility.FromJson<FacilityState>(JsonUtility.ToJson(item)));
            return result;
        }

        // Role: 이후 배치 기능에서 사용할 영역·점유·접근 가능 조건을 함께 검사한다.
        public bool CanPlace(FacilityState candidate, out string reason)
        {
            if (state == null) { reason = "함선이 초기화되지 않았습니다."; return false; }
            if (candidate == null || string.IsNullOrEmpty(candidate.id) || state.facilities.Exists(f => f.id == candidate.id))
            { reason = "시설 ID가 누락되거나 중복됩니다."; return false; }
            if (!ValidatePlacement(candidate, state.facilities, out reason)) return false;
            var proposed = new List<FacilityState>(state.facilities) { candidate };
            if (!CanReachFacilities(Floor(candidate.floorId), proposed)) { reason = "시설 접근 통로를 막습니다."; return false; }
            return true;
        }

        // Role: 시설 크기·층 제한·내구도·점유 겹침을 검사한다.
        private bool ValidatePlacement(FacilityState item, List<FacilityState> occupied, out string reason)
        {
            reason = "유효하지 않은 시설 배치입니다.";
            FloorDefinition floor = Floor(item.floorId);
            FacilityDefinition kind = Kind(item.definitionId);
            if (floor == null || kind == null || kind.allowedFloors == null || Array.IndexOf(kind.allowedFloors, item.floorId) < 0) return false;
            if (float.IsNaN(item.durability) || float.IsInfinity(item.durability) || item.durability < 0 || item.durability > kind.maxDurability) return false;
            if (item.x < 0 || item.y < 0 || item.x > floor.size.x - kind.size.x || item.y > floor.size.y - kind.size.y) return false;
            foreach (Vector2Int cell in new RectInt(item.x, item.y, kind.size.x, kind.size.y).allPositionsWithin)
                if (InRects(floor.blocked, cell) || InRects(floor.reserved, cell) || Occupied(item.floorId, cell, occupied)) return false;
            reason = null;
            return true;
        }

        // Role: 계단 진입 칸에서 갈 수 있는 영역을 구하고 모든 시설의 접근 칸을 확인한다.
        private bool CanReachFacilities(FloorDefinition floor, List<FacilityState> items)
        {
            HashSet<Vector2Int> reachable = Reachable(floor, items);
            if (reachable.Count == 0) return false;
            foreach (FacilityState item in items)
                if (item.floorId == floor.id && !TryAccess(item, reachable, out _)) return false;
            return true;
        }

        // Role: 현재 배치에서 특정 시설 옆의 도달 가능한 지역 좌표를 반환한다.
        public bool TryGetAccessPosition(string facilityId, out Vector2 position)
        {
            position = default;
            FacilityState item = state?.facilities.Find(f => f.id == facilityId);
            if (item == null) return false;
            FloorDefinition floor = Floor(item.floorId);
            if (!TryAccess(item, Reachable(floor, state.facilities), out Vector2Int cell)) return false;
            position = (floor.origin + (Vector2)cell + Vector2.one * .5f) * CellSize;
            return true;
        }

        // Role: 고정된 순서로 시설 외곽의 접근 가능한 칸을 선택한다.
        private bool TryAccess(FacilityState item, HashSet<Vector2Int> reachable, out Vector2Int access)
        {
            FacilityDefinition kind = Kind(item.definitionId);
            var area = new RectInt(item.x, item.y, kind.size.x, kind.size.y);
            foreach (Vector2Int cell in area.allPositionsWithin)
                foreach (Vector2Int step in Steps)
                    if (!area.Contains(cell + step) && reachable.Contains(cell + step)) { access = cell + step; return true; }
            access = default;
            return false;
        }

        // Role: 배치 격자에서 이동 가능한 영역을 너비 우선으로 계산한다.
        private HashSet<Vector2Int> Reachable(FloorDefinition floor, List<FacilityState> items)
        {
            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            Vector2Int start = Vector2Int.FloorToInt(floor.entry - floor.origin);
            if (!Walkable(floor, start, items)) return visited;
            visited.Add(start); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int step in Steps)
                {
                    Vector2Int next = current + step;
                    if (Walkable(floor, next, items) && visited.Add(next)) queue.Enqueue(next);
                }
            }
            return visited;
        }

        // Role: 지정 칸이 바닥 안이며 시설이나 고정 사물로 막히지 않았는지 판정한다.
        private bool Walkable(FloorDefinition floor, Vector2Int cell, List<FacilityState> items) =>
            cell.x >= 0 && cell.y >= 0 && cell.x < floor.size.x && cell.y < floor.size.y
            && !InRects(floor.blocked, cell) && !Occupied(floor.id, cell, items);

        // Role: 시설 인스턴스들의 점유 칸을 조회한다.
        private bool Occupied(string floorId, Vector2Int cell, List<FacilityState> items)
        {
            foreach (FacilityState item in items)
            {
                if (item.floorId != floorId) continue;
                FacilityDefinition kind = Kind(item.definitionId);
                if (kind != null && new RectInt(item.x, item.y, kind.size.x, kind.size.y).Contains(cell)) return true;
            }
            return false;
        }

        // Role: 예약 또는 차단 영역에 칸이 속하는지 확인한다.
        private static bool InRects(RectInt[] regions, Vector2Int cell) => regions != null && Array.Exists(regions, area => area.Contains(cell));

        // Role: 층 ID에 대응하는 정의를 찾는다.
        private FloorDefinition Floor(string id) => Array.Find(definition.floors, f => f.id == id);

        // Role: 시설 종류 ID에 대응하는 정의를 찾는다.
        private FacilityDefinition Kind(string id) => Array.Find(definition.facilities, f => f.id == id);

        // Role: 층 지역 좌표를 점유 격자 좌표로 변환한다.
        private Vector2Int Cell(FloorDefinition floor, Vector2 position) => Vector2Int.FloorToInt(position / CellSize - floor.origin);

        // Role: 저장 위치의 수치와 현재 층의 경계를 검사한다.
        public bool ValidPosition(string floorId, Vector2 position)
        {
            FloorDefinition floor = Floor(floorId);
            if (floor == null || float.IsNaN(position.x) || float.IsInfinity(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.y)) return false;
            Vector2Int cell = Cell(floor, position);
            return cell.x >= 0 && cell.y >= 0 && cell.x < floor.size.x && cell.y < floor.size.y;
        }

        // Role: 현재 층의 마지막 지역 위치를 저장 상태에 기록한다.
        public void RememberPosition()
        {
            if (state == null) return;
            FloorVisit visit = state.visits.Find(v => v.floorId == state.activeFloor);
            if (visit == null) { visit = new FloorVisit { floorId = state.activeFloor }; state.visits.Add(visit); }
            visit.x = player.position.x; visit.y = player.position.y;
        }

        // Role: 계단으로 층을 이동하고 방문한 층의 지역 위치를 복원한다.
        public void Travel(string targetFloor)
        {
            if (!aboard || state == null || session == null || !session.IsReady || session.IsPaused || targetFloor == state.activeFloor) return;
            FloorDefinition target = Floor(targetFloor);
            if (target == null) return;
            RememberPosition();
            FloorVisit visit = state.visits.Find(v => v.floorId == targetFloor);
            Vector2 destination = visit == null ? target.entry * CellSize : new Vector2(visit.x, visit.y);
            if (!ValidPosition(targetFloor, destination)) return;
            state.activeFloor = targetFloor;
            ShowFloor();
            player.position = destination;
            player.transform.position = new Vector3(destination.x, destination.y, player.transform.position.z);
            player.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        // Role: 데이터는 유지한 채 현재 층의 표시와 충돌체만 활성화한다.
        private void ShowFloor()
        {
            foreach (FloorView view in views) view.root.SetActive(aboard && view.floorId == state.activeFloor);
            if (floorLabel != null) floorLabel.text = aboard ? Floor(state.activeFloor).displayName : "항구 선착장";
        }

        // Role: 함선 밖에서는 모든 층의 표현과 충돌을 비활성화한다.
        public void SetAboard(bool value)
        {
            aboard = value;
            ShowFloor();
        }

        // Role: 저장된 시설 배치에 대응하는 표현 인스턴스를 만든다.
        private void CreateView(FacilityState item)
        {
            FloorDefinition floor = Floor(item.floorId);
            FacilityDefinition kind = Kind(item.definitionId);
            FloorView view = Array.Find(views, v => v.floorId == item.floorId);
            GameObject instance = Instantiate(kind.prefab, view.facilityRoot);
            instance.name = item.id;
            Vector2 position = floor.origin + new Vector2(item.x, item.y) + (Vector2)kind.size * .5f;
            instance.transform.localPosition = position;
            instance.transform.localScale = new Vector3(kind.size.x, kind.size.y, 1);
        }
    }
}
