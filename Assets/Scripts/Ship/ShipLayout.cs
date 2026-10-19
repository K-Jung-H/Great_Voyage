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
        private readonly Dictionary<string, GameObject> facilityViews = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Color[]> facilityColors = new Dictionary<string, Color[]>();
        public string ActiveFloor => state.activeFloor;
        public IReadOnlyList<FacilityState> Facilities => state.facilities;
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
            foreach (FloorDefinition floor in definition.floors)
                if (floor.connectedFloors != null)
                    foreach (string connected in floor.connectedFloors)
                        if (connected == floor.id || Floor(connected) == null)
                            throw new InvalidDataException("계단의 논리 층 연결이 유효하지 않습니다.");
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

        // Role: 안정 시설 ID로 런타임 상태를 조회한다.
        public FacilityState GetFacility(string id) => state.facilities.Find(f => f.id == id);

        // Role: 시설 종류의 크기·기능·수리 조건을 조회한다.
        public FacilityDefinition GetFacilityDefinition(string id) => Kind(id);

        // Role: 해당 층의 표시 여부를 상태에서 판단한다.
        public bool IsFloorVisible(string id) => aboard && state.activeFloor == id;

        // Role: 선원 Cell 좌표가 실제 통행 가능한 칸인지 검사한다.
        public bool IsWalkablePosition(string id, Vector2 position)
        {
            FloorDefinition floor = Floor(id);
            return floor != null && !float.IsNaN(position.x) && !float.IsNaN(position.y) && !float.IsInfinity(position.x) && !float.IsInfinity(position.y)
                && Walkable(floor, Vector2Int.FloorToInt(position - floor.origin), state.facilities);
        }

        // Role: 선원용 계단 진입점의 Cell 좌표를 반환한다.
        public Vector2 EntryPosition(string id) => Floor(id).entry;

        // Role: 현재 층의 배회 후보를 논리 칸 중심으로 정렬한다.
        public Vector2 CellCenter(string id, Vector2 position)
        {
            FloorDefinition floor = Floor(id);
            return floor.origin + (Vector2)Vector2Int.FloorToInt(position - floor.origin) + Vector2.one * .5f;
        }

        // Role: 개발용 재준비 요청에서 시설 초기 상태와 표현을 다시 만든다.
        public void RestoreInitialFacilities()
        {
            foreach (GameObject view in facilityViews.Values) { view.SetActive(false); Destroy(view); }
            facilityViews.Clear();
            facilityColors.Clear();
            state.facilities = NewState().facilities;
            foreach (FacilityState item in state.facilities) CreateView(item);
            RefreshFacilityViews();
        }

        // Role: 경로 전체를 재탐색하지 않고 인접 계단 연결만 확인한다.
        public bool AreFloorsConnected(string from, string to)
        {
            FloorDefinition floor = Floor(from);
            return floor?.connectedFloors != null && Array.IndexOf(floor.connectedFloors, to) >= 0;
        }

        // Role: 비활성 층에서도 원형 선원과 고정 장애물의 여유 공간을 검사한다.
        public bool HasCrewClearance(string floorId, Vector2 position, float radius)
        {
            FloorDefinition floor = Floor(floorId);
            if (floor == null || position.x - radius < floor.origin.x || position.y - radius < floor.origin.y
                || position.x + radius > floor.origin.x + floor.size.x || position.y + radius > floor.origin.y + floor.size.y) return false;
            Vector2Int minimum = Vector2Int.FloorToInt(position - Vector2.one * radius - floor.origin);
            Vector2Int maximum = Vector2Int.FloorToInt(position + Vector2.one * radius - floor.origin);
            for (int x = minimum.x; x <= maximum.x; x++)
                for (int y = minimum.y; y <= maximum.y; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!InRects(floor.blocked, cell) && !Occupied(floorId, cell, state.facilities)) continue;
                    Vector2 lower = floor.origin + (Vector2)cell;
                    Vector2 nearest = Vector2.Max(lower, Vector2.Min(position, lower + Vector2.one));
                    if ((position - nearest).sqrMagnitude < radius * radius) return false;
                }
            return true;
        }

        // Role: Nav 재탐색 없이 짧은 이동 선분의 정적 통행 가능 여부를 검사한다.
        public bool IsCrewSegmentClear(string floorId, Vector2 from, Vector2 to, float radius)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / Mathf.Max(.05f, radius * .5f)));
            for (int i = 1; i <= samples; i++)
                if (!HasCrewClearance(floorId, Vector2.Lerp(from, to, (float)i / samples), radius)) return false;
            return true;
        }

        // Role: 시설 주변 접근점의 Cell 좌표를 반환한다.
        public bool TryGetAccessCell(string id, out Vector2 position)
        {
            bool found = TryGetAccessPosition(id, out position);
            position /= CellSize;
            return found;
        }

        // Role: 재탐색 없이 시설 외곽 접근점에 도착했는지 검사한다.
        public bool IsAtFacilityAccess(string id, Vector2 position, float tolerance)
        {
            FacilityState item = GetFacility(id);
            if (item == null) return false;
            FloorDefinition floor = Floor(item.floorId);
            FacilityDefinition kind = Kind(item.definitionId);
            var area = new RectInt(item.x, item.y, kind.size.x, kind.size.y);
            foreach (Vector2Int cell in area.allPositionsWithin)
                foreach (Vector2Int step in Steps)
                {
                    Vector2Int next = cell + step;
                    if (!area.Contains(next) && Walkable(floor, next, state.facilities)
                        && Vector2.Distance(position, floor.origin + (Vector2)next + Vector2.one * .5f) <= tolerance) return true;
                }
            return false;
        }

        // Role: 고정 순서 BFS로 연결된 층 목록을 구한다.
        public bool TryGetFloorRoute(string from, string to, out List<string> route)
        {
            route = new List<string>();
            if (Floor(from) == null || Floor(to) == null) return false;
            var parents = new Dictionary<string, string> { [from] = null };
            var queue = new Queue<string>(); queue.Enqueue(from);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                if (current == to) break;
                foreach (string next in Floor(current).connectedFloors ?? Array.Empty<string>())
                    if (!parents.ContainsKey(next)) { parents.Add(next, current); queue.Enqueue(next); }
            }
            if (!parents.ContainsKey(to)) return false;
            for (string current = to; current != null; current = parents[current]) route.Add(current);
            route.Reverse();
            return true;
        }

        // Role: 시설·고정 장애물을 피하는 Cell 중심 경로를 구한다.
        public bool TryFindPath(string floorId, Vector2 from, Vector2 to, out List<Vector2> path)
        {
            path = new List<Vector2>();
            FloorDefinition floor = Floor(floorId);
            if (floor == null) return false;
            Vector2Int start = Vector2Int.FloorToInt(from - floor.origin);
            Vector2Int goal = Vector2Int.FloorToInt(to - floor.origin);
            if (!Walkable(floor, start, state.facilities) || !Walkable(floor, goal, state.facilities)) return false;
            var parents = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
            var queue = new Queue<Vector2Int>(); queue.Enqueue(start);
            while (queue.Count > 0 && !parents.ContainsKey(goal))
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int step in Steps)
                {
                    Vector2Int next = current + step;
                    if (Walkable(floor, next, state.facilities) && !parents.ContainsKey(next))
                    { parents.Add(next, current); queue.Enqueue(next); }
                }
            }
            if (!parents.ContainsKey(goal)) return false;
            for (Vector2Int current = goal; ; current = parents[current])
            {
                path.Add(floor.origin + (Vector2)current + Vector2.one * .5f);
                if (current == start) break;
            }
            path.Reverse();
            if ((path[0] - from).sqrMagnitude < .000001f) path.RemoveAt(0);
            return true;
        }

        // Role: 기능이 남아 있는 시설의 부분 손상만 수리한다.
        public bool RepairFacility(string id, float amount)
        {
            FacilityState item = GetFacility(id);
            FacilityDefinition kind = item == null ? null : Kind(item.definitionId);
            if (kind == null || !kind.repairable || item.durability <= 0 || item.durability >= kind.maxDurability || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0) return false;
            item.durability = Mathf.Min(kind.maxDurability, item.durability + amount);
            return true;
        }

        // Role: 내구도에 따른 색을 갱신하고 제거된 시설의 표현을 정리한다.
        public void RefreshFacilityViews()
        {
            var removed = new List<string>();
            foreach (var pair in facilityViews)
            {
                FacilityState item = GetFacility(pair.Key);
                if (item == null) { Destroy(pair.Value); removed.Add(pair.Key); continue; }
                float brightness = .25f + .75f * item.durability / Kind(item.definitionId).maxDurability;
                SpriteRenderer[] renderers = pair.Value.GetComponentsInChildren<SpriteRenderer>(true);
                Color[] colors = facilityColors[pair.Key];
                for (int i = 0; i < renderers.Length; i++)
                {
                    Color color = colors[i];
                    renderers[i].color = new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
                }
            }
            foreach (string id in removed) { facilityViews.Remove(id); facilityColors.Remove(id); }
        }

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
            facilityViews.Add(item.id, instance);
            SpriteRenderer[] renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            var colors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) colors[i] = renderers[i].color;
            facilityColors.Add(item.id, colors);
        }
    }
}
