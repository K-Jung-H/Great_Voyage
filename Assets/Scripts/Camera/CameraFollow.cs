using UnityEngine;

namespace GreatVoyage
{
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField, Tooltip("카메라가 따라갈 플레이어.")]
        private Transform target;
        [SerializeField, Tooltip("플레이어 위치를 따라가는 반응 속도.")]
        private float followSpeed = 8f;

        // Role: 물리 보간된 플레이어 위치를 부드럽게 따라간다.
        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 destination = new Vector3(target.position.x, target.position.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, destination,
                1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        }
    }
}
