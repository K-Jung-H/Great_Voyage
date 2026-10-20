using UnityEngine;

namespace GreatVoyage
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameComposition : MonoBehaviour
    {
        [SerializeField, Tooltip("독립 편집 가능한 선내 공간 루트.")]
        private Transform world;
        [SerializeField, Tooltip("공간 표시 수명과 분리된 캐릭터 루트.")]
        private Transform characters;
        [SerializeField, Tooltip("공통 행동 입력을 받는 플레이어 물리 모터.")]
        private PlayerMovement player;
        [SerializeField, Tooltip("플레이어의 거리·차폐 기반 상호작용 조회.")]
        private InteractionController interactions;
        [SerializeField, Tooltip("플레이어를 따라가는 공통 카메라.")]
        private CameraFollow cameraFollow;
        [SerializeField, Tooltip("사전 구성한 화면과 입력 포커스.")]
        private UIRoot ui;
        [SerializeField, Tooltip("S1 대표 상호작용인 선장실 책상.")]
        private JournalDesk journalDesk;

        private GameInput input;
        private GameFlow flow;
        private bool started;

        private void Awake()
        {
            if (world == null || characters == null || world == characters ||
                player == null || !player.UsesSharedInput || !player.transform.IsChildOf(characters) ||
                interactions == null || interactions.gameObject != player.gameObject ||
                cameraFollow == null || ui == null || !ui.IsConfigured ||
                journalDesk == null || !journalDesk.transform.IsChildOf(world))
            {
                Debug.LogError("GameComposition: 공통 실행 구성의 필수 참조/수명 계약을 확인하세요.", this);
                enabled = false;
                return;
            }
            input = new GameInput();
            player.BindInput(input);
            cameraFollow.BindTarget(player.transform);
            flow = new GameFlow(input, player, interactions, ui);
            journalDesk.OpenRequested += flow.OpenJournal;
        }

        private void Start() { started = true; flow?.Activate(); }
        private void OnEnable() { if (started) flow?.Activate(); }
        private void Update() => flow?.Tick();
        private void OnDisable() => flow?.Suspend();

        private void OnDestroy()
        {
            if (journalDesk != null && flow != null) journalDesk.OpenRequested -= flow.OpenJournal;
            flow?.Dispose();
            input?.Dispose();
        }
    }
}
