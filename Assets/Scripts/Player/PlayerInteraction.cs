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
        [SerializeField, Tooltip("조타 입력과 승하선을 관리하는 항해 세션.")]
        private NavigationSession navigation;
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
            bool steering = navigation != null && navigation.IsSteering;
            movement.SetInputBlocked(journal.IsOpen || steering);
            if (journal.IsOpen || (session != null && (session.IsPaused || session.IsSleeping)))
            {
                journal.ShowPrompt(false);
                return;
            }
            if (steering)
            {
                journal.ShowPrompt(false);
                if (interactAction.WasPressedThisFrame() || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
                {
                    navigation.EndSteering();
                    movement.SetInputBlocked(false);
                }
                return;
            }

            JournalDesk nearest = null;
            Stairway nearestStair = null;
            NavigationPoint nearestNavigation = null;
            CrewView nearestCrew = null;
            PortServicePoint nearestService = null;
            BedInteraction nearestBed = null;
            float range = interactionRange * ship.CellSize;
            float nearestDistance = range;
            Vector2 origin = transform.position;
            int count = Physics2D.OverlapCircle(origin, range, filter, candidates);
            for (int i = 0; i < count; i++)
            {
                JournalDesk desk = candidates[i].GetComponentInParent<JournalDesk>();
                Stairway stair = candidates[i].GetComponentInParent<Stairway>();
                NavigationPoint navigationPoint = candidates[i].GetComponentInParent<NavigationPoint>();
                CrewView crew = candidates[i].GetComponentInParent<CrewView>();
                PortServicePoint service = candidates[i].GetComponentInParent<PortServicePoint>();
                if (service != null && !service.Available) service = null;
                BedInteraction bed = candidates[i].GetComponentInParent<BedInteraction>();
                if (bed != null && !bed.Available) bed = null;
                if (crew != null && !crew.Available) crew = null;
                if (navigationPoint != null && !navigationPoint.Available) navigationPoint = null;
                if (desk == null && stair == null && navigationPoint == null && crew == null && service == null && bed == null) continue;
                Vector2 point = bed != null ? bed.InteractionPosition : desk != null ? desk.InteractionPosition : stair != null ? stair.InteractionPosition : navigationPoint != null ? navigationPoint.InteractionPosition : crew != null ? crew.InteractionPosition : service.InteractionPosition;
                Transform target = bed != null ? bed.transform : desk != null ? desk.transform : stair != null ? stair.transform : navigationPoint != null ? navigationPoint.transform : crew != null ? crew.transform : service.transform;
                float distance = Vector2.Distance(origin, point);
                if (distance > nearestDistance || !CanReach(target, point)) continue;
                nearest = desk;
                nearestStair = stair;
                nearestNavigation = navigationPoint;
                nearestCrew = crew;
                nearestService = service;
                nearestBed = bed;
                nearestDistance = distance;
            }

            journal.ShowPrompt(nearest != null || nearestStair != null || nearestNavigation != null || nearestCrew != null || nearestService != null || nearestBed != null,
                nearestBed != null ? nearestBed.Prompt : nearestService != null ? nearestService.Prompt : nearestCrew != null ? nearestCrew.Prompt : nearestNavigation != null ? nearestNavigation.Prompt : nearestStair != null ? nearestStair.Prompt : "[E] 항해 일지 열기");
            if (interactAction.WasPressedThisFrame() && nearest == null && nearestBed == null && (nearestService != null || nearestCrew != null || nearestNavigation != null || nearestStair != null)) session?.MarkInteraction();
            if (nearestBed != null && interactAction.WasPressedThisFrame()) nearestBed.Interact();
            else if (nearestService != null && interactAction.WasPressedThisFrame()) nearestService.Interact();
            else if (nearestCrew != null && interactAction.WasPressedThisFrame()) nearestCrew.Interact();
            else if (nearestNavigation != null && interactAction.WasPressedThisFrame())
            {
                nearestNavigation.Interact();
                movement.SetInputBlocked(navigation != null && navigation.IsSteering);
            }
            else if (nearestStair != null && interactAction.WasPressedThisFrame()) nearestStair.Travel();
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
