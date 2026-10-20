using UnityEngine;

namespace GreatVoyage
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class CrewMovement : MonoBehaviour, ICrewBody, IInteractable
    {
        private Rigidbody2D body;
        private Collider2D shape;
        private SpriteRenderer[] renderers;
        private CrewBrain brain;
        private float cellSize;
        public event System.Action<string> Selected;
        public CrewState State { get; private set; }
        public bool IsSimulated => body != null && body.simulated;
        public Transform InteractionRoot => transform;
        public Vector2 InteractionPosition => body.position;
        public string Prompt => "[E] " + State.displayName + " · 업무 " + State.primaryDuty;
        public bool Available => brain != null && brain.IsReady && State.availability == CrewAvailability.Ready;

        public void Bind(CrewState state, CrewBrain brain, float cellSize)
        {
            State = state;
            this.brain = brain;
            this.cellSize = cellSize;
            body = GetComponent<Rigidbody2D>();
            shape = GetComponent<Collider2D>();
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.simulated = false;
            shape.enabled = false;
            if (shape is CircleCollider2D circle)
                circle.radius = brain.BodyRadius;
            transform.localScale = Vector3.one * cellSize;
            foreach (var renderer in renderers)
                renderer.enabled = false;
            TeleportToState();
        }

        public void SynchronizeVisibility(bool show)
        {
            if (IsSimulated == show)
                return;
            if (!show)
                CaptureState();
            else
                TeleportToState();
            body.simulated = show;
            shape.enabled = show;
            if (!show)
                body.linearVelocity = Vector2.zero;
            foreach (var renderer in renderers)
                renderer.enabled = show;
        }

        public void CaptureState()
        {
            if (IsSimulated)
            {
                State.position = body.position / cellSize;
                State.velocity = body.linearVelocity / cellSize;
            }
        }

        public void TeleportToState()
        {
            body.position = State.position * cellSize;
            body.linearVelocity = State.velocity * cellSize;
        }

        public void ApplyVelocity(Vector2 velocity)
        {
            if (IsSimulated)
                body.linearVelocity = velocity * cellSize;
        }

        public void DriveVelocity(Vector2 velocity, float acceleration, float mass)
        {
            body.mass = mass;
            if (IsSimulated)
                body.AddForce(Vector2.ClampMagnitude((velocity * cellSize - body.linearVelocity) / Time.fixedDeltaTime, acceleration * cellSize) * mass, ForceMode2D.Force);
        }

        private readonly RaycastHit2D[] hits = new RaycastHit2D[24];
        public float ObstacleDistance(Vector2 origin, Vector2 direction, float radius, float distance)
        {
            if (!IsSimulated)
                return distance;
            int count = Physics2D.CircleCast(origin * cellSize, radius * cellSize, direction, new ContactFilter2D { useTriggers = false }, hits, distance * cellSize);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider.GetComponentInParent<CrewMovement>())
                    continue;
                if (hit.distance == 0 && Vector2.Dot(direction, origin * cellSize - hit.collider.ClosestPoint(origin * cellSize)) > 0)
                    continue;
                distance = Mathf.Min(distance, hit.distance / cellSize);
            }

            return distance;
        }

        public void Interact()
        {
            if (Available)
                Selected?.Invoke(State.id);
        }
    }
}
