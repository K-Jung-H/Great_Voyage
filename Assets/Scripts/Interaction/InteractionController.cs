using System.Collections.Generic;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class InteractionController : MonoBehaviour
    {
        [SerializeField, Min(0.01f), Tooltip("현재 공간의 Unity 단위 기준 상호작용 거리. S3 공간 바인딩에서 Cell을 환산한다.")]
        private float interactionRange = 1.25f;
        [SerializeField, Tooltip("상호작용 대상과 시야를 막는 물리 계층.")]
        private LayerMask interactionMask = ~0;

        private readonly List<Collider2D> candidates = new List<Collider2D>(16);
        private readonly List<RaycastHit2D> obstacles = new List<RaycastHit2D>(16);

        // Role: 유형별 분기 없이 거리와 차폐 조건을 만족하는 가장 가까운 대상을 조회한다.
        public IInteractable FindTarget()
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(interactionMask);
            Physics2D.OverlapCircle(transform.position, interactionRange, filter, candidates);
            IInteractable nearest = null;
            float distanceLimit = interactionRange;
            foreach (Collider2D candidate in candidates)
            {
                IInteractable target = candidate.GetComponentInParent<IInteractable>();
                if (target == null || !target.Available || target.InteractionRoot.IsChildOf(transform)) continue;
                float distance = Vector2.Distance(transform.position, target.InteractionPosition);
                if (distance > distanceLimit || !CanReach(target, filter)) continue;
                nearest = target;
                distanceLimit = distance;
            }
            return nearest;
        }

        private bool CanReach(IInteractable target, ContactFilter2D filter)
        {
            Physics2D.Linecast(transform.position, target.InteractionPosition, filter, obstacles);
            foreach (RaycastHit2D obstacle in obstacles)
            {
                Transform hit = obstacle.collider.transform;
                if (!hit.IsChildOf(transform) && !hit.IsChildOf(target.InteractionRoot)) return false;
            }
            return true;
        }
    }
}
