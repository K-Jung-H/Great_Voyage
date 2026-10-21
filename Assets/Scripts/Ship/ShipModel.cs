using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class ShipModel
    {
        private ShipDefinition definition;
        public ShipDefinition Definition => definition;
        private readonly PlayerLocation player;
        public ShipModel(ShipDefinition definition, PlayerLocation player)
        {
            this.definition = definition;
            this.player = player;
        }

        public ShipState State => state;

        private ShipState state;
        private bool aboard = true;
        public string ActiveFloor => state.activeFloor;
        public IReadOnlyList<FacilityState> Facilities => state.facilities;
        public float CellSize => definition.cellSize;
        public float SailingFactor
        {
            get
            {
                var sails = state.facilities.Where(item => Kind(item.definitionId).role == FacilityRole.Sail).ToArray();
                return sails.Length == 0 ? 1 : Mathf.Lerp(.35f, 1, sails.Average(item => item.sailDeployed ? item.durability / MaximumDurability(item) : 0));
            }
        }

        private static readonly Vector2Int[] Steps =
        {
            Vector2Int.right,
            Vector2Int.up,
            Vector2Int.left,
            Vector2Int.down
        };
        // Role: 저장된 함선 상태를 검증하고 화면을 상태로부터 구성한다.
        public ShipState Initialize(ShipState saved)
        {
            ValidateDefinition();
            state = saved ?? NewState();
            state.unlockedTiles ??= new List<string>();
            if (state.facilities == null)
                throw new InvalidDataException("시설 목록이 누락되었습니다.");
            foreach (var initial in definition.initialFacilities ?? Array.Empty<FacilityState>())
                if ((!string.IsNullOrEmpty(initial.fixedTileId) && !state.facilities.Exists(item => item.fixedTileId == initial.fixedTileId && item.floorId == initial.floorId)) || (state.layoutVersion == 0 && Kind(initial.definitionId)?.essential == true && !state.facilities.Exists(item => item.definitionId == initial.definitionId)))
                    state.facilities.Add(JsonUtility.FromJson<FacilityState>(JsonUtility.ToJson(initial)));
            state.layoutVersion = 1;
            if (state.definitionId != definition.id || Floor(state.activeFloor) == null || state.facilities == null || state.visits == null)
                throw new InvalidDataException("함선 또는 현재 층 상태가 유효하지 않습니다.");
            var ids = new HashSet<string>();
            var accepted = new List<FacilityState>();
            foreach (FacilityState item in state.facilities)
            {
                if (item != null && item.level == 0) item.level = 1;
                if (item == null || string.IsNullOrEmpty(item.id) || !ids.Add(item.id))
                    throw new InvalidDataException("시설 ID가 누락되거나 중복됩니다.");
                if (!ValidatePlacement(item, accepted, out string reason))
                    throw new InvalidDataException(reason);
                accepted.Add(item);
            }

            foreach (FloorDefinition floor in definition.floors)
                if (!CanReachFacilities(floor, accepted))
                    throw new InvalidDataException("계단에서 접근할 수 없는 시설 배치입니다.");
            ids.Clear();
            foreach (FloorVisit visit in state.visits)
                if (visit == null || !ids.Add(visit.floorId) || !ValidPosition(visit.floorId, new Vector2(visit.x, visit.y)))
                    throw new InvalidDataException("층별 방문 위치가 유효하지 않습니다.");
            return state;
        }

        // Role: 필수 층·시설 정의와 화면 참조를 검증한다.
        private void ValidateDefinition()
        {
            if (definition == null || float.IsNaN(definition.cellSize) || float.IsInfinity(definition.cellSize) || definition.cellSize <= 0 || definition.floors == null || definition.facilities == null || player == null)
                throw new InvalidDataException("함선 정의 또는 화면 참조가 연결되지 않았습니다.");
            var ids = new HashSet<string>();
            foreach (FloorDefinition floor in definition.floors)
            {
                if (floor == null || string.IsNullOrEmpty(floor.id) || !ids.Add(floor.id) || floor.size.x <= 0 || floor.size.y <= 0 || floor.size.x > 128 || floor.size.y > 128)
                    throw new InvalidDataException("층 정의가 유효하지 않습니다.");
                var tileIds = new HashSet<string>();
                foreach (var tile in floor.specialTiles ?? Array.Empty<SpecialTileDefinition>())
                    if (tile == null || string.IsNullOrEmpty(tile.id) || !tileIds.Add(tile.id) || tile.cells.width <= 0 || tile.cells.height <= 0 || tile.cells.xMin < 0 || tile.cells.yMin < 0 || tile.cells.xMax > floor.size.x || tile.cells.yMax > floor.size.y || !Enum.IsDefined(typeof(TilePassability), tile.passability))
                        throw new InvalidDataException("특수 타일 분류/영역이 유효하지 않습니다.");
            }

            ids.Clear();
            foreach (FloorDefinition floor in definition.floors)
                if (floor.connectedFloors != null)
                    foreach (string connected in floor.connectedFloors)
                        if (connected == floor.id || Floor(connected) == null)
                            throw new InvalidDataException("계단의 논리 층 연결이 유효하지 않습니다.");
            foreach (FacilityDefinition facility in definition.facilities)
                if (facility == null || string.IsNullOrEmpty(facility.id) || !ids.Add(facility.id) || facility.size.x <= 0 || facility.size.y <= 0 || facility.prefab == null || facility.maxDurability <= 0 || !Enum.IsDefined(typeof(TilePassability), facility.passability))
                    throw new InvalidDataException("시설 정의가 유효하지 않습니다.");
        }

        // Role: 정의 자산을 수정하지 않고 독립적인 새 함선 상태를 만든다.
        private ShipState NewState()
        {
            var result = new ShipState
            {
                definitionId = definition.id,
                activeFloor = definition.startingFloor,
                layoutVersion = 1
            };
            if (definition.initialFacilities != null)
                foreach (FacilityState item in definition.initialFacilities)
                    result.facilities.Add(JsonUtility.FromJson<FacilityState>(JsonUtility.ToJson(item)));
            return result;
        }

        // Role: 이후 배치 기능에서 사용할 영역·점유·접근 가능 조건을 함께 검사한다.
        public bool CanPlace(FacilityState candidate, out string reason)
        {
            if (state == null)
            {
                reason = "함선이 초기화되지 않았습니다.";
                return false;
            }

            if (candidate == null || string.IsNullOrEmpty(candidate.id) || state.facilities.Exists(f => f.id == candidate.id))
            {
                reason = "시설 ID가 누락되거나 중복됩니다.";
                return false;
            }

            if (!ValidatePlacement(candidate, state.facilities, out reason))
                return false;
            var proposed = new List<FacilityState>(state.facilities)
            {
                candidate
            };
            if (!CanReachFacilities(Floor(candidate.floorId), proposed))
            {
                reason = "시설 접근 통로를 막습니다.";
                return false;
            }

            return true;
        }

        // Role: 시설 크기·층 제한·내구도·점유 겹침을 검사한다.
        private bool ValidatePlacement(FacilityState item, List<FacilityState> occupied, out string reason)
        {
            reason = "유효하지 않은 시설 배치입니다.";
            FloorDefinition floor = Floor(item.floorId);
            FacilityDefinition kind = Kind(item.definitionId);
            if (floor == null || kind == null || kind.allowedFloors == null || Array.IndexOf(kind.allowedFloors, item.floorId) < 0)
                return false;
            if (item.level < 1 || item.level > kind.maximumLevel)
                return false;
            if (!string.IsNullOrEmpty(item.fixedTileId))
            {
                var fixedTile = Array.Find(floor.specialTiles ?? Array.Empty<SpecialTileDefinition>(), tile => tile.id == item.fixedTileId);
                if (fixedTile == null || fixedTile.cells.position != new Vector2Int(item.x, item.y) || fixedTile.cells.size != kind.size)
                    return false;
            }
            if (float.IsNaN(item.durability) || float.IsInfinity(item.durability) || item.durability < 0 || item.durability > kind.maxDurability * item.level)
                return false;
            if (item.x < 0 || item.y < 0 || item.x > floor.size.x - kind.size.x || item.y > floor.size.y - kind.size.y)
                return false;
            foreach (Vector2Int cell in new RectInt(item.x, item.y, kind.size.x, kind.size.y).allPositionsWithin)
                if (InRects(floor.blocked, cell) || SpecialOccupied(floor, cell, false, item.fixedTileId) || InRects(floor.reserved, cell) || Occupied(item.floorId, cell, occupied))
                    return false;
            reason = null;
            return true;
        }

        // Role: 계단 진입 칸에서 갈 수 있는 영역을 구하고 모든 시설의 접근 칸을 확인한다.
        private bool CanReachFacilities(FloorDefinition floor, List<FacilityState> items)
        {
            HashSet<Vector2Int> reachable = Reachable(floor, items);
            if (reachable.Count == 0)
                return false;
            foreach (FacilityState item in items)
                if (item.floorId == floor.id && !TryAccess(item, reachable, out _))
                    return false;
            return true;
        }

        // Role: 현재 배치에서 특정 시설 옆의 도달 가능한 지역 좌표를 반환한다.
        public bool TryGetAccessPosition(string facilityId, out Vector2 position)
        {
            position = default;
            FacilityState item = state?.facilities.Find(f => f.id == facilityId);
            if (item == null)
                return false;
            FloorDefinition floor = Floor(item.floorId);
            if (!TryAccess(item, Reachable(floor, state.facilities), out Vector2Int cell))
                return false;
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
                    if (!area.Contains(cell + step) && reachable.Contains(cell + step))
                    {
                        access = cell + step;
                        return true;
                    }

            access = default;
            return false;
        }

        // Role: 배치 격자에서 이동 가능한 영역을 너비 우선으로 계산한다.
        private HashSet<Vector2Int> Reachable(FloorDefinition floor, List<FacilityState> items)
        {
            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            Vector2Int start = Vector2Int.FloorToInt(floor.entry - floor.origin);
            if (!Walkable(floor, start, items))
                return visited;
            visited.Add(start);
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int step in Steps)
                {
                    Vector2Int next = current + step;
                    if (Walkable(floor, next, items) && visited.Add(next))
                        queue.Enqueue(next);
                }
            }

            return visited;
        }

        // Role: 지정 칸이 바닥 안이며 시설이나 고정 사물로 막히지 않았는지 판정한다.
        private bool Walkable(FloorDefinition floor, Vector2Int cell, List<FacilityState> items) => cell.x >= 0 && cell.y >= 0 && cell.x < floor.size.x && cell.y < floor.size.y && !InRects(floor.blocked, cell) && !SpecialOccupied(floor, cell, true) && !Occupied(floor.id, cell, items, true);
        // Role: 시설 인스턴스들의 점유 칸을 조회한다.
        private bool Occupied(string floorId, Vector2Int cell, List<FacilityState> items, bool blockingOnly = false)
        {
            foreach (FacilityState item in items)
            {
                if (item.floorId != floorId)
                    continue;
                FacilityDefinition kind = Kind(item.definitionId);
                if (kind != null && (!blockingOnly || kind.passability == TilePassability.Impassable) && new RectInt(item.x, item.y, kind.size.x, kind.size.y).Contains(cell))
                    return true;
            }

            return false;
        }

        // Role: 예약 또는 차단 영역에 칸이 속하는지 확인한다.
        private static bool InRects(RectInt[] regions, Vector2Int cell) => regions != null && Array.Exists(regions, area => area.Contains(cell));
        // Role: 층 ID에 대응하는 정의를 찾는다.
        public FloorDefinition GetFloorDefinition(string id) => Floor(id);
        private bool SpecialOccupied(FloorDefinition floor, Vector2Int cell, bool blockingOnly, string ignored = null) => floor.specialTiles != null && Array.Exists(floor.specialTiles, tile => tile.id != ignored && tile.cells.Contains(cell) && !(tile.unlockable && state.unlockedTiles.Contains(floor.id + ":" + tile.id)) && (!blockingOnly || tile.unlockable || tile.passability == TilePassability.Impassable));
        public bool IsTileBlocking(string floorId, string tileId)
        {
            var tile = Array.Find(Floor(floorId)?.specialTiles ?? Array.Empty<SpecialTileDefinition>(), value => value.id == tileId);
            return tile != null && (tile.unlockable ? !state.unlockedTiles.Contains(floorId + ":" + tileId) : tile.passability == TilePassability.Impassable);
        }

        public bool TryEdit(FacilityState replacement, string removedId, out string reason)
        {
            reason = "시설 ID 또는 배치가 유효하지 않습니다.";
            var old = GetFacility(removedId);
            if (old != null && !string.IsNullOrEmpty(old.fixedTileId) && (replacement == null || replacement.x != old.x || replacement.y != old.y || replacement.floorId != old.floorId || replacement.definitionId != old.definitionId))
            {
                reason = "함선 고정 시설은 이동·제거할 수 없습니다.";
                return false;
            }
            var proposed = state.facilities.Where(value => value.id != removedId).ToList();
            if (replacement != null)
            {
                if (string.IsNullOrEmpty(replacement.id) || proposed.Exists(value => value.id == replacement.id) || !ValidatePlacement(replacement, proposed, out reason))
                    return false;
                proposed.Add(replacement);
            }
            foreach (var floor in definition.floors)
                if (!CanReachFacilities(floor, proposed))
                {
                    reason = "시설 접근 경로를 막습니다.";
                    return false;
                }
            state.facilities = proposed;
            reason = null;
            return true;
        }

        public void ChangeDefinition(ShipDefinition target, ShipState targetState)
        {
            var previousDefinition = definition;
            var previousState = state;
            try { definition = target; Initialize(targetState); }
            catch { definition = previousDefinition; state = previousState; throw; }
        }
        private FloorDefinition Floor(string id) => Array.Find(definition.floors, f => f.id == id);
        // Role: 시설 종류 ID에 대응하는 정의를 찾는다.
        private FacilityDefinition Kind(string id) => Array.Find(definition.facilities, f => f.id == id);
        // Role: 안정 시설 ID로 런타임 상태를 조회한다.
        public FacilityState GetFacility(string id) => state.facilities.Find(f => f.id == id);
        // Role: 시설 종류의 크기·기능·수리 조건을 조회한다.
        public FacilityDefinition GetFacilityDefinition(string id) => Kind(id);
        public float MaximumDurability(FacilityState item) => Kind(item.definitionId).maxDurability * item.level;
        // Role: 해당 층의 표시 여부를 상태에서 판단한다.
        private string presentationFloor;
        public void SetPresentationFloor(string floorId) => presentationFloor = floorId;
        public bool IsFloorVisible(string id) => (aboard || presentationFloor != null) && (presentationFloor ?? state.activeFloor) == id;
        // Role: 선원 Cell 좌표가 실제 통행 가능한 칸인지 검사한다.
        public bool IsWalkablePosition(string id, Vector2 position)
        {
            FloorDefinition floor = Floor(id);
            return floor != null && !float.IsNaN(position.x) && !float.IsNaN(position.y) && !float.IsInfinity(position.x) && !float.IsInfinity(position.y) && Walkable(floor, Vector2Int.FloorToInt(position - floor.origin), state.facilities);
        }

        // Role: 선원용 계단 진입점의 Cell 좌표를 반환한다.
        public Vector2 EntryPosition(string id) => Floor(id).entry;
        // Role: 현재 층의 배회 후보를 논리 칸 중심으로 정렬한다.
        public Vector2 CellCenter(string id, Vector2 position)
        {
            FloorDefinition floor = Floor(id);
            return floor.origin + (Vector2)Vector2Int.FloorToInt(position - floor.origin) + Vector2.one * .5f;
        }

        // Role: 경로 전체를 재탐색하지 않고 인접 계단 연결만 확인한다.
        public bool AreFloorsConnected(string from, string to)
        {
            if (definition.stairLinks != null && definition.stairLinks.Length > 0)
                return Array.Exists(definition.stairLinks, link => (link.fromFloor == from && link.toFloor == to) || (link.toFloor == from && link.fromFloor == to));
            FloorDefinition floor = Floor(from);
            return floor?.connectedFloors != null && Array.IndexOf(floor.connectedFloors, to) >= 0;
        }

        // Role: 비활성 층에서도 원형 선원과 고정 장애물의 여유 공간을 검사한다.
        public bool HasCrewClearance(string floorId, Vector2 position, float radius)
        {
            FloorDefinition floor = Floor(floorId);
            if (floor == null || position.x - radius < floor.origin.x || position.y - radius < floor.origin.y || position.x + radius > floor.origin.x + floor.size.x || position.y + radius > floor.origin.y + floor.size.y)
                return false;
            Vector2Int minimum = Vector2Int.FloorToInt(position - Vector2.one * radius - floor.origin);
            Vector2Int maximum = Vector2Int.FloorToInt(position + Vector2.one * radius - floor.origin);
            for (int x = minimum.x; x <= maximum.x; x++)
                for (int y = minimum.y; y <= maximum.y; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!InRects(floor.blocked, cell) && !SpecialOccupied(floor, cell, true) && !Occupied(floorId, cell, state.facilities, true))
                        continue;
                    Vector2 lower = floor.origin + (Vector2)cell;
                    Vector2 nearest = Vector2.Max(lower, Vector2.Min(position, lower + Vector2.one));
                    if ((position - nearest).sqrMagnitude < radius * radius)
                        return false;
                }

            return true;
        }

        // Role: Nav 재탐색 없이 짧은 이동 선분의 정적 통행 가능 여부를 검사한다.
        public bool IsCrewSegmentClear(string floorId, Vector2 from, Vector2 to, float radius)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / Mathf.Max(.05f, radius * .5f)));
            for (int i = 1; i <= samples; i++)
                if (!HasCrewClearance(floorId, Vector2.Lerp(from, to, (float)i / samples), radius))
                    return false;
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
            if (item == null)
                return false;
            FloorDefinition floor = Floor(item.floorId);
            FacilityDefinition kind = Kind(item.definitionId);
            var area = new RectInt(item.x, item.y, kind.size.x, kind.size.y);
            foreach (Vector2Int cell in area.allPositionsWithin)
                foreach (Vector2Int step in Steps)
                {
                    Vector2Int next = cell + step;
                    if (!area.Contains(next) && Walkable(floor, next, state.facilities) && Vector2.Distance(position, floor.origin + (Vector2)next + Vector2.one * .5f) <= tolerance)
                        return true;
                }

            return false;
        }

        // Role: 고정 순서 BFS로 연결된 층 목록을 구한다.
        public bool TryGetFloorRoute(string from, string to, out List<string> route)
        {
            route = new List<string>();
            if (Floor(from) == null || Floor(to) == null)
                return false;
            var parents = new Dictionary<string, string>
            {
                [from] = null
            };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                if (current == to)
                    break;
                foreach (string next in Floor(current).connectedFloors ?? Array.Empty<string>())
                    if (!parents.ContainsKey(next))
                    {
                        parents.Add(next, current);
                        queue.Enqueue(next);
                    }
            }

            if (!parents.ContainsKey(to))
                return false;
            for (string current = to; current != null; current = parents[current])
                route.Add(current);
            route.Reverse();
            return true;
        }

        // Role: 시설·고정 장애물을 피하는 Cell 중심 경로를 구한다.
        public bool TryFindPath(string floorId, Vector2 from, Vector2 to, out List<Vector2> path)
        {
            path = new List<Vector2>();
            FloorDefinition floor = Floor(floorId);
            if (floor == null)
                return false;
            Vector2Int start = Vector2Int.FloorToInt(from - floor.origin);
            Vector2Int goal = Vector2Int.FloorToInt(to - floor.origin);
            if (!Walkable(floor, start, state.facilities) || !Walkable(floor, goal, state.facilities))
                return false;
            var parents = new Dictionary<Vector2Int, Vector2Int>
            {
                [start] = start
            };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            while (queue.Count > 0 && !parents.ContainsKey(goal))
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int step in Steps)
                {
                    Vector2Int next = current + step;
                    if (Walkable(floor, next, state.facilities) && !parents.ContainsKey(next))
                    {
                        parents.Add(next, current);
                        queue.Enqueue(next);
                    }
                }
            }

            if (!parents.ContainsKey(goal))
                return false;
            for (Vector2Int current = goal;; current = parents[current])
            {
                path.Add(floor.origin + (Vector2)current + Vector2.one * .5f);
                if (current == start)
                    break;
            }

            path.Reverse();
            if ((path[0] - from).sqrMagnitude < .000001f)
                path.RemoveAt(0);
            return true;
        }

        // Role: 기능이 남아 있는 시설의 부분 손상만 수리한다.
        public bool RepairFacility(string id, float amount)
        {
            FacilityState item = GetFacility(id);
            FacilityDefinition kind = item == null ? null : Kind(item.definitionId);
            if (kind == null || !kind.repairable || item.durability <= 0 || item.durability >= MaximumDurability(item) || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0)
                return false;
            item.durability = Mathf.Min(MaximumDurability(item), item.durability + amount);
            return true;
        }

        // Role: 층 지역 좌표를 점유 격자 좌표로 변환한다.
        private Vector2Int Cell(FloorDefinition floor, Vector2 position) => Vector2Int.FloorToInt(position / CellSize - floor.origin);
        // Role: 저장 위치의 수치와 현재 층의 경계를 검사한다.
        public bool ValidPosition(string floorId, Vector2 position)
        {
            FloorDefinition floor = Floor(floorId);
            if (floor == null || float.IsNaN(position.x) || float.IsInfinity(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.y))
                return false;
            Vector2Int cell = Cell(floor, position);
            return cell.x >= 0 && cell.y >= 0 && cell.x < floor.size.x && cell.y < floor.size.y;
        }

        // Role: 현재 층의 마지막 지역 위치를 저장 상태에 기록한다.
        public void RememberPosition()
        {
            if (state == null)
                return;
            FloorVisit visit = state.visits.Find(v => v.floorId == state.activeFloor);
            if (visit == null)
            {
                visit = new FloorVisit
                {
                    floorId = state.activeFloor
                };
                state.visits.Add(visit);
            }

            visit.x = player.position.x;
            visit.y = player.position.y;
        }

        // Role: 저장된 시설 배치에 대응하는 표현 인스턴스를 만든다.
        public void SetAboard(bool value) => aboard = value;
        public bool Travel(string floor, Vector2 destination)
        {
            if (!aboard || !AreFloorsConnected(ActiveFloor, floor) || !ValidPosition(floor, destination))
                return false;
            var visit = state.visits.Find(item => item.floorId == floor);
            if (visit != null)
            {
                destination = new Vector2(visit.x, visit.y);
                if (!IsWalkablePosition(floor, destination / CellSize))
                    throw new InvalidDataException("Saved floor visit is blocked");
            }

            RememberPosition();
            state.activeFloor = floor;
            player.position = destination;
            player.linearVelocity = Vector2.zero;
            return true;
        }

        public bool TryConnection(string from, string to, Vector2 near, out Vector2 departure, out Vector2 arrival)
        {
            departure = arrival = default;
            float best = float.PositiveInfinity;
            foreach (var link in definition.stairLinks ?? Array.Empty<StairLinkDefinition>())
            {
                bool forward = link.fromFloor == from && link.toFloor == to, reverse = link.toFloor == from && link.fromFloor == to;
                if (!forward && !reverse)
                    continue;
                Vector2 a = forward ? link.fromPosition : link.toPosition, b = forward ? link.toPosition : link.fromPosition;
                float distance = (a - near).sqrMagnitude;
                if (distance >= best)
                    continue;
                best = distance;
                departure = a;
                arrival = b;
            }

            return !float.IsInfinity(best);
        }

        public bool IsStairTransition(string from, string to, Vector2 position, Vector2 destination, float tolerance)
        {
            foreach (var link in definition.stairLinks ?? Array.Empty<StairLinkDefinition>())
            {
                if (link.fromFloor == from && link.toFloor == to && Vector2.Distance(position, link.fromPosition) <= tolerance && Vector2.Distance(destination, link.toPosition) < .001f)
                    return true;
                if (link.toFloor == from && link.fromFloor == to && Vector2.Distance(position, link.toPosition) <= tolerance && Vector2.Distance(destination, link.fromPosition) < .001f)
                    return true;
            }

            return false;
        }
    }
}
