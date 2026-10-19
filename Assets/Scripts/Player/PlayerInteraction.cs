using UnityEngine;
using UnityEngine.InputSystem;

namespace GreatVoyage
{
    [RequireComponent(typeof(PlayerMovement))]
    public sealed class PlayerInteraction : MonoBehaviour
    {
        [SerializeField, Tooltip("이 플레이어가 열 일지 화면.")]
        private JournalPanel journal;
        [SerializeField, Tooltip("책상 상호작용 지점까지의 최대 거리.")]
        private float interactionRange = 1.25f;
        [SerializeField, Tooltip("정지 중 새 게임 상호작용을 차단할 세션.")]
        private GameSession session;

        private readonly Collider2D[] candidates = new Collider2D[16];
        private readonly RaycastHit2D[] obstacles = new RaycastHit2D[16];
        private PlayerMovement movement;
        private InputAction interactAction;
        private ContactFilter2D filter;

        // Role: 상호작용 키와 트리거를 제외한 공간 검색 조건을 준비한다.
        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            filter = new ContactFilter2D { useTriggers = false };
        }

        // Role: 상호작용 입력을 활성화한다.
        private void OnEnable() => interactAction.Enable();

        // Role: 가장 가까운 접근 가능한 책상을 안내하고 일지 열기를 요청한다.
        private void Update()
        {
            if (journal == null) return;
            movement.SetInputBlocked(journal.IsOpen);
            if (journal.IsOpen || (session != null && session.IsPaused))
            {
                journal.ShowPrompt(false);
                return;
            }

            JournalDesk nearest = null;
            float nearestDistance = interactionRange;
            Vector2 origin = transform.position;
            int count = Physics2D.OverlapCircle(origin, interactionRange, filter, candidates);
            for (int i = 0; i < count; i++)
            {
                JournalDesk desk = candidates[i].GetComponentInParent<JournalDesk>();
                if (desk == null) continue;
                float distance = Vector2.Distance(origin, desk.InteractionPosition);
                if (distance > nearestDistance || !CanReach(desk)) continue;
                nearest = desk;
                nearestDistance = distance;
            }

            journal.ShowPrompt(nearest != null);
            if (nearest != null && interactAction.WasPressedThisFrame())
            {
                journal.Open();
                movement.SetInputBlocked(true);
            }
        }

        // Role: 플레이어와 책상 사이에 다른 충돌체가 있는지 확인한다.
        private bool CanReach(JournalDesk desk)
        {
            int count = Physics2D.Linecast(transform.position, desk.InteractionPosition, filter, obstacles);
            for (int i = 0; i < count; i++)
            {
                Transform hit = obstacles[i].collider.transform;
                if (hit.IsChildOf(transform) || hit.IsChildOf(desk.transform)) continue;
                return false;
            }
            return count < obstacles.Length;
        }

        // Role: 입력과 상호작용 안내를 정리한다.
        private void OnDisable()
        {
            interactAction.Disable();
            if (journal != null) journal.ShowPrompt(false);
            if (movement != null) movement.SetInputBlocked(false);
        }

        // Role: 상호작용 입력 자원을 해제한다.
        private void OnDestroy() => interactAction?.Dispose();
    }
}
