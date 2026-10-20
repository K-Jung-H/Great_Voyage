using UnityEngine;

namespace GreatVoyage
{
    public sealed class JournalDesk : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("플레이어가 접근하는 책상 앞의 상호작용 지점.")]
        private Transform interactionPoint;

        public Vector2 InteractionPosition => interactionPoint != null
            ? (Vector2)interactionPoint.position : (Vector2)transform.position;

        public Transform InteractionRoot => transform;
        public string Prompt => "[E] 항해 일지 열기";
        public bool Available => isActiveAndEnabled && OpenRequested != null;
        public event System.Action OpenRequested;
        public void Interact() => OpenRequested?.Invoke();
    }
}
