using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GreatVoyage
{
    public sealed class NavigationSession : MonoBehaviour
    {
        [SerializeField, Tooltip("고정 항구·해역·조타 설정.")] private NavigationDefinition definition;
        [SerializeField, Tooltip("시간·정지·저장 세션.")] private GameSession session;
        [SerializeField, Tooltip("층의 표현을 전환할 함선.")] private ShipLayout ship;
        [SerializeField, Tooltip("승하선할 플레이어.")] private Rigidbody2D player;
        [SerializeField, Tooltip("입항한 항구의 공용 개발 공간. Ship 밖에 둔다.")] private GameObject portRoot;
        [SerializeField, Tooltip("항구 입장 위치. Cell 단위.")] private Vector2 shoreEntry = Vector2.zero;
        [SerializeField, Tooltip("개발 항구의 플레이어 영역. Cell 단위.")] private Rect shoreBounds = new Rect(-6, -3, 12, 6);
        [SerializeField, Tooltip("항해 위치·조타·입항 상태 HUD.")] private Text statusLabel;
        private NavigationState state;
        private string playthroughId;
        public bool IsReady => state != null && session.IsReady;
        public bool IsAboard => state == null || state.aboard;
        public bool IsDocked => state != null && !string.IsNullOrEmpty(state.dockedPort);
        public bool IsSteering { get; private set; }
        public event Action<string, string> VoyageCompleted;

        // Role: 정의와 저장 상태를 검증하고 현재 공간의 표시를 복원한다.
        public NavigationState Initialize(NavigationState saved, string playId)
        {
            ValidateDefinition();
            playthroughId = playId;
            state = saved ?? new NavigationState
            {
                definitionId = definition.id, position = definition.ports[0].position,
                dockedPort = definition.ports[0].id, aboard = true
            };
            ValidateState();
            IsSteering = false;
            ApplySpace();
            return state;
        }

        // Role: 유한 수치·항구 ID·반경·해역 및 필수 참조를 검사한다.
        private void ValidateDefinition()
        {
            if (definition == null || definition.ports == null || definition.ports.Length < 2 || session == null || ship == null || player == null || portRoot == null || statusLabel == null
                || !Finite(definition.maxSpeed) || definition.maxSpeed <= 0 || !Finite(definition.acceleration) || definition.acceleration <= 0 || !Finite(definition.turnSpeed) || definition.turnSpeed <= 0
                || !ValidRect(definition.bounds) || !ValidRect(shoreBounds) || !Finite(shoreEntry) || !Inside(shoreBounds, shoreEntry))
                throw new InvalidDataException("항해 정의 또는 필수 참조가 유효하지 않습니다.");
            for (int i = 0; i < definition.ports.Length; i++)
            {
                PortDefinition port = definition.ports[i];
                if (port == null || string.IsNullOrEmpty(port.id) || !Finite(port.position) || !Inside(definition.bounds, port.position)
                    || !Finite(port.arrivalRadius) || !Finite(port.departureRadius) || port.arrivalRadius <= 0 || port.departureRadius <= port.arrivalRadius)
                    throw new InvalidDataException("항구 정의가 유효하지 않습니다.");
                for (int j = 0; j < i; j++)
                    if (definition.ports[j].id == port.id || Vector2.Distance(definition.ports[j].position, port.position) <= definition.ports[j].departureRadius + port.departureRadius)
                        throw new InvalidDataException("항구 ID 또는 입출항 영역이 겹칩니다.");
            }
        }

        // Role: 저장된 항해 상태가 연결 가능한 항구·순번·좌표를 가졌는지 검사한다.
        private void ValidateState()
        {
            if (state.definitionId != definition.id || !Finite(state.position) || !Inside(definition.bounds, state.position) || !Finite(state.heading) || state.heading < 0 || state.heading >= 360
                || !Finite(state.speed) || state.speed < 0 || state.speed > definition.maxSpeed || !Finite(state.boardingPosition)
                || state.completedSequence < 0 || state.voyageSequence < state.completedSequence || (long)state.voyageSequence > (long)state.completedSequence + 1)
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

        // Role: 조타 입력과 항해를 정지 상태에 맞춰 진행하고 HUD를 갱신한다.
        private void Update()
        {
            if (!IsReady) return;
            float delta = Time.unscaledDeltaTime;
            if (!session.IsPaused)
            {
                Keyboard keys = Keyboard.current;
                if (IsSteering && keys != null)
                {
                    float turn = (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0) - (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0);
                    state.heading = Mathf.Repeat(state.heading + turn * definition.turnSpeed * delta, 360);
                    bool forward = keys.wKey.isPressed || keys.upArrowKey.isPressed;
                    bool slow = keys.sKey.isPressed || keys.downArrowKey.isPressed;
                    if (IsDocked && forward) Depart();
                    if (!IsDocked) state.speed = Mathf.Clamp(state.speed + ((forward ? 1 : 0) - (slow ? 1 : 0)) * definition.acceleration * delta, 0, definition.maxSpeed);
                }
                if (!IsDocked) Advance(delta);
            }
            PortDefinition dock = Port(state.dockedPort);
            string mode = dock != null ? $"{dock.displayName} · 입항 · {(IsAboard ? "승선 중" : "항구")}" : "항해 중";
            statusLabel.text = $"{mode}\n세계 ({state.position.x:0.0}, {state.position.y:0.0})  방향 {state.heading:0}°  속도 {state.speed:0.0}\n완료 항해 {state.completedSequence}회"
                + (IsSteering ? "\n조타: A/D 회전 · W 출항/가속 · S 감속 · E/Esc 조타 종료" : "\n항구 A (0, 0) · 항구 B (30, 0)");
        }

        // Role: 출항지를 기록하고 고유 항해 순번으로 항해를 시작한다.
        private void Depart()
        {
            if (!IsDocked || !IsAboard || state.voyageSequence == int.MaxValue) return;
            state.departurePort = state.dockedPort;
            state.dockedPort = null;
            state.departureCleared = false;
            state.voyageSequence++;
            state.speed = definition.maxSpeed;
        }

        // Role: 세계 좌표를 이동하고 이동 선분의 첫 유효 항구에서 자동 입항한다.
        private void Advance(float delta)
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
                if (port.id == state.departurePort && !state.departureCleared) continue;
                if (EntryFraction(previous, next, port.position, port.arrivalRadius, out float fraction) && fraction < earliest)
                { earliest = fraction; destination = port; }
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
                    VoyageCompleted?.Invoke(playthroughId + ":" + state.voyageSequence, destination.id);
                }
                return;
            }
            PortDefinition departure = Port(state.departurePort);
            if (Vector2.Distance(next, departure.position) > departure.departureRadius) state.departureCleared = true;
            if (next == previous && state.speed > 0) state.speed = 0;
        }

        // Role: 긴 프레임에도 항구를 건너뛰지 않도록 선분과 원의 첫 교차 비율을 구한다.
        private static bool EntryFraction(Vector2 from, Vector2 to, Vector2 center, float radius, out float fraction)
        {
            Vector2 relative = from - center;
            fraction = 0;
            if (relative.sqrMagnitude <= radius * radius) return true;
            Vector2 direction = to - from;
            float a = direction.sqrMagnitude;
            if (a <= Mathf.Epsilon) return false;
            float b = Vector2.Dot(relative, direction);
            float c = relative.sqrMagnitude - radius * radius;
            float discriminant = b * b - a * c;
            if (discriminant < 0) return false;
            fraction = (-b - Mathf.Sqrt(discriminant)) / a;
            return fraction >= 0 && fraction <= 1;
        }

        // Role: 승선 중에만 방향타 조작을 시작한다.
        public void BeginSteering()
        {
            if (IsReady && IsAboard && !session.IsPaused) IsSteering = true;
        }

        // Role: 이동 중인 배의 속도·방향을 유지하면서 조타 입력만 종료한다.
        public void EndSteering() => IsSteering = false;

        // Role: 입항한 상태에서 선내 위치를 보존하며 항구와 함선을 왕복한다.
        public void SetAboard(bool aboard)
        {
            if (!IsReady || !IsDocked || session.IsPaused || state.aboard == aboard) return;
            if (!aboard) { ship.RememberPosition(); state.boardingPosition = player.position; }
            state.aboard = aboard;
            IsSteering = false;
            ApplySpace();
            Vector2 position = aboard ? state.boardingPosition : shoreEntry * ship.CellSize;
            player.position = position;
            player.transform.position = new Vector3(position.x, position.y, player.transform.position.z);
            player.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        // Role: 저장된 위치가 현재 공간의 경계 안에 있는지 검사한다.
        public bool ValidPlayerPosition(Vector2 position) => IsAboard ? ship.ValidPosition(ship.ActiveFloor, position) : Finite(position) && Inside(shoreBounds, position / ship.CellSize);

        // Role: 항구 또는 현재 층의 표현·충돌체만 활성화한다.
        private void ApplySpace()
        {
            ship.SetAboard(state.aboard);
            portRoot.transform.localScale = Vector3.one * ship.CellSize;
            portRoot.SetActive(!state.aboard);
        }

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
    }
}
