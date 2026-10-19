using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace GreatVoyage
{
    public sealed class CrewSimulation : MonoBehaviour
    {
        [SerializeField, Tooltip("선원 생성·이동·생활·수리 조정값.")] private CrewSettings settings;
        [SerializeField, Tooltip("시간·정지·저장을 관리하는 세션.")] private GameSession session;
        [SerializeField, Tooltip("층 경로와 시설 상태의 원본.")] private ShipLayout ship;
        [SerializeField, Tooltip("개발용 상태 표시. 정식 빈 선원 구성에서는 생략 가능.")] private Text statusLabel;
        private CrewRosterState state;
        private readonly Dictionary<string, string> claims = new Dictionary<string, string>();
        private readonly Dictionary<string, CrewView> views = new Dictionary<string, CrewView>();
        private readonly RaycastHit2D[] steeringHits = new RaycastHit2D[24];
        private readonly ContactFilter2D steeringFilter = new ContactFilter2D { useTriggers = false };
        private Vector2[] directions;
        private float[] scores;
        private int navigationSearches;
        public bool IsReady => state != null && session.IsReady;
        public float CellSize => ship.CellSize;
        public float BodyRadius => settings.bodyRadius;

        // Role: 저장 상태를 검증하고 행동에서 선점을 복원한 뒤 표시만 생성한다.
        public CrewRosterState Initialize(CrewRosterState saved)
        {
            ValidateSettings();
            directions = new Vector2[settings.steeringDirections];
            scores = new float[directions.Length];
            for (int i = 0; i < directions.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / directions.Length;
                directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            state = saved ?? new CrewRosterState { settingsId = settings.id, food = settings.initialFood, repairTools = settings.initialRepairTools };
            if (saved == null)
                foreach (CrewState member in settings.initialCrew)
                    state.members.Add(JsonUtility.FromJson<CrewState>(JsonUtility.ToJson(member)));
            if (state.settingsId != settings.id || state.food < 0 || state.repairTools < 0 || state.members == null || state.members.Count > 256
                || double.IsNaN(state.remainder) || double.IsInfinity(state.remainder) || state.remainder < 0 || state.remainder >= settings.stepSeconds)
                throw new InvalidDataException("선원 목록 또는 자원·시간 상태가 유효하지 않습니다.");
            state.members.Sort((left, right) => string.Compare(left?.id, right?.id, StringComparison.Ordinal));
            var ids = new HashSet<string>();
            foreach (CrewState member in state.members)
            {
                if (member != null)
                {
                    member.path = new List<CrewWaypoint>();
                    if (member.nextWaypoint != null && !string.IsNullOrEmpty(member.nextWaypoint.floorId)) member.path.Add(member.nextWaypoint);
                    else member.nextWaypoint = null;
                    if (member.destination != null && string.IsNullOrEmpty(member.destination.floorId)) member.destination = null;
                    member.resumeWaypoint = member.nextWaypoint != null;
                    if (IsNavigating(member) || IsReturning(member)) member.repathRemaining = 0;
                }
                ValidateMember(member);
                member.repathRemaining = Mathf.Min(member.repathRemaining, settings.repathInterval);
                if (!ids.Add(member.id)) throw new InvalidDataException("선원 ID가 중복됩니다.");
                if (HasTarget(member))
                {
                    if (!TargetUsable(member) || member.availability != CrewAvailability.Ready || claims.ContainsKey(member.targetId)
                        || member.path.Exists(point => !ship.IsWalkablePosition(point.floorId, point.position))) Cancel(member);
                    else claims.Add(member.targetId, member.id);
                }
                if (IsNavigating(member) && member.destination == null) Cancel(member);
                CrewView view = Instantiate(settings.prefab, transform);
                view.name = member.id;
                view.Bind(member, this);
                views.Add(member.id, view);
            }
            session.Pauses.Changed += StopWhenPaused;
            ship.RefreshFacilityViews();
            return state;
        }

        // Role: 영·음수 간격이나 비정상 생활 수치로 진행이 멈추지 않도록 검사한다.
        private void ValidateSettings()
        {
            if (settings == null || session == null || ship == null || settings.prefab == null || settings.initialCrew == null || string.IsNullOrEmpty(settings.id))
                throw new InvalidDataException("선원 설정 또는 필수 참조가 누락됐습니다.");
            foreach (float value in new[] { settings.stepSeconds, settings.moveSpeed, settings.hungerRate, settings.mealThreshold, settings.mealSeconds, settings.mealRecovery,
                settings.energyDrain, settings.sleepThreshold, settings.wakeThreshold, settings.sleepRecovery, settings.repairSeconds, settings.repairAmount, settings.starvationRate,
                settings.repathInterval, settings.lookAhead, settings.avoidanceWeight, settings.acceleration, settings.bodyRadius, settings.clearance,
                settings.waypointRadius, settings.arrivalRadius, settings.predictionSeconds, settings.normalMass, settings.workingMass,
                settings.workReturnSpeed, settings.workReturnAcceleration, settings.workRadius, settings.wanderDistance })
                if (!Finite(value) || value <= 0) throw new InvalidDataException("선원 조정값은 양의 유한 수치여야 합니다.");
            if (settings.stepSeconds < .05f || settings.stepSeconds > 1 || settings.repairSeconds < settings.stepSeconds || settings.mealSeconds < settings.stepSeconds
                || settings.mealThreshold > 100 || settings.mealRecovery > 100 || settings.sleepThreshold >= settings.wakeThreshold || settings.wakeThreshold > 100
                || settings.wakeHour < 0 || settings.sleepHour > 23 || settings.sleepHour <= settings.wakeHour || settings.initialFood < 0 || settings.initialRepairTools < 0
                || settings.steeringDirections < 8 || settings.steeringDirections > 32 || settings.repathInterval < settings.stepSeconds
                || !Finite(settings.momentumWeight) || settings.momentumWeight < 0 || settings.momentumWeight > 1
                || settings.bodyRadius + settings.clearance >= .5f || settings.arrivalRadius >= settings.waypointRadius || settings.waypointRadius >= .5f)
                throw new InvalidDataException("선원 생활·작업 설정 범위가 유효하지 않습니다.");
            if (settings.workingMass <= settings.normalMass || settings.workRadius <= settings.arrivalRadius)
                throw new InvalidDataException("작업 중 질량 또는 복귀 범위가 유효하지 않습니다.");
        }

        // Role: 저장된 위치·욕구·행동·경로의 기본 무결성을 검사한다.
        private void ValidateMember(CrewState member)
        {
            if (member == null || string.IsNullOrEmpty(member.id) || !ship.IsWalkablePosition(member.floorId, member.position)
                || !Percent(member.health) || !Percent(member.happiness) || !Percent(member.hunger) || !Percent(member.energy)
                || !Finite(member.remainingSeconds) || member.remainingSeconds < 0 || member.path == null || member.path.Count > 16384
                || !Finite(member.velocity.x) || !Finite(member.velocity.y) || !Finite(member.repathRemaining) || member.repathRemaining < 0
                || member.wanderSequence < 0 || (member.hasWorkPosition && !ship.ValidPosition(member.floorId, member.workPosition * CellSize))
                || !Enum.IsDefined(typeof(CrewActivity), member.activity) || !Enum.IsDefined(typeof(CrewAvailability), member.availability)
                || !Enum.IsDefined(typeof(CrewSpecialty), member.specialty) || !Enum.IsDefined(typeof(CrewDuty), member.primaryDuty)
                || (member.primaryDuty == CrewDuty.Maintenance && member.specialty != CrewSpecialty.Maintenance)
                || (member.health == 0 && member.availability != CrewAvailability.Dead))
                throw new InvalidDataException("저장된 선원 상태가 유효하지 않습니다.");
            foreach (CrewWaypoint point in member.path)
                if (point == null || !ship.ValidPosition(point.floorId, point.position * ship.CellSize))
                    throw new InvalidDataException("저장된 선원 경로가 유효하지 않습니다.");
            if (HasTarget(member) && string.IsNullOrEmpty(member.targetId)) throw new InvalidDataException("선원의 행동 대상이 누락됐습니다.");
            foreach (CrewWaypoint point in new[] { member.destination, member.nextWaypoint })
                if (point != null && !ship.ValidPosition(point.floorId, point.position * CellSize)) throw new InvalidDataException("선원의 이동 목적지가 유효하지 않습니다.");
            if (!HasTarget(member) && !string.IsNullOrEmpty(member.targetId)) throw new InvalidDataException("선원 행동과 대상이 일치하지 않습니다.");
            if (!IsNavigating(member) && !IsReturning(member) && member.path.Count > 0)
                throw new InvalidDataException("이동하지 않는 선원에게 남은 경로가 있습니다.");
            if (member.activity == CrewActivity.Repairing && member.remainingSeconds > settings.repairSeconds)
                throw new InvalidDataException("선원의 수리 진행 시간이 유효하지 않습니다.");
            if (member.activity == CrewActivity.Eating && member.remainingSeconds > settings.mealSeconds)
                throw new InvalidDataException("선원의 식사 진행 시간이 유효하지 않습니다.");
        }

        // Role: 저장 가능한 잔여 시간을 유지하며 일정 간격·고정 선원 순서로 갱신한다.
        public void Advance(float seconds, double now)
        {
            if (!IsReady || session.IsPaused) return;
            state.remainder += seconds;
            while (state.remainder >= settings.stepSeconds)
            {
                state.remainder -= settings.stepSeconds;
                double simulatedTime = now - state.remainder;
                float hour = (float)(simulatedTime % GameSession.SecondsPerDay / GameSession.SecondsPerDay * 24);
                bool night = hour >= settings.sleepHour || hour < settings.wakeHour;
                foreach (CrewState member in state.members) Step(member, settings.stepSeconds, night);
                ship.RefreshFacilityViews();
            }
        }

        // Role: 식사·취침·일반 업무 우선순위에 따라 한 선원의 상태를 진행한다.
        private void Step(CrewState member, float seconds, bool night)
        {
            if (member.availability != CrewAvailability.Ready) { Cancel(member); return; }
            if (member.activity != CrewActivity.Sleeping)
            {
                member.hunger = Mathf.Min(100, member.hunger + settings.hungerRate * seconds);
                member.energy = Mathf.Max(0, member.energy - settings.energyDrain * seconds);
            }
            if (member.hunger >= 100)
            {
                member.health = Mathf.Max(1, member.health - settings.starvationRate * seconds);
                member.happiness = Mathf.Max(0, member.happiness - settings.starvationRate * seconds);
            }
            if (member.activity == CrewActivity.Eating)
            {
                if (!AtWorkPosition(member)) return;
                member.remainingSeconds = Mathf.Max(0, member.remainingSeconds - seconds);
                if (member.remainingSeconds == 0) { member.hunger = Mathf.Max(0, member.hunger - settings.mealRecovery); Cancel(member); }
                return;
            }
            if (member.hunger >= settings.mealThreshold && state.food > 0)
            {
                Cancel(member);
                state.food--;
                member.activity = CrewActivity.Eating;
                member.remainingSeconds = settings.mealSeconds;
                BeginWorkPosition(member);
                return;
            }
            bool sleeping = member.activity == CrewActivity.GoingToBed || member.activity == CrewActivity.Sleeping;
            bool needsSleep = night || member.energy <= settings.sleepThreshold || (sleeping && member.energy < settings.wakeThreshold);
            if (needsSleep)
            {
                if (!sleeping || !OwnsUsableTarget(member))
                {
                    Cancel(member);
                    TryStartTask(member, true);
                    return;
                }
                if (member.activity == CrewActivity.Sleeping && AtWorkPosition(member)) member.energy = Mathf.Min(100, member.energy + settings.sleepRecovery * seconds);
                return;
            }
            if (sleeping) { Cancel(member); return; }
            if (member.activity == CrewActivity.GoingToRepair || member.activity == CrewActivity.Repairing)
            {
                if (!OwnsUsableTarget(member) || state.repairTools == 0) { Cancel(member); return; }
                if (member.activity == CrewActivity.GoingToRepair)
                {
                    return;
                }
                if (!AtWorkPosition(member)) return;
                member.remainingSeconds = Mathf.Max(0, member.remainingSeconds - seconds);
                if (member.remainingSeconds == 0)
                {
                    if (ship.RepairFacility(member.targetId, settings.repairAmount)) state.repairTools--;
                    member.remainingSeconds = settings.repairSeconds;
                    if (!TargetUsable(member) || state.repairTools == 0) Cancel(member);
                }
                return;
            }
            if (member.activity == CrewActivity.Wandering)
            {
                bool workExists = member.specialty == CrewSpecialty.Maintenance && state.repairTools > 0;
                if (workExists)
                {
                    workExists = false;
                    foreach (FacilityState item in ship.Facilities)
                    {
                        FacilityDefinition kind = ship.GetFacilityDefinition(item.definitionId);
                        if (!claims.ContainsKey(item.id) && kind.repairable && item.durability > 0 && item.durability < kind.maxDurability) { workExists = true; break; }
                    }
                }
                if (!workExists) return;
                Cancel(member);
            }
            if (member.repathRemaining == 0 && !TryStartTask(member, false)) TryStartWander(member);
        }

        // Role: 안정 시설 순서로 자격·자재·접근·선점을 확인하고 대표 업무를 선택한다.
        private bool TryStartTask(CrewState member, bool bed)
        {
            if (member.repathRemaining > 0) return false;
            member.repathRemaining = settings.repathInterval;
            if (!bed && (member.specialty != CrewSpecialty.Maintenance || state.repairTools == 0)) return false;
            var facilities = new List<FacilityState>(ship.Facilities);
            facilities.Sort((left, right) => string.Compare(left.id, right.id, StringComparison.Ordinal));
            foreach (FacilityState item in facilities)
            {
                FacilityDefinition kind = ship.GetFacilityDefinition(item.definitionId);
                if (claims.ContainsKey(item.id) || item.durability <= 0 || (bed ? !kind.usableAsBed : !kind.repairable || item.durability >= kind.maxDurability)) continue;
                if (!ship.TryGetAccessCell(item.id, out Vector2 access) || !BuildPath(member, item.floorId, access, out List<CrewWaypoint> path)) continue;
                claims.Add(item.id, member.id);
                member.targetId = item.id;
                member.destination = new CrewWaypoint { floorId = item.floorId, position = access };
                member.resumeWaypoint = false;
                member.path = path;
                member.activity = bed ? CrewActivity.GoingToBed : CrewActivity.GoingToRepair;
                return true;
            }
            return false;
        }

        // Role: 층별 통행 경로를 계단 진입점으로 연결한다.
        private bool BuildPath(CrewState member, string targetFloor, Vector2 target, out List<CrewWaypoint> result)
        {
            navigationSearches++;
            result = new List<CrewWaypoint>();
            if (!ship.TryGetFloorRoute(member.floorId, targetFloor, out List<string> floors)) return false;
            Vector2 from = member.position;
            for (int i = 0; i < floors.Count; i++)
            {
                Vector2 to = i == floors.Count - 1 ? target : ship.EntryPosition(floors[i]);
                if (!ship.TryFindPath(floors[i], from, to, out List<Vector2> segment)) return false;
                if (segment.Count > 1 && ship.IsCrewSegmentClear(floors[i], from, segment[1], settings.bodyRadius + settings.clearance)) segment.RemoveAt(0);
                foreach (Vector2 position in segment) result.Add(new CrewWaypoint { floorId = floors[i], position = position });
                if (i < floors.Count - 1)
                {
                    from = ship.EntryPosition(floors[i + 1]);
                    result.Add(new CrewWaypoint { floorId = floors[i + 1], position = from });
                }
            }
            return true;
        }

        // Role: 일이 없으면 가까운 안전한 목적지를 정해 제한된 주기로 배회한다.
        private void TryStartWander(CrewState member)
        {
            member.repathRemaining = settings.repathInterval;
            int seed = 0;
            foreach (char character in member.id) seed = (seed * 31 + character) & 0x7fffffff;
            int sequence = member.wanderSequence;
            member.wanderSequence = sequence == int.MaxValue ? 0 : sequence + 1;
            for (int i = 0; i < directions.Length; i++)
            {
                int index = (int)(((long)seed + (long)sequence * 7 + i * 5) % directions.Length);
                float distance = settings.wanderDistance * (.5f + .25f * (sequence % 3));
                Vector2 target = ship.CellCenter(member.floorId, member.position + directions[index] * distance);
                if (Vector2.Distance(target, member.position) < 1 || !ship.IsCrewSegmentClear(member.floorId, member.position, target, settings.bodyRadius + settings.clearance)) continue;
                if (!BuildPath(member, member.floorId, target, out List<CrewWaypoint> path)) return;
                member.activity = CrewActivity.Wandering;
                member.destination = new CrewWaypoint { floorId = member.floorId, position = target };
                member.path = path;
                member.resumeWaypoint = false;
                return;
            }
        }

        // Role: 생활 판단과 독립적으로 물리 간격에서 연속 이동을 갱신한다.
        private void FixedUpdate()
        {
            if (!IsReady) return;
            foreach (CrewState member in state.members) views[member.id].SynchronizeVisibility(IsVisible(member));
            CapturePositions();
            if (session.IsPaused) { StopWhenPaused(); return; }
            float seconds = Time.fixedDeltaTime;
            foreach (CrewState member in state.members)
            {
                member.repathRemaining = Mathf.Max(0, member.repathRemaining - seconds);
                if (member.availability == CrewAvailability.Ready && IsNavigating(member))
                    Navigate(member, views[member.id], seconds);
                else
                {
                    HoldPosition(member, views[member.id], seconds);
                }
            }
        }

        // Role: 저장 직전과 물리 갱신 경계에서 실제 위치·속도를 수집한다.
        public void CapturePositions()
        {
            if (state == null) return;
            foreach (CrewState member in state.members)
            {
                views[member.id].CaptureState();
                if (IsWorking(member) && member.hasWorkPosition)
                    member.destination = new CrewWaypoint { floorId = member.floorId, position = member.workPosition };
                member.nextWaypoint = (IsNavigating(member) || IsReturning(member)) && member.path.Count > 0
                    ? new CrewWaypoint { floorId = member.path[0].floorId, position = member.path[0].position } : null;
            }
        }

        // Role: 긴 주기 Nav 경로와 짧은 주기 Context steering을 결합한다.
        private void Navigate(CrewState member, CrewView view, float seconds)
        {
            if (member.activity != CrewActivity.Wandering && !OwnsUsableTarget(member)) { Cancel(member); return; }
            if (member.repathRemaining == 0 && !RefreshRoute(member)) return;
            while (member.path.Count > 0)
            {
                CrewWaypoint point = member.path[0];
                if (point.floorId != member.floorId)
                {
                    if (!ship.AreFloorsConnected(member.floorId, point.floorId)
                        || Vector2.Distance(member.position, ship.EntryPosition(member.floorId)) > settings.waypointRadius
                        || Vector2.Distance(point.position, ship.EntryPosition(point.floorId)) > .001f)
                    { Cancel(member); return; }
                    member.floorId = point.floorId;
                    member.position = point.position;
                    member.velocity = Vector2.zero;
                    member.path.RemoveAt(0);
                    view.TeleportToState();
                    view.SynchronizeVisibility(IsVisible(member));
                    continue;
                }
                float tolerance = member.path.Count == 1 ? settings.arrivalRadius : settings.waypointRadius;
                bool passed = Vector2.Distance(member.position, point.position) <= tolerance;
                if (!passed && member.path.Count > 1)
                {
                    CrewWaypoint next = member.path[1];
                    passed = next.floorId == member.floorId && Vector2.Dot(member.position - point.position, next.position - point.position) > 0
                        && ship.IsCrewSegmentClear(member.floorId, member.position, next.position, settings.bodyRadius + settings.clearance);
                }
                if (!passed) break;
                member.path.RemoveAt(0);
            }
            if (member.path.Count == 0)
            {
                view.DriveVelocity(Vector2.zero, settings.acceleration, settings.normalMass);
                if (member.activity == CrewActivity.Wandering) { Cancel(member); return; }
                FacilityState target = ship.GetFacility(member.targetId);
                if (target != null && member.floorId == target.floorId && ship.IsAtFacilityAccess(target.id, member.position, settings.arrivalRadius))
                {
                    bool bed = member.activity == CrewActivity.GoingToBed;
                    member.activity = bed ? CrewActivity.Sleeping : CrewActivity.Repairing;
                    member.remainingSeconds = bed ? 0 : settings.repairSeconds;
                    member.velocity = Vector2.zero;
                    view.ApplyVelocity(Vector2.zero);
                    BeginWorkPosition(member);
                }
                return;
            }
            Vector2 goal = member.path[0].position;
            Vector2 direction = SteeringDirection(member, view, goal);
            float speed = Mathf.Min(settings.moveSpeed, Vector2.Distance(member.position, goal) / seconds);
            Vector2 velocity = Vector2.MoveTowards(member.velocity, direction * speed, settings.acceleration * seconds);
            velocity = Vector2.ClampMagnitude(velocity, speed);
            Vector2 proposed = member.position + velocity * seconds;
            if (!ship.IsCrewSegmentClear(member.floorId, member.position, proposed, settings.bodyRadius + settings.clearance)) velocity = Vector2.zero;
            member.velocity = velocity;
            if (view.IsSimulated) view.DriveVelocity(velocity, settings.acceleration, settings.normalMass);
            else member.position += velocity * seconds;
        }

        // Role: 쿨다운이 끝났을 때만 현재 대상까지의 Nav 경로를 갱신한다.
        private bool RefreshRoute(CrewState member, CrewWaypoint workDestination = null)
        {
            member.repathRemaining = settings.repathInterval;
            CrewWaypoint destination = workDestination ?? member.destination;
            if (workDestination == null && member.activity != CrewActivity.Wandering)
            {
                FacilityState target = ship.GetFacility(member.targetId);
                if (target == null || !ship.TryGetAccessCell(target.id, out Vector2 access)) { Cancel(member); return false; }
                destination = new CrewWaypoint { floorId = target.floorId, position = access };
            }
            if (destination == null) { Cancel(member); return false; }
            CrewState origin = member;
            CrewWaypoint via = member.resumeWaypoint && member.path.Count > 0 ? member.path[0] : null;
            if (via != null)
            {
                bool valid = via.floorId == member.floorId
                    ? ship.IsCrewSegmentClear(member.floorId, member.position, via.position, settings.bodyRadius + settings.clearance)
                    : ship.AreFloorsConnected(member.floorId, via.floorId) && Vector2.Distance(member.position, ship.EntryPosition(member.floorId)) <= settings.waypointRadius;
                if (valid) origin = new CrewState { floorId = via.floorId, position = via.position };
                else via = null;
            }
            if (!BuildPath(origin, destination.floorId, destination.position, out List<CrewWaypoint> path))
            {
                if (workDestination == null) Cancel(member);
                else { member.path.Clear(); member.resumeWaypoint = false; }
                return false;
            }
            if (via != null) path.Insert(0, via);
            member.destination = destination;
            member.path = path;
            member.resumeWaypoint = false;
            return true;
        }

        // Role: 작업을 실제 시작한 위치를 고정 복귀점으로 보관한다.
        private void BeginWorkPosition(CrewState member)
        {
            member.hasWorkPosition = true;
            member.workPosition = member.position;
        }

        // Role: 밀려난 동안에는 작업 진행을 보류한다.
        private bool AtWorkPosition(CrewState member) => !member.hasWorkPosition || Vector2.Distance(member.position, member.workPosition) <= settings.workRadius;

        // Role: 작업자는 무겁게 유지하고 밀려나면 원래 위치로 천천히 복귀한다.
        private void HoldPosition(CrewState member, CrewView view, float seconds)
        {
            bool working = IsWorking(member) && member.availability == CrewAvailability.Ready;
            if (working && !member.hasWorkPosition) BeginWorkPosition(member);
            float acceleration = working ? settings.workReturnAcceleration : settings.acceleration;
            Vector2 desired = Vector2.zero;
            if (working && Vector2.Distance(member.position, member.workPosition) > .025f)
            {
                Vector2 goal = member.workPosition;
                bool clear = ship.IsCrewSegmentClear(member.floorId, member.position, goal, settings.bodyRadius + settings.clearance);
                if (!clear)
                {
                    if (member.repathRemaining == 0) RefreshRoute(member, new CrewWaypoint { floorId = member.floorId, position = member.workPosition });
                    while (member.path.Count > 0 && Vector2.Distance(member.position, member.path[0].position) <= settings.waypointRadius) member.path.RemoveAt(0);
                    if (member.path.Count > 0) goal = member.path[0].position;
                }
                else { member.path.Clear(); member.resumeWaypoint = false; }
                if (clear || member.path.Count > 0)
                    desired = SteeringDirection(member, view, goal) * Mathf.Min(settings.workReturnSpeed, Vector2.Distance(member.position, goal) / seconds);
            }
            else if (working) member.path.Clear();
            member.velocity = Vector2.MoveTowards(member.velocity, desired, acceleration * seconds);
            if (view.IsSimulated) view.DriveVelocity(desired, acceleration, working ? settings.workingMass : settings.normalMass);
            else
            {
                Vector2 next = member.position + member.velocity * seconds;
                if (ship.IsCrewSegmentClear(member.floorId, member.position, next, settings.bodyRadius + settings.clearance)) member.position = next;
                else member.velocity = Vector2.zero;
            }
        }

        // Role: 중간 목적지 interest와 회피 danger를 방향별로 평가한다.
        private Vector2 SteeringDirection(CrewState member, CrewView view, Vector2 goal)
        {
            Vector2 toward = (goal - member.position).normalized;
            Vector2 right = new Vector2(toward.y, -toward.x);
            Vector2 momentum = member.velocity.normalized;
            float distance = Mathf.Min(settings.lookAhead, Vector2.Distance(member.position, goal));
            float bestScore = 0;
            int best = -1;
            for (int i = 0; i < directions.Length; i++)
            {
                Vector2 candidate = directions[i];
                float interest = Mathf.Max(0, Vector2.Dot(candidate, toward));
                float danger = DirectionDanger(member, view, candidate, distance);
                scores[i] = Mathf.Max(0, interest + settings.momentumWeight * Mathf.Max(0, Vector2.Dot(candidate, momentum)) - settings.avoidanceWeight * danger);
                if (scores[i] > bestScore + .00001f || (scores[i] > 0 && Mathf.Abs(scores[i] - bestScore) <= .00001f
                    && (best < 0 || Vector2.Dot(candidate, right) > Vector2.Dot(directions[best], right))))
                { best = i; bestScore = scores[i]; }
            }
            if (best < 0) return Vector2.zero;
            int previous = (best + directions.Length - 1) % directions.Length;
            int next = (best + 1) % directions.Length;
            return (directions[best] * scores[best] + directions[previous] * scores[previous] + directions[next] * scores[next]).normalized;
        }

        // Role: 논리 장애물·예측 선원 위치와 화면 내 물리 장애물의 위험을 합친다.
        private float DirectionDanger(CrewState member, CrewView view, Vector2 direction, float lookAhead)
        {
            if (lookAhead <= .0001f) return 0;
            float nearest = lookAhead;
            float radius = settings.bodyRadius + settings.clearance;
            int samples = Mathf.Max(1, Mathf.CeilToInt(lookAhead / Mathf.Max(.05f, radius * .5f)));
            for (int i = 1; i <= samples; i++)
            {
                float distance = lookAhead * i / samples;
                if (!ship.HasCrewClearance(member.floorId, member.position + direction * distance, radius)) { nearest = distance; break; }
            }
            foreach (CrewState other in state.members)
            {
                if (other == member || other.availability == CrewAvailability.Dead || other.floorId != member.floorId) continue;
                Vector2 relative = other.position + other.velocity * settings.predictionSeconds - member.position;
                float separation = settings.bodyRadius * 2 + settings.clearance;
                float along = Vector2.Dot(relative, direction);
                if (relative.sqrMagnitude < separation * separation)
                {
                    if (along > 0) nearest = 0;
                    continue;
                }
                float perpendicularSquared = Mathf.Max(0, relative.sqrMagnitude - along * along);
                if (along > 0 && perpendicularSquared < separation * separation)
                    nearest = Mathf.Min(nearest, Mathf.Max(0, along - Mathf.Sqrt(separation * separation - perpendicularSquared)));
            }
            if (view.IsSimulated)
            {
                Vector2 origin = member.position * CellSize;
                int count = Physics2D.CircleCast(origin, radius * CellSize, direction, steeringFilter, steeringHits, lookAhead * CellSize);
                for (int i = 0; i < count; i++)
                {
                    RaycastHit2D hit = steeringHits[i];
                    if (hit.collider.GetComponentInParent<CrewView>() != null) continue;
                    if (hit.distance == 0 && Vector2.Dot(direction, origin - hit.collider.ClosestPoint(origin)) > 0) continue;
                    nearest = Mathf.Min(nearest, hit.distance / CellSize);
                }
            }
            return 1 - Mathf.Clamp01(nearest / lookAhead);
        }

        // Role: 메뉴 등 정지 사유가 생기면 선원의 물리 속도도 즉시 멈춘다.
        private void StopWhenPaused()
        {
            if (!session.IsPaused) return;
            foreach (CrewView view in views.Values) view.ApplyVelocity(Vector2.zero);
        }

        // Role: 세션 정지 구독을 정리한다.
        private void OnDestroy()
        {
            if (session != null) session.Pauses.Changed -= StopWhenPaused;
        }
        // Role: 현재 시설이 행동 자격과 기능 조건을 계속 만족하는지 검사한다.
        private bool TargetUsable(CrewState member)
        {
            FacilityState item = ship.GetFacility(member.targetId);
            if (item == null || item.durability <= 0) return false;
            FacilityDefinition kind = ship.GetFacilityDefinition(item.definitionId);
            bool bed = member.activity == CrewActivity.GoingToBed || member.activity == CrewActivity.Sleeping;
            return bed ? kind.usableAsBed : member.specialty == CrewSpecialty.Maintenance && kind.repairable && item.durability < kind.maxDurability;
        }

        // Role: 유효한 작업 대상을 자신이 계속 선점하고 있는지 확인한다.
        private bool OwnsUsableTarget(CrewState member) => !string.IsNullOrEmpty(member.targetId) && claims.TryGetValue(member.targetId, out string owner) && owner == member.id && TargetUsable(member);

        // Role: 행동 종료·욕구 전환·작업 중단 시 대상과 남은 경로를 함께 해제한다.
        private void Cancel(CrewState member)
        {
            if (!string.IsNullOrEmpty(member.targetId) && claims.TryGetValue(member.targetId, out string owner) && owner == member.id) claims.Remove(member.targetId);
            member.targetId = null;
            member.path.Clear();
            member.activity = CrewActivity.Idle;
            member.remainingSeconds = 0;
            member.destination = null;
            member.nextWaypoint = null;
            member.resumeWaypoint = false;
            member.hasWorkPosition = false;
            member.velocity = Vector2.zero;
            if (views.TryGetValue(member.id, out CrewView view)) view.ApplyVelocity(Vector2.zero);
        }

        // Role: UI에서 지속할 주 업무를 변경한다. 자격 없는 특별 업무는 거부한다.
        public void SetDuty(string id, CrewDuty duty)
        {
            if (!IsReady || session.IsPaused || !Enum.IsDefined(typeof(CrewDuty), duty)) return;
            CrewState member = state.members.Find(candidate => candidate.id == id);
            if (member == null || member.availability != CrewAvailability.Ready || (duty == CrewDuty.Maintenance && member.specialty != CrewSpecialty.Maintenance)) return;
            member.primaryDuty = duty;
            if (member.activity == CrewActivity.GoingToRepair || member.activity == CrewActivity.Repairing) Cancel(member);
        }

        // Role: 사망·수감·전투 등 외부 시스템의 업무 중단을 즉시 반영한다.
        public void SetAvailability(string id, CrewAvailability availability)
        {
            if (!IsReady || !Enum.IsDefined(typeof(CrewAvailability), availability)) return;
            CrewState member = state.members.Find(candidate => candidate.id == id);
            if (member == null || (member.health == 0 && availability != CrewAvailability.Dead)) return;
            Cancel(member);
            member.availability = availability;
            if (availability == CrewAvailability.Dead) member.health = 0;
        }

        // Role: 사용자가 개발 재준비 버튼을 누른 경우에만 초기 선원·시설을 복원한다.
        public CrewRosterState RestorePrototype()
        {
            foreach (CrewView view in views.Values) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
            views.Clear();
            claims.Clear();
            navigationSearches = 0;
            session.Pauses.Changed -= StopWhenPaused;
            ship.RestoreInitialFacilities();
            return Initialize(null);
        }

        // Role: 표시만 현재 층에 제한하고 시뮬레이션은 모든 층에서 유지한다.
        public bool IsVisible(CrewState member) => IsReady && member.availability != CrewAvailability.Dead && ship.IsFloorVisible(member.floorId);

        // Role: 자원·행동·시설 선점 상태를 개발 HUD로 표시한다.
        private void LateUpdate()
        {
            if (!IsReady) return;
            foreach (CrewState member in state.members) views[member.id].SynchronizeVisibility(IsVisible(member));
            StopWhenPaused();
            if (statusLabel == null || !IsReady) return;
            var text = new StringBuilder($"선원 {state.members.Count}명 · 식량 {state.food} · 수리도구 {state.repairTools} · Nav {navigationSearches}회\n");
            foreach (CrewState member in state.members)
            {
                string activity = IsWorking(member) && !AtWorkPosition(member) ? "작업점 복귀" : ActivityName(member.activity);
                text.AppendLine($"{member.displayName} {member.floorId} · {activity} · 주업무 {member.primaryDuty}\n  기력 {member.energy:0} / 허기 {member.hunger:0} / 체력 {member.health:0} / 행복 {member.happiness:0}");
            }
            foreach (FacilityState item in ship.Facilities)
                text.AppendLine($"{item.id} 내구도 {item.durability:0} / 선점 {(claims.TryGetValue(item.id, out string owner) ? owner : "없음")}");
            statusLabel.text = text.ToString();
        }

        // Role: 내부 행동 값을 사용자에게 읽을 수 있는 이름으로 표시한다.
        private static string ActivityName(CrewActivity activity) => activity switch
        {
            CrewActivity.GoingToRepair => "수리 이동", CrewActivity.Repairing => "수리", CrewActivity.GoingToBed => "취침 이동",
            CrewActivity.Sleeping => "취침", CrewActivity.Eating => "식사", CrewActivity.Wandering => "배회", _ => "휴식"
        };
        // Role: 전체 경로를 런타임 캐시로 사용하는 이동 행동을 구분한다.
        private static bool IsNavigating(CrewState member) => member.activity == CrewActivity.GoingToRepair || member.activity == CrewActivity.GoingToBed || member.activity == CrewActivity.Wandering;
        // Role: 밀림 저항과 작업 시작점 복귀가 필요한 행동을 구분한다.
        private static bool IsWorking(CrewState member) => member.activity == CrewActivity.Repairing || member.activity == CrewActivity.Sleeping || member.activity == CrewActivity.Eating;
        // Role: 작업 복귀 경로가 필요한 밀림 상태를 구분한다.
        private bool IsReturning(CrewState member) => IsWorking(member) && member.hasWorkPosition && !AtWorkPosition(member);
        // Role: 시설 선점이 필요한 행동인지 구분한다.
        private static bool HasTarget(CrewState member) => member.activity == CrewActivity.GoingToRepair || member.activity == CrewActivity.Repairing || member.activity == CrewActivity.GoingToBed || member.activity == CrewActivity.Sleeping;
        // Role: 0~100 상태 수치인지 검사한다.
        private static bool Percent(float value) => Finite(value) && value >= 0 && value <= 100;
        // Role: 수치가 유한한지 검사한다.
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
