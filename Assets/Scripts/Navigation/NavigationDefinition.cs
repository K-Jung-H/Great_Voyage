using System;
using UnityEngine;

namespace GreatVoyage
{
    [Serializable]
    public sealed class PortDefinition
    {
        public string id;
        public string displayName;
        public Vector2 position;
        [Tooltip("자동 입항 반경. 세계 좌표 단위.")] public float arrivalRadius = 3;
        [Tooltip("출항 후 재입항을 허용할 이탈 반경. 입항 반경보다 커야 한다.")] public float departureRadius = 5;
    }

    [CreateAssetMenu(menuName = "Great Voyage/Navigation")]
    public sealed class NavigationDefinition : ScriptableObject
    {
        public string id = "prototype-sea";
        public PortDefinition[] ports;
        [Tooltip("개발 해역의 세계 좌표 경계.")] public Rect bounds = new Rect(-100, -100, 200, 200);
        [Tooltip("초당 최대 항해 거리. Cell 크기와 독립적인 세계 좌표 단위.")] public float maxSpeed = 4;
        [Tooltip("초당 속도 조절량.")] public float acceleration = 4;
        [Tooltip("초당 회전 각도. 0도는 동쪽, 90도는 북쪽.")] public float turnSpeed = 60;
        [Range(0, 1), Tooltip("방향타 기능 정지 시 최근접 항구 견인 비용의 보유금 비율.")]
        public float towGoldFraction = .25f;
    }
}
