using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class NavigationRules
    {
        public void RotateByStorm(float degrees)
        {
            if (!IsDocked)
                state.heading = Mathf.Repeat(state.heading + degrees, 360);
        }
        private readonly NavigationDefinition definition;
        private readonly GameRuntime session;
        private readonly ShipModel ship;
        public JournalRules journal;
        public CrewBrain crew;
        public NavigationState State => state;

        public NavigationRules(NavigationDefinition definition, GameRuntime session, ShipModel ship)
        {
            this.definition = definition;
            this.session = session;
            this.ship = ship;
        }

        private bool arrivalPending;
        private NavigationState state;
        private string playthroughId;
        public bool IsReady => state != null && session.IsReady;
        public bool IsAboard => state == null || state.aboard;
        public bool IsDocked => state != null && !string.IsNullOrEmpty(state.dockedPort);
        public string DockedPort => state?.dockedPort;
        public int CompletedVoyages => state?.completedSequence ?? 0;
        public int PortCount => definition.ports.Length;
        public int VoyageSequence => state?.voyageSequence ?? 0;
        public string DeparturePort => state?.departurePort;
        public Vector2 WorldPosition => state?.position ?? Vector2.zero;
        public float Heading => state?.heading ?? 0;
        public PortDefinition[] Ports => definition.ports;
        public Rect WorldBounds => definition.bounds;
        public bool HelmOperational => ship.Facilities.Any(value => ship.GetFacilityDefinition(value.definitionId).role == FacilityRole.Helm && value.durability > 0);
        public bool NeedsNavigator => HelmOperational && journal != null && journal.AutopilotRequested && !IsDocked && !IsSteering;
        public bool CanRequestTow => IsReady && IsAboard && !IsDocked && !HelmOperational && !session.Battle.Running && !session.Events.Running && !session.Events.AwaitingChoice && !session.Economy.State.gameOver && session.ErrorMessage == null;
        public PortDefinition NearestTowPort => CanRequestTow ? definition.ports.OrderBy(port => (port.position - state.position).sqrMagnitude).First() : null;
        public int TowCost => (int)Math.Floor(session.State.inventory.Gold * (double)definition.towGoldFraction);

        // Role: 메뉴에서 확인한 비용과 목적지를 재확인하고 공통 입항·정산을 원자 저장한다.
        public bool RequestTow(string expectedPort, int expectedCost)
        {
            var destination = NearestTowPort;
            if (destination == null || destination.id != expectedPort || TowCost != expectedCost)
                return false;
            if (!session.FlushSave(true))
                return false;
            bool completeVoyage = destination.id != state.departurePort || state.departureCleared;
            session.Rest.Wake();
            session.State.inventory.Gold -= expectedCost;
            state.position = destination.position;
            state.speed = 0;
            state.dockedPort = destination.id;
            IsSteering = false;
            arrivalPending = completeVoyage;
            state.completedSequence = state.voyageSequence;
            session.Journal.Arrive(destination.id);
            NotifyArrival();
            session.RequestSave();
            bool saved = session.FlushSave(true);
            session.NotifyChanged(true);
            return saved;
        }

        // Role: 경제 정의의 항구 ID를 검증한다.
        public bool HasPort(string id) => !string.IsNullOrEmpty(id) && Port(id) != null;
        // Role: 정산 후 출항 요청을 기존 출항 검사로 처리한다.
        public void RequestDeparture()
        {
            if (IsReady && !session.IsPaused)
                Depart();
        }

        public bool IsSteering { get; private set; }

        public event Action<string, string> VoyageCompleted;
        // Role: 정의와 저장 상태를 검증하고 현재 공간의 표시를 복원한다.
        public NavigationState Initialize(NavigationState saved, string playId, string startingPort = null)
        {
            ValidateDefinition();
            playthroughId = playId;
            var port = Array.Find(definition.ports, value => value.id == startingPort) ?? definition.ports[0];
            state = saved ?? new NavigationState
            {
                definitionId = definition.id,
                position = port.position,
                dockedPort = port.id,
                aboard = true
            };
            ValidateState();
            IsSteering = false;
            return state;
        }

        // Role: 유한 수치·항구 ID·반경·해역 및 필수 참조를 검사한다.
        private void ValidateDefinition()
        {
            if (definition != null && (!Finite(definition.towGoldFraction) || definition.towGoldFraction < 0 || definition.towGoldFraction > 1))
                throw new InvalidDataException("견인 비용 비율이 유효하지 않습니다.");
            if (definition == null || definition.ports == null || definition.ports.Length < 2 || session == null || ship == null || !Finite(definition.maxSpeed) || definition.maxSpeed <= 0 || !Finite(definition.acceleration) || definition.acceleration <= 0 || !Finite(definition.turnSpeed) || definition.turnSpeed <= 0 || !ValidRect(definition.bounds))
                throw new InvalidDataException("항해 정의 또는 필수 참조가 유효하지 않습니다.");
            for (int i = 0; i < definition.ports.Length; i++)
            {
                PortDefinition port = definition.ports[i];
                if (port == null || string.IsNullOrEmpty(port.id) || !Finite(port.position) || !Inside(definition.bounds, port.position) || !Finite(port.arrivalRadius) || !Finite(port.departureRadius) || port.arrivalRadius <= 0 || port.departureRadius <= port.arrivalRadius)
                    throw new InvalidDataException("항구 정의가 유효하지 않습니다.");
                for (int j = 0; j < i; j++)
                    if (definition.ports[j].id == port.id || Vector2.Distance(definition.ports[j].position, port.position) <= definition.ports[j].departureRadius + port.departureRadius)
                        throw new InvalidDataException("항구 ID 또는 입출항 영역이 겹칩니다.");
            }
        }

        // Role: 저장된 항해 상태가 연결 가능한 항구·순번·좌표를 가졌는지 검사한다.
        private void ValidateState()
        {
            if (state.definitionId != definition.id || !Finite(state.position) || !Inside(definition.bounds, state.position) || !Finite(state.heading) || state.heading < 0 || state.heading >= 360 || !Finite(state.speed) || state.speed < 0 || state.speed > definition.maxSpeed || !Finite(state.boardingPosition) || state.completedSequence < 0 || state.voyageSequence < state.completedSequence || (long)state.voyageSequence > (long)state.completedSequence + 1)
                throw new InvalidDataException("저장된 항해 상태가 유효하지 않습니다.");
            if (IsDocked)
            {
                PortDefinition port = Port(state.dockedPort);
                if (port == null || state.speed != 0 || Vector2.Distance(port.position, state.position) > .001f || state.voyageSequence != state.completedSequence)
                    throw new InvalidDataException("저장된 입항 상태가 유효하지 않습니다.");
            }
            else if (!state.aboard || Port(state.departurePort) == null || state.voyageSequence != state.completedSequence + 1)
                throw new InvalidDataException("저장된 출항 상태가 유효하지 않습니다.");
            if (!state.aboard && !ship.ValidPosition(ship.ActiveFloor, state.boardingPosition))
                throw new InvalidDataException("저장된 승선 위치가 유효하지 않습니다.");
        }

        // Role: 출항지를 기록하고 고유 항해 순번으로 항해를 시작한다.
        public void Depart()
        {
            if (!IsDocked || !IsAboard || state.voyageSequence == int.MaxValue)
                return;
            if (!session.CanDepart())
                return;
            state.departurePort = state.dockedPort;
            state.dockedPort = null;
            state.departureCleared = false;
            state.voyageSequence++;
            state.voyageStartedAt = session.ElapsedSeconds;
            state.speed = definition.maxSpeed * ship.SailingFactor;
            journal?.BeginVoyage();
        }

        // Role: 공통 세션 시간으로 항해와 자동 조타를 진행하고 실제 소모 시간을 반환한다.
        public float AdvanceTime(float seconds)
        {
            if (!HelmOperational)
                IsSteering = false;
            if (IsDocked)
                return seconds;
            state.speed = Mathf.Min(state.speed, definition.maxSpeed * ship.SailingFactor);
            if (NeedsNavigator && crew != null && crew.HelmsmanReady)
            {
                if (journal.TryGetHeading(state.position, out float heading))
                {
                    float turn = Mathf.Abs(Mathf.DeltaAngle(state.heading, heading));
                    state.heading = Mathf.MoveTowardsAngle(state.heading, heading, definition.turnSpeed * seconds);
                    state.heading = Mathf.Repeat(state.heading, 360);
                    float speed = Mathf.Lerp(definition.maxSpeed, .5f, Mathf.InverseLerp(10, 80, turn));
                    state.speed = Mathf.MoveTowards(state.speed, speed, definition.acceleration * seconds);
                }
                else
                    state.speed = 0;
            }

            state.speed = Mathf.Min(state.speed, definition.maxSpeed * ship.SailingFactor);
            return Advance(seconds);
        }

        // Role: 시간·일지·취침 갱신이 끝난 후 계약을 포함한 입항 알림을 한 번 전달한다.
        public void NotifyArrival()
        {
            if (!arrivalPending)
                return;
            arrivalPending = false;
            VoyageCompleted?.Invoke(playthroughId + ":" + state.voyageSequence, state.dockedPort);
        }

        // Role: 세계 좌표를 이동하고 이동 선분의 첫 유효 항구에서 자동 입항한다.
        private float Advance(float delta)
        {
            Vector2 previous = state.position;
            float radians = state.heading * Mathf.Deg2Rad;
            Vector2 next = previous + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * state.speed * delta;
            next.x = Mathf.Clamp(next.x, definition.bounds.xMin, definition.bounds.xMax);
            next.y = Mathf.Clamp(next.y, definition.bounds.yMin, definition.bounds.yMax);
            PortDefinition destination = null;
            float earliest = float.PositiveInfinity;
            foreach (PortDefinition port in definition.ports)
            {
                if (port.id == state.departurePort && !state.departureCleared)
                    continue;
                if (EntryFraction(previous, next, port.position, port.arrivalRadius, out float fraction) && fraction < earliest)
                {
                    earliest = fraction;
                    destination = port;
                }
            }

            state.position = next;
            if (destination != null)
            {
                state.position = destination.position;
                state.speed = 0;
                state.dockedPort = destination.id;
                IsSteering = false;
                if (state.completedSequence != state.voyageSequence)
                {
                    state.completedSequence = state.voyageSequence;
                    arrivalPending = true;
                }

                return delta * Mathf.Clamp01(earliest);
            }

            PortDefinition departure = Port(state.departurePort);
            if (Vector2.Distance(next, departure.position) > departure.departureRadius)
                state.departureCleared = true;
            if (next == previous && state.speed > 0)
                state.speed = 0;
            return delta;
        }

        // Role: 긴 프레임에도 항구를 건너뛰지 않도록 선분과 원의 첫 교차 비율을 구한다.
        private static bool EntryFraction(Vector2 from, Vector2 to, Vector2 center, float radius, out float fraction)
        {
            Vector2 relative = from - center;
            fraction = 0;
            if (relative.sqrMagnitude <= radius * radius)
                return true;
            Vector2 direction = to - from;
            float a = direction.sqrMagnitude;
            if (a <= Mathf.Epsilon)
                return false;
            float b = Vector2.Dot(relative, direction);
            float c = relative.sqrMagnitude - radius * radius;
            float discriminant = b * b - a * c;
            if (discriminant < 0)
                return false;
            fraction = (-b - Mathf.Sqrt(discriminant)) / a;
            return fraction >= 0 && fraction <= 1;
        }

        // Role: 승선 중에만 방향타 조작을 시작한다.
        public void BeginSteering()
        {
            if (IsReady && IsAboard && HelmOperational && !session.IsPaused)
                IsSteering = true;
        }

        // Role: 이동 중인 배의 속도·방향을 유지하면서 조타 입력만 종료한다.
        public void EndSteering() => IsSteering = false;
        // Role: 저장된 위치가 현재 공간의 경계 안에 있는지 검사한다.
        public bool ValidPlayerPosition(Vector2 position) => IsAboard ? ship.ValidPosition(ship.ActiveFloor, position) : Finite(position);
        // Role: 지도와 계획점의 해역 좌표를 검증한다.
        public bool ValidWorldPosition(Vector2 position) => Finite(position) && Inside(definition.bounds, position);
        // Role: 안정 항구 ID로 정의를 조회한다.
        private PortDefinition Port(string id) => Array.Find(definition.ports, port => port.id == id);
        // Role: 경계의 최댓값도 포함해 위치를 검사한다.
        private static bool Inside(Rect bounds, Vector2 position) => position.x >= bounds.xMin && position.x <= bounds.xMax && position.y >= bounds.yMin && position.y <= bounds.yMax;
        // Role: 양수 크기의 유한 직사각형인지 검사한다.
        private static bool ValidRect(Rect rect) => Finite(rect.position) && Finite(rect.size) && rect.width > 0 && rect.height > 0;
        // Role: 좌표가 유한한지 검사한다.
        private static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        // Role: 수치가 유한한지 검사한다.
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public void SetAboard(bool aboard)
        {
            if (!IsDocked || state.aboard == aboard)
                return;
            if (!aboard)
            {
                ship.RememberPosition();
                state.boardingPosition = session.State.player.position;
            }

            state.aboard = aboard;
            ship.SetAboard(aboard);
        }

        public void Steer(Vector2 axes, float seconds)
        {
            if (!IsSteering || !HelmOperational || session.IsSleeping || session.IsPaused)
                return;
            state.heading = Mathf.Repeat(state.heading - axes.x * definition.turnSpeed * seconds, 360);
            if (IsDocked && axes.y > 0)
                Depart();
            if (!IsDocked)
                state.speed = Mathf.Clamp(state.speed + axes.y * definition.acceleration * seconds, 0, definition.maxSpeed);
        }
    }
}
