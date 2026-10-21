using System;
using UnityEngine;

namespace GreatVoyage
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameComposition : MonoBehaviour
    {
        [SerializeField, Tooltip("상태와 분리된 월드 공간 루트.")]
        private Transform world;
        [SerializeField, Tooltip("플레이어/동적 인물의 공통 부모.")]
        private Transform characters;
        [SerializeField, Tooltip("공통 플레이어 모터.")]
        private PlayerMovement player;
        [SerializeField, Tooltip("타입 분기 없는 거리·차폐 기반 조회.")]
        private InteractionController interactions;
        [SerializeField, Tooltip("공유 추적 카메라.")]
        private CameraFollow cameraFollow;
        [SerializeField, Tooltip("사전 제작 UI 루트.")]
        private UIRoot ui;
        [SerializeField, Tooltip("선장실 일지 접근점.")]
        private JournalDesk journalDesk;
        [SerializeField, Tooltip("정의/시작/재구성 전용 저장 정책.")]
        private GameStartProfile profile;
        [SerializeField, Tooltip("구역·인물·시설 표시의 소유자.")]
        private WorldView worldView;
        [SerializeField, Tooltip("관리 화면 정지·입력 포커스.")]
        private ScreenStack stack;
        [SerializeField, Tooltip("대표 경제 화면 연결.")]
        private EconomyScreens economyScreens;
        [SerializeField, Tooltip("일지 화면 연결.")]
        private JournalScreenController journalController;
        [SerializeField, Tooltip("오류/종료/게임 오버 화면.")]
        private SessionScreenController sessionScreens;
        [SerializeField, Tooltip("HUD 상태 표시.")]
        private HudPresenter hud;
        [SerializeField, Tooltip("조타·취침 상태의 맥락 표시.")]
        private ContextPresenter context;
        [SerializeField, Tooltip("선원 상태·지속 업무 지정 화면.")]
        private CrewScreenController crewScreens;
        [SerializeField, Tooltip("사전 제작된 사건 대화와 결과 표시.")]
        private EventScreenController eventScreens;
        [SerializeField, Tooltip("관전/처분/무장/포로 인계 화면.")]
        private BattleScreens battleScreens;
        [SerializeField, Tooltip("사전 제작 캐릭터/상태바/투사체 표현.")]
        private BattlePresentation battlePresentation;
        [SerializeField, Tooltip("층별 시설 Prefab 표시와 상호작용.")]
        private FacilityPresentation facilityPresentation;
        [SerializeField, Tooltip("조선소와 시설 편집 화면.")]
        private ShipyardScreens shipyardScreens;
        [SerializeField, Tooltip("시청 퀘스트 탭의 수주/납품 연결.")]
        private QuestScreens questScreens;
        [SerializeField, Tooltip("층별 유해 생물 표시.")]
        private PestPresentation pestPresentation;
        private GameInput input;
        private GameFlow flow;
        private GameRuntime game;
        private Rigidbody2D body;
        private bool started, savedForQuit;
        private void Awake()
        {
            try
            {
                if (!world || !characters || !player || !interactions || !cameraFollow || !ui || !ui.IsConfigured || !journalDesk || !profile || !worldView || !stack || !economyScreens || !journalController || !sessionScreens || !hud || !context || !crewScreens || !player.UsesSharedInput)
                    throw new InvalidOperationException("GameComposition references incomplete");
                body = player.GetComponent<Rigidbody2D>();
                ui.Initialize();
                input = new GameInput();
                player.BindInput(input);
                player.SetInputBlocked(true);
                cameraFollow.BindTarget(player.transform);
                game = new GameRuntime(profile, body.position * profile.ship.cellSize);
                player.BindCellSize(game.Ship.CellSize);
                interactions.BindCellSize(game.Ship.CellSize);
                stack.Bind(game);
                economyScreens.Bind(game);
                journalController.Bind(game);
                sessionScreens.Bind(game);
                hud.Bind(game);
                context.Bind(game, worldView);
                flow = new GameFlow(input, player, interactions, ui, game, worldView, stack, economyScreens, journalController, sessionScreens, context, battleScreens, shipyardScreens);
                journalDesk.OpenRequested += flow.OpenJournal;
                worldView.BindInteractions(flow.Interact, flow.Available);
                worldView.Bind(game);
                sessionScreens.QuitRequested += SaveAndQuit;
                crewScreens.Bind(game);
                if (!eventScreens)
                    throw new InvalidOperationException("Event screen reference incomplete");
                eventScreens.Bind(game);
                battleScreens.Bind(game);
                battlePresentation.Bind(game);
                facilityPresentation.Bind(game);
                shipyardScreens.Bind(game);
                questScreens.Bind(game);
                pestPresentation.Bind(game);
                worldView.CrewSelected += crewScreens.Open;
                if (game.State.economy.gameOver)
                    sessionScreens.ShowGameOver();
                else if (game.State.economy.settlementPending)
                    economyScreens.OpenSettlement();
                Application.wantsToQuit += PrepareQuit;
                game.Pauses.Changed += ApplyPause;
            }
            catch (Exception error)
            {
                Debug.LogError("Game initialization stopped: " + error.Message, this);
                if (player)
                    player.SetInputBlocked(true);
                if (sessionScreens)
                    sessionScreens.ShowError(error.Message);
                enabled = false;
            }
        }

        private void Start()
        {
            started = true;
            flow?.Activate();
        }

        private void ApplyPause()
        {
            if (game != null && game.IsPaused && player)
                player.SetInputBlocked(true);
        }

        private void OnEnable()
        {
            if (started)
                flow?.Activate();
        }

        private void Update()
        {
            if (game == null || flow == null)
                return;
            try
            {
                worldView.CapturePlayer();
                flow.Tick();
                game.Loop.Advance(Time.unscaledDeltaTime);
                worldView.Refresh();
                battlePresentation.Refresh();
                facilityPresentation.Refresh();
                pestPresentation.Refresh();
                hud.Refresh();
                context.Refresh();
            }
            catch (Exception error)
            {
                game.Fail(error);
            }
        }

        private void FixedUpdate()
        {
            if (game != null && game.IsReady)
                game.Crew.FixedStep(Time.fixedDeltaTime);
        }

        private void LateUpdate()
        {
            if (game == null)
                return;
            worldView.CapturePlayer();
            game.FlushSave();
        }

        private bool PrepareQuit()
        {
            if (game == null || !game.IsReady)
                return true;
            if (savedForQuit)
                return true;
            worldView.CapturePlayer();
            savedForQuit = game.FlushSave(true);
            return savedForQuit;
        }

        private void SaveAndQuit()
        {
            if (!PrepareQuit())
                return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }

        private void OnApplicationQuit()
        {
            if (!savedForQuit && game != null)
                PrepareQuit();
        }

        private void OnDisable() => flow?.Suspend();
        private void OnDestroy()
        {
            Application.wantsToQuit -= PrepareQuit;
            if (game != null)
                game.Pauses.Changed -= ApplyPause;
            if (journalDesk && flow != null)
                journalDesk.OpenRequested -= flow.OpenJournal;
            if (sessionScreens)
                sessionScreens.QuitRequested -= SaveAndQuit;
            if (worldView && crewScreens)
                worldView.CrewSelected -= crewScreens.Open;
            flow?.Dispose();
            game?.Dispose();
            input?.Dispose();
        }
    }
}
