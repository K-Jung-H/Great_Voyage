using UnityEngine;

namespace GreatVoyage
{
    public sealed class JournalDesk : MonoBehaviour
    {
        [SerializeField, Tooltip("플레이어가 접근하는 책상 앞의 상호작용 지점.")]
        private Transform interactionPoint;

        public Vector2 InteractionPosition => interactionPoint != null
            ? (Vector2)interactionPoint.position : (Vector2)transform.position;
    }
}
