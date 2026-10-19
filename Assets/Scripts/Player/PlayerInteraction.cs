using UnityEngine;
using UnityEngine.InputSystem;

namespace GreatVoyage
{
    [RequireComponent(typeof(PlayerMovement))]
    public sealed class PlayerInteraction : MonoBehaviour
    {
        [SerializeField, Tooltip("이 플레이어가 열 일지 화면.")]
        private JournalPanel journal;
        [SerializeField, Tooltip("상호작용 지점까지의 최대 거리. Cell 단위.")]
        private float interactionRange = 1.25f;
        [SerializeField, Tooltip("상호작용 거리를 Unity 단위로 환산할 함선.")]
        private ShipLayout ship;
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

        // Role: 가장 가까운 책상 또는 계단을 안내하고 해당 행동을 요청한다.
        private void Update()
        {
            if (journal == null || ship == null) return;
            movement.SetInputBlocked(journal.IsOpen);
            if (journal.IsOpen || (session != null && session.IsPaused))
            {
                journal.ShowPrompt(false);
                return;
            }

            JournalDesk nearest = null;
            Stairway nearestStair = null;
            float range = interactionRange * ship.CellSize;
            float nearestDistance = range;
            Vector2 origin = transform.position;
            int count = Physics2D.OverlapCircle(origin, range, filter, candidates);
            for (int i = 0; i < count; i++)
            {
                JournalDesk desk = candidates[i].GetComponentInParent<JournalDesk>();
                Stairway stair = candidates[i].GetComponentInParent<Stairway>();
                if (desk == null && stair == null) continue;
                Vector2 point = desk != null ? desk.InteractionPosition : stair.InteractionPosition;
                Transform target = desk != null ? desk.transform : stair.transform;
                float distance = Vector2.Distance(origin, point);
                if (distance > nearestDistance || !CanReach(target, point)) continue;
                nearest = desk;
                nearestStair = stair;
                nearestDistance = distance;
            }

            journal.ShowPrompt(nearest != null || nearestStair != null,
                nearestStair != null ? nearestStair.Prompt : "[E] 항해 일지 열기");
            if (nearestStair != null && interactAction.WasPressedThisFrame()) nearestStair.Travel();
            else if (nearest != null && interactAction.WasPressedThisFrame())
            {
                journal.Open();
                movement.SetInputBlocked(true);
            }
        }

        // Role: 플레이어와 대상 사이에 다른 충돌체가 있는지 확인한다.
        private bool CanReach(Transform target, Vector2 position)
        {
            int count = Physics2D.Linecast(transform.position, position, filter, obstacles);
            for (int i = 0; i < count; i++)
            {
                Transform hit = obstacles[i].collider.transform;
                if (hit.IsChildOf(transform) || hit.IsChildOf(target)) continue;
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
