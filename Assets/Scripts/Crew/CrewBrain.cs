using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class CrewBrain : IDisposable
    {
        private readonly CrewSettings settings;
        private readonly GameRuntime session;
        private readonly ShipModel ship;
        private readonly NavigationRules navigation;
        private readonly InventoryState inventory;
        public CrewRosterState State => state;

        public CrewBrain(CrewSettings settings, GameRuntime session, ShipModel ship, NavigationRules navigation, InventoryState inventory)
        {
            this.settings = settings;
            this.session = session;
            this.ship = ship;
            this.navigation = navigation;
            this.inventory = inventory;
        }

        public void BindBody(string id, ICrewBody body)
        {
            if (views.TryGetValue(id, out var previous))
                previous.SynchronizeVisibility(false);
            views[id] = body;
        }

        private const string HelmTarget = "navigation-helm";
        private CrewRosterState state;
        private readonly ReservationService claims = new ReservationService();
        private readonly Dictionary<string, ICrewBody> views = new Dictionary<string, ICrewBody>();
        private Vector2[] directions;
        private float[] scores;
        private int navigationSearches;
        public bool IsReady => state != null && session.IsReady;
        public float CellSize => ship.CellSize;
        public float BodyRadius => settings.bodyRadius;
        public bool HelmsmanReady => state != null && state.members.Exists(member => member.activity == CrewActivity.Helming && member.availability == CrewAvailability.Ready && AtWorkPosition(member) && TargetUsable(member));

        // Role: 플레이어가 사용할 침대의 선점 가능 여부를 확인한다.
        public bool PlayerBedAvailable(string id) => !claims.TryGetValue(id, out string owner) || owner == "player";
        // Role: 침대를 플레이어에게 예약해 선원과의 중복 사용을 방지한다.
        public bool ReservePlayerBed(string id)
        {
            if (!PlayerBedAvailable(id))
                return false;
            claims[id] = "player";
            return true;
        }

        // Role: 취침 종료 시 플레이어의 침대 예약만 해제한다.
        public void ReleasePlayerBed(string id)
        {
            if (!string.IsNullOrEmpty(id) && claims.TryGetValue(id, out string owner) && owner == "player")
                claims.Remove(id);
        }

        // Role: 안정 ID로 현재 승선 선원을 조회한다.
        public CrewState FindMember(string id) => state.members.Find(member => member.id == id);
        // Role: 후보가 현재 함선에서 복원 가능한 인물인지 검사한다.
        public void CheckCandidate(CrewState candidate)
        {
            candidate = CopyCandidate(candidate);
            ValidateMember(candidate);
            if (candidate.availability != CrewAvailability.Ready || candidate.activity != CrewActivity.Idle)
                throw new InvalidDataException("고용 후보의 상태가 유효하지 않습니다.");
        }

        // Role: 계약 확정된 인물을 복사하고 물리 표현을 생성한다.
        public void AddMember(CrewState candidate)
        {
            if (FindMember(candidate.id) != null || state.members.Count >= 256)
                throw new InvalidOperationException("선원이 이미 승선했거나 인원 한도를 초과했습니다.");
            CrewState member = CopyCandidate(candidate);
            member.velocity = Vector2.zero;
            CheckCandidate(member);
            ICrewBody view = new LogicalCrewBody();
            state.members.Add(member);
            state.members.Sort((left, right) => string.Compare(left.id, right.id, StringComparison.Ordinal));
            views.Add(member.id, view);
        }

        // Role: Unity가 빈 객체로 직렬화한 선택적 경로를 고용 복사본에서 정규화한다.
        private static CrewState CopyCandidate(CrewState candidate)
        {
            if (candidate == null)
                throw new InvalidDataException("고용 후보가 없습니다.");
            CrewState copy = JsonUtility.FromJson<CrewState>(JsonUtility.ToJson(candidate));
            copy.path = new List<CrewWaypoint>();
            if (copy.destination != null && string.IsNullOrEmpty(copy.destination.floorId))
                copy.destination = null;
            if (copy.nextWaypoint != null && string.IsNullOrEmpty(copy.nextWaypoint.floorId))
                copy.nextWaypoint = null;
            return copy;
        }

        // Role: 만료 선원의 업무 선점과 물리 표현을 해제하고 재계약용 상태를 반환한다.
        public CrewState RemoveMember(string id)
        {
            CrewState member = FindMember(id);
            if (member == null)
                return null;
            Cancel(member);
            member.velocity = Vector2.zero;
            if (views.TryGetValue(id, out ICrewBody view))
            {
                view.SynchronizeVisibility(false);
                views.Remove(id);
            }

            state.members.Remove(member);
            return member;
        }

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

            state = saved ?? new CrewRosterState
            {
                settingsId = settings.id,
                food = settings.initialFood,
                repairTools = settings.initialRepairTools
            };
            if (saved == null)
                foreach (CrewState member in settings.initialCrew)
                    state.members.Add(JsonUtility.FromJson<CrewState>(JsonUtility.ToJson(member)));
            if (state.settingsId != settings.id || inventory.Food < 0 || inventory.RepairTools < 0 || state.members == null || state.members.Count > 256 || double.IsNaN(state.remainder) || double.IsInfinity(state.remainder) || state.remainder < 0 || state.remainder >= settings.stepSeconds)
                throw new InvalidDataException("선원 목록 또는 자원·시간 상태가 유효하지 않습니다.");
            state.members.Sort((left, right) => string.Compare(left?.id, right?.id, StringComparison.Ordinal));
            var ids = new HashSet<string>();
            foreach (CrewState member in state.members)
            {
                if (member != null)
                {
                    member.path = new List<CrewWaypoint>();
                    if (member.nextWaypoint != null && !string.IsNullOrEmpty(member.nextWaypoint.floorId))
                        member.path.Add(member.nextWaypoint);
                    else
                        member.nextWaypoint = null;
                    if (member.destination != null && string.IsNullOrEmpty(member.destination.floorId))
                        member.destination = null;
                    member.resumeWaypoint = member.nextWaypoint != null;
                    if (IsNavigating(member) || IsReturning(member))
                        member.repathRemaining = 0;
                }

                ValidateMember(member);
                member.repathRemaining = Mathf.Min(member.repathRemaining, settings.repathInterval);
                if (!ids.Add(member.id))
                    throw new InvalidDataException("선원 ID가 중복됩니다.");
                if (HasTarget(member))
                {
                    if (!TargetUsable(member) || member.availability != CrewAvailability.Ready || claims.ContainsKey(member.targetId) || member.path.Exists(point => !ship.IsWalkablePosition(point.floorId, point.position)))
                        Cancel(member);
                    else
                        claims.Add(member.targetId, member.id);
                }

                if (IsNavigating(member) && member.destination == null)
                    Cancel(member);
                ICrewBody view = new LogicalCrewBody();
                views.Add(member.id, view);
            }

            session.Pauses.Changed += StopWhenPaused;
            return state;
        }

        // Role: 영·음수 간격이나 비정상 생활 수치로 진행이 멈추지 않도록 검사한다.
        private void ValidateSettings()
        {
            if (settings == null || session == null || ship == null || settings.initialCrew == null || string.IsNullOrEmpty(settings.id))
                throw new InvalidDataException("선원 설정 또는 필수 참조가 누락됐습니다.");
            foreach (float value in new[]
            {
                settings.stepSeconds,
                settings.moveSpeed,
                settings.hungerRate,
                settings.mealThreshold,
                settings.mealSeconds,
                settings.mealRecovery,
                settings.energyDrain,
                settings.sleepThreshold,
                settings.wakeThreshold,
                settings.sleepRecovery,
                settings.repairSeconds,
                settings.repairAmount,
                settings.starvationRate,
                settings.repathInterval,
                settings.lookAhead,
                settings.avoidanceWeight,
                settings.acceleration,
                settings.bodyRadius,
                settings.clearance,
                settings.waypointRadius,
                settings.arrivalRadius,
                settings.predictionSeconds,
                settings.normalMass,
                settings.workingMass,
                settings.workReturnSpeed,
                settings.workReturnAcceleration,
                settings.workRadius,
                settings.wanderDistance
            }

            )
                if (!Finite(value) || value <= 0)
                    throw new InvalidDataException("선원 조정값은 양의 유한 수치여야 합니다.");
            if (settings.stepSeconds < .05f || settings.stepSeconds > 1 || settings.repairSeconds < settings.stepSeconds || settings.mealSeconds < settings.stepSeconds || settings.mealThreshold > 100 || settings.mealRecovery > 100 || settings.sleepThreshold >= settings.wakeThreshold || settings.wakeThreshold > 100 || settings.wakeHour < 0 || settings.sleepHour > 23 || settings.sleepHour <= settings.wakeHour || settings.initialFood < 0 || settings.initialRepairTools < 0 || settings.steeringDirections < 8 || settings.steeringDirections > 32 || settings.repathInterval < settings.stepSeconds || !Finite(settings.momentumWeight) || settings.momentumWeight < 0 || settings.momentumWeight > 1 || settings.bodyRadius + settings.clearance >= .5f || settings.arrivalRadius >= settings.waypointRadius || settings.waypointRadius >= .5f)
                throw new InvalidDataException("선원 생활·작업 설정 범위가 유효하지 않습니다.");
            if (settings.workingMass <= settings.normalMass || settings.workRadius <= settings.arrivalRadius)
                throw new InvalidDataException("작업 중 질량 또는 복귀 범위가 유효하지 않습니다.");
        }

        // Role: 저장된 위치·욕구·행동·경로의 기본 무결성을 검사한다.
        private void ValidateMember(CrewState member)
        {
            if (member == null || string.IsNullOrEmpty(member.id) || !ship.IsWalkablePosition(member.floorId, member.position) || !Percent(member.health) || !Percent(member.happiness) || !Percent(member.hunger) || !Percent(member.energy) || !Finite(member.remainingSeconds) || member.remainingSeconds < 0 || member.path == null || member.path.Count > 16384 || !Finite(member.velocity.x) || !Finite(member.velocity.y) || !Finite(member.repathRemaining) || member.repathRemaining < 0 || member.wanderSequence < 0 || (member.hasWorkPosition && !ship.ValidPosition(member.floorId, member.workPosition * CellSize)) || !Enum.IsDefined(typeof(CrewActivity), member.activity) || !Enum.IsDefined(typeof(CrewAvailability), member.availability) || !Enum.IsDefined(typeof(CrewSpecialty), member.specialty) || !Enum.IsDefined(typeof(CrewDuty), member.primaryDuty) || (member.primaryDuty == CrewDuty.Maintenance && member.specialty != CrewSpecialty.Maintenance) || member.navigationExperience < 0 || (member.primaryDuty == CrewDuty.Navigation && !QualifiedNavigator(member)) || (member.health == 0 && member.availability != CrewAvailability.Dead))
                throw new InvalidDataException("저장된 선원 상태가 유효하지 않습니다.");
            foreach (CrewWaypoint point in member.path)
                if (point == null || !ship.ValidPosition(point.floorId, point.position * ship.CellSize))
                    throw new InvalidDataException("저장된 선원 경로가 유효하지 않습니다.");
            if (HasTarget(member) && string.IsNullOrEmpty(member.targetId))
                throw new InvalidDataException("선원의 행동 대상이 누락됐습니다.");
            foreach (CrewWaypoint point in new[]
            {
                member.destination,
                member.nextWaypoint
            }

            )
                if (point != null && !ship.ValidPosition(point.floorId, point.position * CellSize))
                    throw new InvalidDataException("선원의 이동 목적지가 유효하지 않습니다.");
            if (!HasTarget(member) && !string.IsNullOrEmpty(member.targetId))
                throw new InvalidDataException("선원 행동과 대상이 일치하지 않습니다.");
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
            if (!IsReady || session.IsPaused)
                return;
            state.remainder += seconds;
            while (state.remainder >= settings.stepSeconds)
            {
                state.remainder -= settings.stepSeconds;
                double simulatedTime = now - state.remainder;
                float hour = (float)(simulatedTime % GameRuntime.SecondsPerDay / GameRuntime.SecondsPerDay * 24);
                bool night = hour >= settings.sleepHour || hour < settings.wakeHour;
                foreach (CrewState member in state.members)
                    Step(member, settings.stepSeconds, night);
            }
        }

        // Role: 식사·취침·일반 업무 우선순위에 따라 한 선원의 상태를 진행한다.
        private void Step(CrewState member, float seconds, bool night)
        {
            if (member.availability == CrewAvailability.Combat)
                return;
            if (member.availability != CrewAvailability.Ready)
            {
                Cancel(member);
                return;
            }

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
                if (!AtWorkPosition(member))
                    return;
                member.remainingSeconds = Mathf.Max(0, member.remainingSeconds - seconds);
                if (member.remainingSeconds == 0)
                {
                    member.hunger = Mathf.Max(0, member.hunger - settings.mealRecovery);
                    Cancel(member);
                }

                return;
            }

            if (member.hunger >= settings.mealThreshold && inventory.HasMeal)
            {
                Cancel(member);
                inventory.ConsumeMeal();
                member.activity = CrewActivity.Eating;
                member.remainingSeconds = settings.mealSeconds;
                BeginWorkPosition(member);
                return;
            }

            bool sleeping = member.activity == CrewActivity.GoingToBed || member.activity == CrewActivity.Sleeping;
            bool navigationDuty = navigation != null && navigation.NeedsNavigator && member.primaryDuty == CrewDuty.Navigation && QualifiedNavigator(member);
            bool needsSleep = !navigationDuty && (night || session.IsSleeping || member.energy <= settings.sleepThreshold || (sleeping && member.energy < settings.wakeThreshold));
            if (needsSleep)
            {
                if (!sleeping || !OwnsUsableTarget(member))
                {
                    Cancel(member);
                    TryStartTask(member, true);
                    return;
                }

                if (member.activity == CrewActivity.Sleeping && AtWorkPosition(member))
                    member.energy = Mathf.Min(100, member.energy + settings.sleepRecovery * seconds);
                return;
            }

            if (sleeping)
            {
                Cancel(member);
                return;
            }

            if (member.activity == CrewActivity.GoingToHelm || member.activity == CrewActivity.Helming)
            {
                if (!navigationDuty || !OwnsUsableTarget(member))
                    Cancel(member);
                else
                    return;
            }

            if (navigationDuty)
            {
                if (member.activity != CrewActivity.Idle)
                    Cancel(member);
                TryStartHelm(member);
                return;
            }

            if (member.activity == CrewActivity.GoingToPest || member.activity == CrewActivity.FightingPest)
            {
                var pest = session.Pests.Find(member.targetId);
                if (pest == null || !OwnsUsableTarget(member)) { Cancel(member); return; }
                if (member.activity == CrewActivity.FightingPest)
                {
                    if (pest.floorId != member.floorId || Vector2.Distance(pest.position, member.position) > 1.1f * CellSize)
                    {
                        member.activity = CrewActivity.GoingToPest; member.hasWorkPosition = false; member.repathRemaining = 0;
                    }
                    else if (AtWorkPosition(member)) session.Pests.Strike(pest.id, seconds * 6 * session.Life.Performance(member, CrewDuty.Guard) * (member.specialty == CrewSpecialty.Guard ? 1.5f : 1));
                }
                return;
            }
            if (member.activity == CrewActivity.GoingToLookout || member.activity == CrewActivity.Lookingout)
            {
                if (!OwnsUsableTarget(member)) Cancel(member);
                else return;
            }

            if (member.activity == CrewActivity.GoingToRepair || member.activity == CrewActivity.Repairing)
            {
                if (!OwnsUsableTarget(member) || inventory.RepairTools == 0)
                {
                    Cancel(member);
                    return;
                }

                if (member.activity == CrewActivity.GoingToRepair)
                {
                    return;
                }

                if (!AtWorkPosition(member))
                    return;
                member.remainingSeconds = Mathf.Max(0, member.remainingSeconds - seconds);
                if (member.remainingSeconds == 0)
                {
                    if (ship.RepairFacility(member.targetId, settings.repairAmount * session.Life.Performance(member, CrewDuty.Maintenance)))
                        inventory.RepairTools--;
                    member.remainingSeconds = settings.repairSeconds;
                    if (!TargetUsable(member) || inventory.RepairTools == 0)
                        Cancel(member);
                }

                return;
            }

            if (member.activity == CrewActivity.Wandering)
            {
                bool workExists = member.specialty == CrewSpecialty.Maintenance && inventory.RepairTools > 0;
                if (workExists)
                {
                    workExists = false;
                    foreach (FacilityState item in ship.Facilities)
                    {
                        FacilityDefinition kind = ship.GetFacilityDefinition(item.definitionId);
                        if (!claims.ContainsKey(item.id) && kind.repairable && item.durability > 0 && item.durability < ship.MaximumDurability(item))
                        {
                            workExists = true;
                            break;
                        }
                    }
                }
                workExists |= session.Pests.Active.Length > 0 || (!navigation.IsDocked && ship.Facilities.Any(item => item.durability > 0 && ship.GetFacilityDefinition(item.definitionId).role == FacilityRole.Lookout && !claims.ContainsKey(item.id)));

                if (!workExists)
                    return;
                Cancel(member);
            }

            if (member.repathRemaining == 0)
            {
                bool generalFirst = member.primaryDuty == CrewDuty.Guard || member.primaryDuty == CrewDuty.Lookout;
                bool started = generalFirst ? TryStartGeneral(member) || TryStartTask(member, false) : TryStartTask(member, false) || TryStartGeneral(member);
                if (!started) TryStartWander(member);
            }
        }

        private bool TryStartGeneral(CrewState member)
        {
            if (member.primaryDuty != CrewDuty.Lookout)
                foreach (var pest in session.Pests.Active)
                    if (!claims.ContainsKey(pest.id) && BuildPath(member, pest.floorId, pest.position, out var route))
                    {
                        claims.Add(pest.id, member.id); member.targetId = pest.id; member.path = route;
                        member.destination = new CrewWaypoint { floorId = pest.floorId, position = pest.position };
                        member.activity = CrewActivity.GoingToPest; member.repathRemaining = settings.repathInterval; return true;
                    }
            if (!navigation.IsDocked)
                foreach (var facility in ship.Facilities.Where(item => item.durability > 0 && ship.GetFacilityDefinition(item.definitionId).role == FacilityRole.Lookout))
                    if (!claims.ContainsKey(facility.id) && ship.TryGetAccessCell(facility.id, out var access) && BuildPath(member, facility.floorId, access, out var route))
                    {
                        claims.Add(facility.id, member.id); member.targetId = facility.id; member.path = route;
                        member.destination = new CrewWaypoint { floorId = facility.floorId, position = access };
                        member.activity = CrewActivity.GoingToLookout; member.repathRemaining = settings.repathInterval; return true;
                    }
            if (member.primaryDuty == CrewDuty.Lookout)
                foreach (var pest in session.Pests.Active)
                    if (!claims.ContainsKey(pest.id) && BuildPath(member, pest.floorId, pest.position, out var route))
                    {
                        claims.Add(pest.id, member.id); member.targetId = pest.id; member.path = route;
                        member.destination = new CrewWaypoint { floorId = pest.floorId, position = pest.position };
                        member.activity = CrewActivity.GoingToPest; member.repathRemaining = settings.repathInterval; return true;
                    }
            return false;
        }

        // Role: 안정 시설 순서로 자격·자재·접근·선점을 확인하고 대표 업무를 선택한다.
        private bool TryStartTask(CrewState member, bool bed)
        {
            if (member.repathRemaining > 0)
                return false;
            member.repathRemaining = settings.repathInterval;
            if (!bed && (member.specialty != CrewSpecialty.Maintenance || inventory.RepairTools == 0))
                return false;
            var facilities = new List<FacilityState>(ship.Facilities);
            facilities.Sort((left, right) => string.Compare(left.id, right.id, StringComparison.Ordinal));
            foreach (FacilityState item in facilities)
            {
                FacilityDefinition kind = ship.GetFacilityDefinition(item.definitionId);
                if (claims.ContainsKey(item.id) || item.durability <= 0 || (bed ? !kind.usableAsBed : !kind.repairable || item.durability >= ship.MaximumDurability(item)))
                    continue;
                if (!ship.TryGetAccessCell(item.id, out Vector2 access) || !BuildPath(member, item.floorId, access, out List<CrewWaypoint> path))
                    continue;
                claims.Add(item.id, member.id);
                member.targetId = item.id;
                member.destination = new CrewWaypoint
                {
                    floorId = item.floorId,
                    position = access
                };
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
            if (!ship.TryGetFloorRoute(member.floorId, targetFloor, out List<string> floors))
                return false;
            Vector2 from = member.position;
            for (int i = 0; i < floors.Count; i++)
            {
                Vector2 to = target, arrival = default;
                if (i < floors.Count - 1 && !ship.TryConnection(floors[i], floors[i + 1], from, out to, out arrival))
                    return false;
                if (!ship.TryFindPath(floors[i], from, to, out List<Vector2> segment))
                    return false;
                if (segment.Count > 1 && ship.IsCrewSegmentClear(floors[i], from, segment[1], settings.bodyRadius + settings.clearance))
                    segment.RemoveAt(0);
                foreach (Vector2 position in segment)
                    result.Add(new CrewWaypoint { floorId = floors[i], position = position });
                if (i < floors.Count - 1)
                {
                    from = arrival;
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
            foreach (char character in member.id)
                seed = (seed * 31 + character) & 0x7fffffff;
            int sequence = member.wanderSequence;
            member.wanderSequence = sequence == int.MaxValue ? 0 : sequence + 1;
            for (int i = 0; i < directions.Length; i++)
            {
                int index = (int)(((long)seed + (long)sequence * 7 + i * 5) % directions.Length);
                float distance = settings.wanderDistance * (.5f + .25f * (sequence % 3));
                Vector2 target = ship.CellCenter(member.floorId, member.position + directions[index] * distance);
                if (Vector2.Distance(target, member.position) < 1 || !ship.IsCrewSegmentClear(member.floorId, member.position, target, settings.bodyRadius + settings.clearance))
                    continue;
                if (!BuildPath(member, member.floorId, target, out List<CrewWaypoint> path))
                    return;
                member.activity = CrewActivity.Wandering;
                member.destination = new CrewWaypoint
                {
                    floorId = member.floorId,
                    position = target
                };
                member.path = path;
                member.resumeWaypoint = false;
                return;
            }
        }

        // Role: 생활 판단과 독립적으로 물리 간격에서 연속 이동을 갱신한다.
        public void FixedStep(float seconds)
        {
            if (!IsReady || session.IsSleeping)
                return;
            AdvanceMovement(seconds);
        }

        // Role: 취침 가속 중 물리를 비활성화하고 같은 논리 steering을 작은 간격으로 진행한다.
        public void AdvanceSleepMovement(float seconds)
        {
            HideForSleep();
            AdvanceMovement(seconds);
        }

        // Role: 화면 밖 이동으로 전환하기 전에 실제 물리 위치를 수집한다.
        public void HideForSleep()
        {
            foreach (ICrewBody view in views.Values)
                view.SynchronizeVisibility(false);
        }

        // Role: 실제 또는 취침 시뮬레이션 간격으로 선원의 이동을 처리한다.
        private void AdvanceMovement(float seconds)
        {
            if (!IsReady)
                return;
            foreach (CrewState member in state.members)
                views[member.id].SynchronizeVisibility(IsVisible(member));
            CapturePositions();
            if (session.IsPaused)
            {
                StopWhenPaused();
                return;
            }

            foreach (CrewState member in state.members)
            {
                member.repathRemaining = Mathf.Max(0, member.repathRemaining - seconds);
                if ((member.availability == CrewAvailability.Ready || member.availability == CrewAvailability.Combat) && IsNavigating(member))
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
            if (state == null)
                return;
            foreach (CrewState member in state.members)
            {
                views[member.id].CaptureState();
                if (IsWorking(member) && member.hasWorkPosition)
                    member.destination = new CrewWaypoint
                    {
                        floorId = member.floorId,
                        position = member.workPosition
                    };
                member.nextWaypoint = (IsNavigating(member) || IsReturning(member)) && member.path.Count > 0 ? new CrewWaypoint
                {
                    floorId = member.path[0].floorId,
                    position = member.path[0].position
                }

                : null;
            }
        }

        public void ResetPositionsForShip()
        {
            var floor = ship.GetFloorDefinition(ship.Definition.startingFloor);
            var positions = Enumerable.Range(0, floor.size.x * floor.size.y).Select(index => floor.origin + new Vector2(index % floor.size.x + .5f, index / floor.size.x + .5f)).Where(position => ship.HasCrewClearance(floor.id, position, settings.bodyRadius + settings.clearance)).OrderBy(position => Vector2.SqrMagnitude(position - floor.entry)).ToArray();
            int index = 0;
            foreach (var member in state.members)
            {
                Cancel(member);
                member.floorId = ship.Definition.startingFloor;
                member.position = positions[index++ % positions.Length];
                member.velocity = Vector2.zero;
                views[member.id].TeleportToState();
            }
        }

        // Role: 긴 주기 Nav 경로와 짧은 주기 Context steering을 결합한다.
        private void Navigate(CrewState member, ICrewBody view, float seconds)
        {
            if (member.activity != CrewActivity.Wandering && member.activity != CrewActivity.EncounterMoving && !OwnsUsableTarget(member))
            {
                Cancel(member);
                return;
            }

            if (member.repathRemaining == 0 && !RefreshRoute(member))
                return;
            while (member.path.Count > 0)
            {
                CrewWaypoint point = member.path[0];
                if (point.floorId != member.floorId)
                {
                    if (!ship.IsStairTransition(member.floorId, point.floorId, member.position, point.position, settings.waypointRadius))
                    {
                        Cancel(member);
                        return;
                    }

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
                    passed = next.floorId == member.floorId && Vector2.Dot(member.position - point.position, next.position - point.position) > 0 && ship.IsCrewSegmentClear(member.floorId, member.position, next.position, settings.bodyRadius + settings.clearance);
                }

                if (!passed)
                    break;
                member.path.RemoveAt(0);
            }

            if (member.path.Count == 0)
            {
                view.DriveVelocity(Vector2.zero, settings.acceleration, settings.normalMass);
                if (member.activity == CrewActivity.EncounterMoving)
                {
                    member.activity = CrewActivity.EncounterReady;
                    member.velocity = Vector2.zero;
                    return;
                }
                if (member.activity == CrewActivity.Wandering)
                {
                    Cancel(member);
                    return;
                }

                if (member.activity == CrewActivity.GoingToHelm)
                {
                    if (member.floorId == settings.helmFloor && Vector2.Distance(member.position, settings.helmPosition) <= settings.arrivalRadius)
                    {
                        member.activity = CrewActivity.Helming;
                        member.velocity = Vector2.zero;
                        view.ApplyVelocity(Vector2.zero);
                        BeginWorkPosition(member);
                    }

                    return;
                }
                if (member.activity == CrewActivity.GoingToPest)
                {
                    member.activity = CrewActivity.FightingPest; member.velocity = Vector2.zero; view.ApplyVelocity(Vector2.zero); BeginWorkPosition(member); return;
                }

                FacilityState target = ship.GetFacility(member.targetId);
                if (target != null && member.floorId == target.floorId && ship.IsAtFacilityAccess(target.id, member.position, settings.arrivalRadius))
                {
                    bool bed = member.activity == CrewActivity.GoingToBed;
                    member.activity = bed ? CrewActivity.Sleeping : member.activity == CrewActivity.GoingToLookout ? CrewActivity.Lookingout : CrewActivity.Repairing;
                    member.remainingSeconds = bed || member.activity == CrewActivity.Lookingout ? 0 : settings.repairSeconds;
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
            if (!ship.IsCrewSegmentClear(member.floorId, member.position, proposed, settings.bodyRadius + settings.clearance))
                velocity = Vector2.zero;
            member.velocity = velocity;
            if (view.IsSimulated)
                view.DriveVelocity(velocity, settings.acceleration, settings.normalMass);
            else
                member.position += velocity * seconds;
        }

        // Role: 쿨다운이 끝났을 때만 현재 대상까지의 Nav 경로를 갱신한다.
        private bool RefreshRoute(CrewState member, CrewWaypoint workDestination = null)
        {
            member.repathRemaining = settings.repathInterval;
            CrewWaypoint destination = workDestination ?? member.destination;
            if (workDestination == null && member.targetId == HelmTarget)
                destination = new CrewWaypoint
                {
                    floorId = settings.helmFloor,
                    position = settings.helmPosition
                };
            else if (workDestination == null && member.activity == CrewActivity.GoingToPest)
            {
                var pest = session.Pests?.Find(member.targetId);
                if (pest == null) { Cancel(member); return false; }
                destination = new CrewWaypoint { floorId = pest.floorId, position = pest.position };
            }
            else if (workDestination == null && member.activity != CrewActivity.Wandering && member.activity != CrewActivity.EncounterMoving)
            {
                FacilityState target = ship.GetFacility(member.targetId);
                if (target == null || !ship.TryGetAccessCell(target.id, out Vector2 access))
                {
                    Cancel(member);
                    return false;
                }

                destination = new CrewWaypoint
                {
                    floorId = target.floorId,
                    position = access
                };
            }

            if (destination == null)
            {
                Cancel(member);
                return false;
            }

            CrewState origin = member;
            CrewWaypoint via = member.resumeWaypoint && member.path.Count > 0 ? member.path[0] : null;
            if (via != null)
            {
                bool valid = via.floorId == member.floorId ? ship.IsCrewSegmentClear(member.floorId, member.position, via.position, settings.bodyRadius + settings.clearance) : ship.AreFloorsConnected(member.floorId, via.floorId) && Vector2.Distance(member.position, ship.EntryPosition(member.floorId)) <= settings.waypointRadius;
                if (valid)
                    origin = new CrewState
                    {
                        floorId = via.floorId,
                        position = via.position
                    };
                else
                    via = null;
            }

            if (!BuildPath(origin, destination.floorId, destination.position, out List<CrewWaypoint> path))
            {
                if (workDestination == null)
                    Cancel(member);
                else
                {
                    member.path.Clear();
                    member.resumeWaypoint = false;
                }

                return false;
            }

            if (via != null)
                path.Insert(0, via);
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
        private void HoldPosition(CrewState member, ICrewBody view, float seconds)
        {
            bool working = IsWorking(member) && member.availability == CrewAvailability.Ready;
            if (working && !member.hasWorkPosition)
                BeginWorkPosition(member);
            float acceleration = working ? settings.workReturnAcceleration : settings.acceleration;
            Vector2 desired = Vector2.zero;
            if (working && Vector2.Distance(member.position, member.workPosition) > .025f)
            {
                Vector2 goal = member.workPosition;
                bool clear = ship.IsCrewSegmentClear(member.floorId, member.position, goal, settings.bodyRadius + settings.clearance);
                if (!clear)
                {
                    if (member.repathRemaining == 0)
                        RefreshRoute(member, new CrewWaypoint { floorId = member.floorId, position = member.workPosition });
                    while (member.path.Count > 0 && Vector2.Distance(member.position, member.path[0].position) <= settings.waypointRadius)
                        member.path.RemoveAt(0);
                    if (member.path.Count > 0)
                        goal = member.path[0].position;
                }
                else
                {
                    member.path.Clear();
                    member.resumeWaypoint = false;
                }

                if (clear || member.path.Count > 0)
                    desired = SteeringDirection(member, view, goal) * Mathf.Min(settings.workReturnSpeed, Vector2.Distance(member.position, goal) / seconds);
            }
            else if (working)
                member.path.Clear();
            member.velocity = Vector2.MoveTowards(member.velocity, desired, acceleration * seconds);
            if (view.IsSimulated)
                view.DriveVelocity(desired, acceleration, working ? settings.workingMass : settings.normalMass);
            else
            {
                Vector2 next = member.position + member.velocity * seconds;
                if (ship.IsCrewSegmentClear(member.floorId, member.position, next, settings.bodyRadius + settings.clearance))
                    member.position = next;
                else
                    member.velocity = Vector2.zero;
            }
        }

        // Role: 중간 목적지 interest와 회피 danger를 방향별로 평가한다.
        private Vector2 SteeringDirection(CrewState member, ICrewBody view, Vector2 goal)
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
                if (scores[i] > bestScore + .00001f || (scores[i] > 0 && Mathf.Abs(scores[i] - bestScore) <= .00001f && (best < 0 || Vector2.Dot(candidate, right) > Vector2.Dot(directions[best], right))))
                {
                    best = i;
                    bestScore = scores[i];
                }
            }

            if (best < 0)
                return Vector2.zero;
            int previous = (best + directions.Length - 1) % directions.Length;
            int next = (best + 1) % directions.Length;
            return (directions[best] * scores[best] + directions[previous] * scores[previous] + directions[next] * scores[next]).normalized;
        }

        // Role: 논리 장애물·예측 선원 위치와 화면 내 물리 장애물의 위험을 합친다.
        private float DirectionDanger(CrewState member, ICrewBody view, Vector2 direction, float lookAhead)
        {
            if (lookAhead <= .0001f)
                return 0;
            float nearest = lookAhead;
            float radius = settings.bodyRadius + settings.clearance;
            int samples = Mathf.Max(1, Mathf.CeilToInt(lookAhead / Mathf.Max(.05f, radius * .5f)));
            for (int i = 1; i <= samples; i++)
            {
                float distance = lookAhead * i / samples;
                if (!ship.HasCrewClearance(member.floorId, member.position + direction * distance, radius))
                {
                    nearest = distance;
                    break;
                }
            }

            foreach (CrewState other in state.members)
            {
                if (other == member || other.availability == CrewAvailability.Dead || other.floorId != member.floorId)
                    continue;
                Vector2 relative = other.position + other.velocity * settings.predictionSeconds - member.position;
                float separation = settings.bodyRadius * 2 + settings.clearance;
                float along = Vector2.Dot(relative, direction);
                if (relative.sqrMagnitude < separation * separation)
                {
                    if (along > 0)
                        nearest = 0;
                    continue;
                }

                float perpendicularSquared = Mathf.Max(0, relative.sqrMagnitude - along * along);
                if (along > 0 && perpendicularSquared < separation * separation)
                    nearest = Mathf.Min(nearest, Mathf.Max(0, along - Mathf.Sqrt(separation * separation - perpendicularSquared)));
            }

            nearest = Mathf.Min(nearest, view.ObstacleDistance(member.position, direction, radius, lookAhead));
            return 1 - Mathf.Clamp01(nearest / lookAhead);
        }

        // Role: 메뉴 등 정지 사유가 생기면 선원의 물리 속도도 즉시 멈춘다.
        private void StopWhenPaused()
        {
            if (!session.IsPaused)
                return;
            foreach (ICrewBody view in views.Values)
                view.ApplyVelocity(Vector2.zero);
        }

        // Role: 현재 시설이 행동 자격과 기능 조건을 계속 만족하는지 검사한다.
        private bool TargetUsable(CrewState member)
        {
            if (member.activity == CrewActivity.GoingToPest || member.activity == CrewActivity.FightingPest) return session.Pests?.Find(member.targetId) != null;
            if (member.targetId == HelmTarget)
                return QualifiedNavigator(member) && member.primaryDuty == CrewDuty.Navigation && navigation != null && navigation.NeedsNavigator && ship.IsWalkablePosition(settings.helmFloor, settings.helmPosition);
            FacilityState item = ship.GetFacility(member.targetId);
            if (item == null || item.durability <= 0)
                return false;
            FacilityDefinition kind = ship.GetFacilityDefinition(item.definitionId);
            if (member.activity == CrewActivity.GoingToLookout || member.activity == CrewActivity.Lookingout) return kind.role == FacilityRole.Lookout && !navigation.IsDocked;
            bool bed = member.activity == CrewActivity.GoingToBed || member.activity == CrewActivity.Sleeping;
            return bed ? kind.usableAsBed : member.specialty == CrewSpecialty.Maintenance && kind.repairable && item.durability < ship.MaximumDurability(item);
        }

        // Role: 유효한 작업 대상을 자신이 계속 선점하고 있는지 확인한다.
        private bool OwnsUsableTarget(CrewState member) => !string.IsNullOrEmpty(member.targetId) && claims.TryGetValue(member.targetId, out string owner) && owner == member.id && TargetUsable(member);
        // Role: 행동 종료·욕구 전환·작업 중단 시 대상과 남은 경로를 함께 해제한다.
        private void Cancel(CrewState member)
        {
            if (!string.IsNullOrEmpty(member.targetId) && claims.TryGetValue(member.targetId, out string owner) && owner == member.id)
                claims.Remove(member.targetId);
            member.targetId = null;
            member.path.Clear();
            member.activity = CrewActivity.Idle;
            member.remainingSeconds = 0;
            member.destination = null;
            member.nextWaypoint = null;
            member.resumeWaypoint = false;
            member.hasWorkPosition = false;
            member.velocity = Vector2.zero;
            if (views.TryGetValue(member.id, out ICrewBody view))
                view.ApplyVelocity(Vector2.zero);
        }

        // Role: UI에서 지속할 주 업무를 변경한다. 자격 없는 특별 업무는 거부한다.
        public void SetDuty(string id, CrewDuty duty)
        {
            if (!IsReady || !Enum.IsDefined(typeof(CrewDuty), duty))
                return;
            CrewState member = state.members.Find(candidate => candidate.id == id);
            if (member == null || member.availability != CrewAvailability.Ready || (duty == CrewDuty.Maintenance && member.specialty != CrewSpecialty.Maintenance))
                return;
            if (duty == CrewDuty.Navigation && !QualifiedNavigator(member))
                return;
            member.primaryDuty = duty;
            if (duty == CrewDuty.Navigation && navigation.journal != null)
                navigation.journal.State.autopilot = true;
            if (member.activity == CrewActivity.GoingToRepair || member.activity == CrewActivity.Repairing || member.targetId == HelmTarget || member.activity == CrewActivity.GoingToLookout || member.activity == CrewActivity.Lookingout || member.activity == CrewActivity.GoingToPest || member.activity == CrewActivity.FightingPest)
                Cancel(member);
        }
        public void StopPestWork()
        {
            foreach (var member in state.members.Where(value => value.activity == CrewActivity.GoingToPest || value.activity == CrewActivity.FightingPest)) Cancel(member);
        }

        // Role: 사망·수감·전투 등 외부 시스템의 업무 중단을 즉시 반영한다.
        public void SetAvailability(string id, CrewAvailability availability)
        {
            if (!IsReady || !Enum.IsDefined(typeof(CrewAvailability), availability))
                return;
            CrewState member = state.members.Find(candidate => candidate.id == id);
            if (member == null || (member.health == 0 && availability != CrewAvailability.Dead))
                return;
            Cancel(member);
            member.availability = availability;
            if (availability == CrewAvailability.Dead)
                member.health = 0;
        }

        // Role: 표시만 현재 층에 제한하고 시뮬레이션은 모든 층에서 유지한다.
        public bool IsVisible(CrewState member) => IsReady && !session.IsSleeping && (member.availability == CrewAvailability.Ready || member.availability == CrewAvailability.Combat) && ship.IsFloorVisible(member.floorId);
        // Role: 내부 행동 값을 사용자에게 읽을 수 있는 이름으로 표시한다.
        private static string ActivityName(CrewActivity activity) => activity switch
        {
            CrewActivity.GoingToRepair => "수리 이동",
            CrewActivity.Repairing => "수리",
            CrewActivity.GoingToBed => "취침 이동",
            CrewActivity.Sleeping => "취침",
            CrewActivity.Eating => "식사",
            CrewActivity.Wandering => "배회",
            CrewActivity.GoingToHelm => "방향타 이동",
            CrewActivity.Helming => "조타",
            _ => "휴식"
        };
        // Role: 전체 경로를 런타임 캐시로 사용하는 이동 행동을 구분한다.
        private static bool IsNavigating(CrewState member) => member.activity == CrewActivity.GoingToRepair || member.activity == CrewActivity.GoingToBed || member.activity == CrewActivity.Wandering || member.activity == CrewActivity.GoingToHelm || member.activity == CrewActivity.EncounterMoving || member.activity == CrewActivity.GoingToPest || member.activity == CrewActivity.GoingToLookout;

        public bool MoveForEncounter(string id, string floorId, Vector2 position)
        {
            var member = FindMember(id);
            if (member == null || member.availability != CrewAvailability.Combat || !BuildPath(member, floorId, position, out List<CrewWaypoint> path))
                return false;
            member.destination = new CrewWaypoint { floorId = floorId, position = position };
            member.path = path;
            member.activity = CrewActivity.EncounterMoving;
            member.repathRemaining = 0;
            return true;
        }
        // Role: 밀림 저항과 작업 시작점 복귀가 필요한 행동을 구분한다.
        private static bool IsWorking(CrewState member) => member.activity == CrewActivity.Repairing || member.activity == CrewActivity.Sleeping || member.activity == CrewActivity.Eating || member.activity == CrewActivity.Helming || member.activity == CrewActivity.FightingPest || member.activity == CrewActivity.Lookingout;
        // Role: 작업 복귀 경로가 필요한 밀림 상태를 구분한다.
        private bool IsReturning(CrewState member) => IsWorking(member) && member.hasWorkPosition && !AtWorkPosition(member);
        // Role: 시설 선점이 필요한 행동인지 구분한다.
        private static bool HasTarget(CrewState member) => member.activity == CrewActivity.GoingToRepair || member.activity == CrewActivity.Repairing || member.activity == CrewActivity.GoingToBed || member.activity == CrewActivity.Sleeping || member.activity == CrewActivity.GoingToHelm || member.activity == CrewActivity.Helming || member.activity == CrewActivity.GoingToPest || member.activity == CrewActivity.FightingPest || member.activity == CrewActivity.GoingToLookout || member.activity == CrewActivity.Lookingout;
        // Role: 항해 특기와 최소 항해 경력을 함께 확인한다.
        public static bool QualifiedNavigator(CrewState member) => member.specialty == CrewSpecialty.Navigation && member.navigationExperience >= 1;
        // Role: 유효한 항해사가 방향타 접근 지점을 선점하고 이동을 시작한다.
        private void TryStartHelm(CrewState member)
        {
            if (member.repathRemaining > 0 || claims.ContainsKey(HelmTarget))
                return;
            member.repathRemaining = settings.repathInterval;
            if (!BuildPath(member, settings.helmFloor, settings.helmPosition, out List<CrewWaypoint> path))
                return;
            claims.Add(HelmTarget, member.id);
            member.targetId = HelmTarget;
            member.path = path;
            member.destination = new CrewWaypoint
            {
                floorId = settings.helmFloor,
                position = settings.helmPosition
            };
            member.activity = CrewActivity.GoingToHelm;
        }

        // Role: 0~100 상태 수치인지 검사한다.
        private static bool Percent(float value) => Finite(value) && value >= 0 && value <= 100;
        // Role: 수치가 유한한지 검사한다.
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public void Dispose()
        {
            session.Pauses.Changed -= StopWhenPaused;
        }
    }
}
