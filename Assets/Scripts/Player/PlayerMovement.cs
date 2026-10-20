using UnityEngine;
using UnityEngine.InputSystem;

namespace GreatVoyage
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField, Tooltip("선내 이동 속도(초당 Unity 단위).")]
        private float moveSpeed = 3.5f;
        [SerializeField, Tooltip("외부의 밀림을 즉시 덮어쓰지 않도록 제한하는 이동·감속 가속도.")]
        private float acceleration = 18;
        [SerializeField, Tooltip("UI 외의 정지 사유도 이동에 반영할 게임 세션.")]
        private GameSession session;
        [SerializeField, Tooltip("새 공통 실행에서는 Composition이 입력을 공급한다. 기존 Scene은 자체 입력을 유지한다.")]
        private bool useSharedInput;

        public bool UsesSharedInput => useSharedInput;
        private GameInput sharedInput;

        private Rigidbody2D body;
        private InputAction moveAction;
        private bool inputBlocked;

        // Role: 이동 입력과 물리 컴포넌트를 준비한다.
        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            if (useSharedInput) return;
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        }

        // Role: 활성화된 플레이어의 이동 입력을 받는다.
        private void OnEnable() => moveAction?.Enable();

        // Role: 입력을 정규화해 충돌이 처리되는 물리 속도로 전달한다.
        private void FixedUpdate()
        {
            if (inputBlocked || (session != null && (session.IsPaused || session.IsSleeping))) { body.linearVelocity = Vector2.zero; return; }
            Vector2 direction = useSharedInput ? sharedInput?.Move ?? Vector2.zero : moveAction.ReadValue<Vector2>();
            Vector2 desired = Vector2.ClampMagnitude(direction, 1f) * moveSpeed;
            Vector2 force = Vector2.ClampMagnitude((desired - body.linearVelocity) / Time.fixedDeltaTime, acceleration) * body.mass;
            body.AddForce(force, ForceMode2D.Force);
        }

        // Role: 일지 UI가 열릴 때 즉시 멈추고 이동 입력을 차단한다.
        public void SetInputBlocked(bool blocked)
        {
            inputBlocked = blocked;
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (blocked) body.linearVelocity = Vector2.zero;
        }

        // Role: 공통 입력을 명시적으로 연결한다. 기존 Scene의 입력 수명에는 관여하지 않는다.
        public void BindInput(GameInput input) => sharedInput = input;

        // Role: 비활성화된 플레이어의 입력과 잔여 이동을 정리한다.
        private void OnDisable()
        {
            moveAction?.Disable();
            body.linearVelocity = Vector2.zero;
        }

        // Role: 플레이어가 소유한 입력 자원을 해제한다.
        private void OnDestroy() => moveAction?.Dispose();
    }
}
