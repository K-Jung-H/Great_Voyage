using UnityEngine;

namespace GreatVoyage
{
    [System.Serializable]
    public sealed class EventWeight
    {
        public string kind;
        public float weight = 1;
    }
    [CreateAssetMenu(menuName = "Great Voyage/Event Settings")]
    public sealed class EventSettings : ScriptableObject
    {
        [Tooltip("항해 중 폭풍 발생 간격의 최소 게임 초. 조정용 수치.")]
        public float minimumInterval = 180;
        [Tooltip("항해 중 폭풍 발생 간격의 최대 게임 초. 조정용 수치.")]
        public float maximumInterval = 360;
        [Tooltip("폭풍 지속 게임 초.")]
        public float stormDuration = 20;
        [Tooltip("갑판 층 ID. 층 번호를 규칙 코드에 고정하지 않는다.")]
        public string exposedFloor = "deck-1";
        [Tooltip("갑판 선원에게 초당 적용하는 체력 피해.")]
        public float healthDamagePerSecond = .5f;
        [Tooltip("펼친 돛 시설의 초당 내구도 피해.")]
        public float sailDamagePerSecond = 1;
        [Tooltip("한 게임 초마다 적용할 방향타 무작위 회전 범위(도).")]
        public float headingVariation = 12;
        [Tooltip("일반 사건 중 해적 습격 비중. 0~1 조정값.")]
        [Range(0, 1)] public float pirateProbability = .35f;
        [Tooltip("일반 사건 유형별 발생 비중. 불침번/반란 조건은 별도로 검사한다.")]
        public EventWeight[] weights = { new EventWeight { kind="storm", weight=3 }, new EventWeight { kind="pirates", weight=2 }, new EventWeight { kind="rats", weight=2 }, new EventWeight { kind="island" }, new EventWeight { kind="fish" }, new EventWeight { kind="kraken", weight=.2f }, new EventWeight { kind="crabs", weight=.5f }, new EventWeight { kind="barnacles", weight=.5f } };
        [Tooltip("취침 중 사건 발생 시 불침번으로 전환할 비율.")]
        [Range(0, 1)] public float watchProbability = .35f;
        [Tooltip("불침번 제한 게임 초.")] public float watchDuration = 60;
        [Tooltip("불침번 요구 순찰 거리(Cell).")]
        public float patrolDistance = 8;
        [Tooltip("무인도 선원 파견 게임 초.")] public float dispatchDuration = 25;
        [Tooltip("무인도 성공 보상 금화.")] public int islandReward = 40;
        [Tooltip("유해 생물 사건의 최대 게임 초.")] public float pestDuration = 45;
        [Tooltip("유해 생물의 시설 초당 피해.")] public float pestDamage = 1;
        [Tooltip("물고기떼의 신선한 생선 보상 수량.")] public int fishYield = 5;
        [Tooltip("망원경/망루 탐지 거리(해상 좌표).")]
        public float detectionRange = 50;
    }
}
