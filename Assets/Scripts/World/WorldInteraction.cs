using System;
using UnityEngine;

namespace GreatVoyage
{
    public enum WorldAction
    {
        Stair,
        Helm,
        Gangway,
        BoardShip,
        Portal,
        Merchant,
        Broker,
        Tavern,
        Contracts,
        Bed,
        DeferredService
    }

    public sealed class WorldInteraction : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("서비스/공간 이동 책임.")]
        private WorldAction action;
        [SerializeField, Tooltip("지역·층·시설의 안정 ID.")]
        private string targetId;
        [SerializeField, Tooltip("지역/층 이동의 도착 접근점.")]
        private Vector2 destination;
        [SerializeField, Tooltip("플레이어가 실제 접근하는 지점.")]
        private Transform accessPoint;
        [SerializeField, Tooltip("확정 기획의 행동 안내.")]
        private string prompt;
        public WorldAction Action => action;
        public string TargetId => targetId;
        public Vector2 Destination => destination;
        public Transform InteractionRoot => transform;
        public Vector2 InteractionPosition => accessPoint ? accessPoint.position : transform.position;
        public string Prompt => prompt;

        private Action<WorldInteraction> request;
        private Func<WorldInteraction, bool> available;
        public bool Available => isActiveAndEnabled && request != null && (available?.Invoke(this) ?? false);

        public void Bind(Action<WorldInteraction> request, Func<WorldInteraction, bool> available)
        {
            this.request = request;
            this.available = available;
        }

        public void Interact()
        {
            if (Available)
                request(this);
        }
    }
}
