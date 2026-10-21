using System;
using System.IO;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class RestSession
    {
        private readonly GameRuntime session;
        private readonly NavigationRules navigation;
        private readonly ShipModel ship;
        private readonly CrewBrain crew;
        private readonly JournalRules journal;
        private readonly PlayerLocation player;
        private readonly float speed = 20;
        public RestSession(GameRuntime session, NavigationRules navigation, ShipModel ship, CrewBrain crew, JournalRules journal, PlayerLocation player)
        {
            this.session = session;
            this.navigation = navigation;
            this.ship = ship;
            this.crew = crew;
            this.journal = journal;
            this.player = player;
        }

        public PlayerSleepState State { get; private set; }
        public bool IsSleeping => State != null && State.mode == PlayerRest.Sleeping;
        public float TimeScale => IsSleeping ? speed : 1;
        public string Status => State == null ? "" : IsSleeping ? "취침 중 · 입항/기상 시각에 중단" : State.mode == PlayerRest.Watch ? $"불침번 · 남은 순찰 {State.patrolRemaining:0.0} Cell · {State.watchRemaining:0}초 · 침대에서 종료" : "";

        private Vector2 patrolPosition;
        private string patrolFloor;
        // Role: 취침·불침번 상태와 침대 선점을 복원한다.
        public PlayerSleepState Initialize(PlayerSleepState saved)
        {
            if (session == null || navigation == null || ship == null || crew == null || journal == null || player == null || float.IsNaN(speed) || float.IsInfinity(speed) || speed < 1 || speed > 100)
                throw new InvalidDataException("취침 참조 또는 배속이 유효하지 않습니다.");
            State = saved ?? new PlayerSleepState();
            if (!Enum.IsDefined(typeof(PlayerRest), State.mode) || double.IsNaN(State.wakeAt) || double.IsInfinity(State.wakeAt) || !Finite(State.watchRemaining) || State.watchRemaining < 0 || !Finite(State.patrolRemaining) || State.patrolRemaining < 0 || (State.mode != PlayerRest.Awake && (!navigation.IsAboard || State.recordDay < 1 || State.wakeAt < 0)))
                throw new InvalidDataException("취침 저장 상태가 유효하지 않습니다.");
            if (State.mode != PlayerRest.Awake && (!ValidBed(State.bedId) || !crew.ReservePlayerBed(State.bedId)))
                throw new InvalidDataException("취침 중인 침대 상태를 복원할 수 없습니다.");
            patrolPosition = player.position;
            patrolFloor = ship.ActiveFloor;
            return State;
        }

        // Role: 정상 작동하는 현재 층 침대가 다른 인물에게 선점되지 않았는지 조회한다.
        public bool CanUseBed(string id)
        {
            FacilityState bed = ship.GetFacility(id);
            return ValidBed(id) && bed.floorId == ship.ActiveFloor && crew.PlayerBedAvailable(id);
        }

        // Role: 다른 층에서 불침번 중이어도 예약 침대의 기능 상태를 검증한다.
        private bool ValidBed(string id)
        {
            FacilityState bed = ship.GetFacility(id);
            return bed != null && navigation.IsAboard && bed.durability > 0 && ship.GetFacilityDefinition(bed.definitionId).usableAsBed;
        }

        // Role: 침대 사용 요청으로 취침 또는 불침번 종료를 시작한다.
        public void UseBed(string id)
        {
            if (!session.IsReady || session.IsPaused || IsSleeping || !CanUseBed(id) || !crew.ReservePlayerBed(id))
                return;
            bool returning = State.mode == PlayerRest.Watch;
            double now = session.ElapsedSeconds;
            if (!returning)
            {
                double morning = Math.Floor(now / GameRuntime.SecondsPerDay) * GameRuntime.SecondsPerDay + GameRuntime.SecondsPerDay / 4;
                State.wakeAt = morning > now ? morning : morning + GameRuntime.SecondsPerDay;
                State.recordDay = (int)(State.wakeAt / GameRuntime.SecondsPerDay) + 1;
                journal.PrepareSleep(State.recordDay);
            }
            else
            {
                if (State.patrolRemaining > 0)
                    State.missedWatch = true;
                journal.PrepareSleep(State.recordDay, true);
                if (State.bedId != id)
                    crew.ReleasePlayerBed(State.bedId);
            }

            State.bedId = id;
            State.mode = PlayerRest.Sleeping;
            player.linearVelocity = Vector2.zero;
            navigation.EndSteering();
            crew.HideForSleep();
            if (State.wakeAt <= now)
                Wake();
            session.RequestSave();
        }

        // Role: 기상 시각을 넘어 진행하지 않도록 이번 시간 간격을 제한한다.
        public float LimitStep(float seconds) => IsSleeping ? (float)Math.Min(seconds, Math.Max(0, State.wakeAt - session.ElapsedSeconds)) : seconds;
        // Role: 취침 기상 시각과 불침번 순찰·제한 시간을 갱신한다.
        public void Advance(float seconds)
        {
            if (IsSleeping && session.ElapsedSeconds >= State.wakeAt - .00001)
            {
                Wake();
                return;
            }

            if (State.mode != PlayerRest.Watch)
                return;
            if (State.watchRemaining > 0 && ship.ActiveFloor == patrolFloor)
                State.patrolRemaining = Mathf.Max(0, State.patrolRemaining - Vector2.Distance(player.position, patrolPosition) / ship.CellSize);
            State.watchRemaining = Mathf.Max(0, State.watchRemaining - seconds);
            if (State.watchRemaining == 0 && State.patrolRemaining > 0)
                State.missedWatch = true;
            patrolPosition = player.position;
            patrolFloor = ship.ActiveFloor;
        }

        // Role: 기상·입항·일반 사건 시 취침을 중단하고 침대 선점을 해제한다.
        public void Wake()
        {
            if (State == null)
                return;
            crew.ReleasePlayerBed(State.bedId);
            State.bedId = null;
            State.mode = PlayerRest.Awake;
        }

        // Role: 8단계의 불침번 사건에서 호출할 기상·순찰 요구 연결점이다.
        public void BeginWatch(float seconds, float distance)
        {
            if (!IsSleeping || !Finite(seconds) || !Finite(distance) || seconds <= 0 || distance < 0)
                return;
            State.mode = PlayerRest.Watch;
            State.missedWatch = false;
            State.watchRemaining = seconds;
            State.patrolRemaining = distance;
            patrolPosition = player.position;
            patrolFloor = ship.ActiveFloor;
        }

        // Role: 취침 기록 날짜를 실제 수면과 불침번 이동에 동일하게 적용한다.
        public int RecordDay(int currentDay) => State != null && State.mode != PlayerRest.Awake ? State.recordDay : currentDay;
        // Role: 시뮬레이션 시간에 사용할 유한 수치인지 검사한다.
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
