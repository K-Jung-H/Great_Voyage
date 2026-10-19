using UnityEngine;

namespace GreatVoyage
{
    public enum NavigationAction { Helm, LeaveShip, BoardShip }

    public sealed class NavigationPoint : MonoBehaviour
    {
        [SerializeField, Tooltip("실행할 항해 상호작용.")] private NavigationAction action;
        [SerializeField, Tooltip("항해 상태와 동작을 관리하는 컴포넌트.")] private NavigationSession navigation;
        [SerializeField, Tooltip("Cell 규격을 따르는 자식 접근 지점.")] private Transform interactionPoint;
        public Vector2 InteractionPosition => interactionPoint != null ? (Vector2)interactionPoint.position : (Vector2)transform.position;
        public string Prompt => action == NavigationAction.Helm ? "[E] 방향타 사용" : action == NavigationAction.LeaveShip ? "[E] 하선" : "[E] 승선";
        public bool Available => navigation != null && navigation.IsReady && (action == NavigationAction.Helm ? navigation.IsAboard : navigation.IsDocked);

        // Role: 현재 항해 상태에서 허용된 조타 또는 승하선을 요청한다.
        public void Interact()
        {
            if (!Available) return;
            if (action == NavigationAction.Helm) navigation.BeginSteering();
            else navigation.SetAboard(action == NavigationAction.BoardShip);
        }
    }
}
