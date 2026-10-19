using UnityEngine;

namespace GreatVoyage
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class CrewView : MonoBehaviour
    {
        private CrewState state;
        private CrewSimulation simulation;
        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private SpriteRenderer[] renderers;
        private bool visible;
        public Vector2 InteractionPosition => body.position;
        public string Prompt => $"[E] {state.displayName} 주업무: {(state.primaryDuty == CrewDuty.None ? "없음 → " + (state.specialty == CrewSpecialty.Navigation ? "항해" : "정비") : "근무 → 없음")}";
        public bool Available => simulation != null && simulation.IsReady && state.availability == CrewAvailability.Ready && (state.specialty == CrewSpecialty.Maintenance || CrewSimulation.QualifiedNavigator(state));
        public bool IsSimulated => body != null && body.simulated;

        // Role: 표시·물리 인스턴스를 저장 가능한 선원 상태에 연결한다.
        public void Bind(CrewState member, CrewSimulation owner)
        {
            state = member;
            simulation = owner;
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            transform.localScale = Vector3.one * simulation.CellSize;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0;
            body.linearDamping = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (bodyCollider is CircleCollider2D circle) circle.radius = simulation.BodyRadius;
            body.simulated = false;
            bodyCollider.enabled = false;
            foreach (SpriteRenderer renderer in renderers) renderer.enabled = false;
            TeleportToState();
        }

        // Role: 표시 전환 경계에서만 상태와 물리 좌표를 동기화한다.
        public void SynchronizeVisibility(bool show)
        {
            if (visible == show) return;
            if (!show) CaptureState();
            visible = show;
            if (show) TeleportToState();
            else body.linearVelocity = Vector2.zero;
            bodyCollider.enabled = show;
            body.simulated = show;
            foreach (SpriteRenderer renderer in renderers) renderer.enabled = show;
        }

        // Role: 실제 물리 위치·속도를 저장 가능한 Cell 좌표로 수집한다.
        public void CaptureState()
        {
            if (!IsSimulated) return;
            state.position = body.position / simulation.CellSize;
            state.velocity = body.linearVelocity / simulation.CellSize;
        }

        // Role: Context steering이 결정한 Cell 속도를 물리 속도로 적용한다.
        public void ApplyVelocity(Vector2 velocity)
        {
            if (IsSimulated) body.linearVelocity = velocity * simulation.CellSize;
        }

        // Role: 가속 제한이 있는 힘으로 움직여 충돌로 생긴 밀림을 보존한다.
        public void DriveVelocity(Vector2 velocity, float acceleration, float mass)
        {
            body.mass = mass;
            if (!IsSimulated) return;
            Vector2 force = Vector2.ClampMagnitude((velocity * simulation.CellSize - body.linearVelocity) / Time.fixedDeltaTime,
                acceleration * simulation.CellSize) * body.mass;
            body.AddForce(force, ForceMode2D.Force);
        }

        // Role: 로드·층 전환·표시 재개 시에만 물리 위치를 상태로 복원한다.
        public void TeleportToState()
        {
            body.position = state.position * simulation.CellSize;
            body.linearVelocity = state.velocity * simulation.CellSize;
        }

        // Role: 선원 직접 상호작용으로 주 업무를 전환한다.
        public void Interact()
        {
            if (Available) simulation.SetDuty(state.id, state.primaryDuty == CrewDuty.None ? (state.specialty == CrewSpecialty.Navigation ? CrewDuty.Navigation : CrewDuty.Maintenance) : CrewDuty.None);
        }
    }
}
