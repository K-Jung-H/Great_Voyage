using System;
using System.IO;
using UnityEngine;

namespace GreatVoyage
{
    [DefaultExecutionOrder(-200)]
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField, Tooltip("저장·복원할 플레이어의 물리 위치.")]
        private Rigidbody2D player;
        [SerializeField, Tooltip("현재 공간의 안정적인 저장 식별자.")]
        private string spaceId = "captains-cabin";
        [SerializeField, Tooltip("층과 시설 상태를 복원할 함선.")]
        private ShipLayout ship;
        [SerializeField, Tooltip("별도 세계 좌표의 항해·출입항 상태.")]
        private NavigationSession navigation;
        [SerializeField, Tooltip("선원의 상태 갱신·시설 업무·생활을 관리한다.")]
        private CrewSimulation crew;
        [SerializeField, Tooltip("저장 파일 이름(확장자 제외). 개발 씬은 별도 슬롯을 사용한다.")]
        private string saveSlot = "cabin-session";
        [SerializeField, Tooltip("선원 개발 씬에서만 사용자 재준비 버튼을 허용한다.")]
        private bool allowCrewPrototypeReset;

        [SerializeField, Tooltip("항구 거래·계약. 기존 개발 씬에서는 생략 가능.")] private EconomySession economy;
        [SerializeField] private JournalSession journal;
        [SerializeField] private PlayerSleep sleep;
        public bool IsSleeping => sleep != null && sleep.IsSleeping;
        public double EventBoundary { get; private set; } = double.PositiveInfinity;
        public event Action EventBoundaryReached;
        // Role: 경제 시스템이 연결된 경우 출항 전 미정산 상태를 검사한다.
        public bool CanDepart() => economy == null || economy.CanDepart();

        public const double SecondsPerDay = 600d;
        public PauseService Pauses { get; } = new PauseService();
        public bool IsPaused => Pauses.IsPaused;
        public string ErrorMessage { get; private set; }
        public bool IsReady { get; private set; }
        public double ElapsedSeconds => state?.elapsedSeconds ?? 0d;
        public string SavePath => saves?.FilePath;

        private SaveData state;
        private SaveService saves;
        private IDisposable errorPause;
        private bool savedForQuit;

        // Role: 세션을 읽고 위치를 복원하며 읽기 실패 시 기존 데이터를 보존한다.
        private void Awake()
        {
            Pauses.Changed += StopPlayerWhenPaused;
            string folder = Application.isEditor ? "Editor" : "Player";
            try
            {
                if (string.IsNullOrEmpty(saveSlot) || saveSlot.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new InvalidOperationException("저장 슬롯 이름이 유효하지 않습니다.");
                saves = new SaveService(Path.Combine(Application.persistentDataPath, "Saves", folder, saveSlot + ".json"));
                if (player == null) throw new InvalidOperationException("플레이어 참조가 연결되지 않았습니다.");
                if (ship == null) throw new InvalidOperationException("함선 참조가 연결되지 않았습니다.");
                state = saves.Load(spaceId) ?? new SaveData
                {
                    playthroughId = Guid.NewGuid().ToString("D"), spaceId = spaceId,
                    playerX = player.position.x * ship.CellSize, playerY = player.position.y * ship.CellSize
                };
                state.ship = ship.Initialize(state.ship);
                if (navigation == null) throw new InvalidOperationException("항해 참조가 연결되지 않았습니다.");
                state.navigation = navigation.Initialize(state.navigation, state.playthroughId);
                if (!navigation.ValidPlayerPosition(new Vector2(state.playerX, state.playerY)))
                    throw new InvalidDataException("저장 위치가 현재 공간의 경계 밖에 있습니다.");
                if (crew == null) throw new InvalidOperationException("선원 시뮬레이션 참조가 연결되지 않았습니다.");
                if ((journal == null) != (sleep == null)) throw new InvalidDataException("일지와 취침 참조는 함께 연결해야 합니다.");
                if (journal != null) state.journal = journal.Initialize(state.journal);
                state.crew = crew.Initialize(state.crew);
                if (economy != null) state.economy = economy.Initialize(state.economy);
                else if (state.economy != null) throw new InvalidDataException("경제 저장을 복원할 시스템 참조가 없습니다.");
                state.economyEnabled = economy != null;
                if (journal != null)
                {
                    player.position = new Vector2(state.playerX, state.playerY);
                    state.sleep = sleep.Initialize(state.sleep);
                }
                else if (state.journalEnabled) throw new InvalidDataException("일지 저장을 복원할 시스템이 없습니다.");
                state.journalEnabled = journal != null;
                player.position = new Vector2(state.playerX, state.playerY);
                player.linearVelocity = Vector2.zero;
                IsReady = true;
            }
            catch (Exception error)
            {
                ErrorMessage = "저장을 불러오지 못했습니다. 기존 파일은 보존됩니다.\n" + error.Message;
                errorPause = Pauses.Acquire("LoadError");
                Debug.LogError(ErrorMessage, this);
            }
        }

        // Role: 정상 종료 전에 저장할 수 있도록 종료 요청을 구독한다.
        private void OnEnable() => Application.wantsToQuit += PrepareQuit;

        // Role: 정지 사유가 없을 때만 게임 시간을 진행한다.
        private void Update()
        {
            if (IsReady && !IsPaused)
            {
                float budget = Mathf.Min(Time.unscaledDeltaTime, .25f) * (sleep != null ? sleep.TimeScale : 1);
                bool fast = IsSleeping;
                while (budget > .000001f && !IsPaused)
                {
                    float step = Mathf.Min(budget, .05f);
                    if (sleep != null) step = sleep.LimitStep(step);
                    step = (float)Math.Min(step, Math.Max(0, EventBoundary - state.elapsedSeconds));
                    if (step <= .000001f)
                    {
                        if (EventBoundary <= state.elapsedSeconds + .00001)
                        { EventBoundary = double.PositiveInfinity; EventBoundaryReached?.Invoke(); if (IsSleeping) sleep.Wake(); break; }
                        sleep?.Advance(0); break;
                    }
                    Vector2 previous = navigation.WorldPosition;
                    bool moving = !navigation.IsDocked;
                    float consumed = navigation.AdvanceTime(step);
                    state.elapsedSeconds += consumed;
                    crew.Advance(consumed, state.elapsedSeconds);
                    if (fast) crew.AdvanceSleepMovement(consumed);
                    int day = (int)(state.elapsedSeconds / SecondsPerDay) + 1;
                    if (moving) journal?.ObserveMovement(previous, navigation.WorldPosition, sleep != null ? sleep.RecordDay(day) : day,
                        sleep != null && sleep.State.mode != PlayerRest.Awake);
                    bool arrived = moving && navigation.IsDocked;
                    if (arrived) { journal?.Arrive(navigation.DockedPort); sleep?.Wake(); }
                    else sleep?.Advance(consumed);
                    navigation.NotifyArrival();
                    budget -= consumed;
                    if (arrived || (fast && !IsSleeping) || consumed <= 0) break;
                }
            }
        }

        // Role: 다음 단계 사건 시스템이 정확한 발생 시각에 진행을 중단하도록 예약한다.
        public void ScheduleEventBoundary(double elapsed)
        {
            if (IsReady && !double.IsNaN(elapsed) && !double.IsInfinity(elapsed) && elapsed >= state.elapsedSeconds) EventBoundary = elapsed;
        }

        // Role: 기록 이후 사용자 상호작용을 취침 이월 자격에 반영한다.
        public void MarkInteraction() => journal?.MarkInteraction();

        // Role: 정지 등록 즉시 현재 물리 이동을 멈춘다.
        private void StopPlayerWhenPaused()
        {
            if (IsPaused && player != null) player.linearVelocity = Vector2.zero;
        }

        // Role: 현재 위치·시간을 저장하고 실패하면 진행을 멈춰 오류를 표시한다.
        public bool SaveCurrent()
        {
            if (!IsReady || player == null) return false;
            try
            {
                crew.CapturePositions();
                state.playerX = player.position.x;
                state.playerY = player.position.y;
                if (navigation.IsAboard) ship.RememberPosition();
                saves.Save(state, spaceId);
                ErrorMessage = null;
                errorPause?.Dispose();
                errorPause = null;
                return true;
            }
            catch (Exception error)
            {
                ErrorMessage = "저장하지 못했습니다. 종료를 중단했습니다.\n" + error.Message;
                if (errorPause == null) errorPause = Pauses.Acquire("SaveError");
                Debug.LogError(ErrorMessage, this);
                return false;
            }
        }

        // Role: 개발 버튼 요청 시 이전 상태를 보존하고 선원 테스트 상태를 재준비한다.
        public void RestoreCrewPrototype()
        {
            if (!IsReady || !allowCrewPrototypeReset || saveSlot != "crew-prototype" || !SaveCurrent()) return;
            state.crew = crew.RestorePrototype();
            state.elapsedSeconds = new SaveData().elapsedSeconds;
            SaveCurrent();
        }

        // Role: 저장 성공 후에만 플레이를 종료한다.
        public void SaveAndQuit()
        {
            if (!PrepareQuit()) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // Role: 운영체제 종료 요청도 저장 성공을 확인한 뒤 허용한다.
        private bool PrepareQuit()
        {
            if (savedForQuit) return true;
            if (!IsReady) return true;
            savedForQuit = SaveCurrent();
            return savedForQuit;
        }

        // Role: Editor의 정지 버튼처럼 종료 요청을 우회하는 종료에서도 마지막 상태를 저장한다.
        private void OnApplicationQuit()
        {
            if (!savedForQuit && IsReady) SaveCurrent();
        }

        // Role: 세션 비활성화 시 종료 콜백을 해제한다.
        private void OnDisable() => Application.wantsToQuit -= PrepareQuit;

        // Role: 정지 알림 구독을 정리한다.
        private void OnDestroy() => Pauses.Changed -= StopPlayerWhenPaused;
    }
}
