using UnityEngine;

namespace GreatVoyage
{
    [CreateAssetMenu(menuName = "Great Voyage/Battle Settings")]
    public sealed class BattleSettings : ScriptableObject
    {
        [Tooltip("전투를 관전할 실제 함선 층 ID.")]
        public string floorId = "deck-1";
        [Tooltip("초기 해적 수. 조정용 수치.")]
        public int pirateCount = 2;
        [Tooltip("해적 기본 체력.")]
        public float pirateHealth = 50;
        [Tooltip("해적 기본 힘.")]
        public float pirateStrength = 10;
        [Tooltip("맨손/칼/총 순서의 공격 대기 시간.")]
        public Vector3 cooldowns = new Vector3(1.2f, .8f, 2);
        [Tooltip("맨손/칼/총 순서의 공격 범위(Cell).")]
        public Vector3 ranges = new Vector3(.8f, 1.2f, 4);
        [Tooltip("총 투사체 속도(Cell/초).")]
        public float projectileSpeed = 6;
        [Tooltip("해적 이동 속도(Cell/초).")]
        public float movementSpeed = 1.6f;
        [Tooltip("대포 한 시설이 전투 시작 전 적에게 가하는 체력 피해.")]
        public float cannonDamage = 10;
        [Tooltip("이 체력 이하의 생존 적은 항복하여 처분 대상이 된다.")]
        public float surrenderHealth = 10;
        [Tooltip("시청에서 생존 포로 한 명을 인계할 때 지급할 조정용 보상.")]
        public int handoverReward = 25;
        [Tooltip("크라켄 기본 체력. 조정용 수치.")]
        public float krakenHealth = 200;
    }
}
