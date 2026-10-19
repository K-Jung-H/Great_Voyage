using UnityEngine;

namespace GreatVoyage
{
    public sealed class Stairway : MonoBehaviour
    {
        [SerializeField, Tooltip("이 계단이 연결된 함선.")] private ShipLayout ship;
        [SerializeField, Tooltip("이동할 층 ID.")] private string targetFloor;
        [SerializeField, Tooltip("계단 앞의 접근 지점.")] private Transform interactionPoint;
        [SerializeField, Tooltip("접근 시 표시할 안내.")] private string prompt;
        public Vector2 InteractionPosition => interactionPoint.position;
        public string Prompt => prompt;

        // Role: 함선에 목표 층 이동을 요청한다.
        public void Travel() => ship.Travel(targetFloor);
    }
}
