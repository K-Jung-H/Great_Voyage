using UnityEngine;

namespace GreatVoyage
{
    [CreateAssetMenu(menuName = "Great Voyage/Crew Settings")]
    public sealed class CrewSettings : ScriptableObject
    {
        public string id = "standard-crew";
        [Tooltip("대표 방향타 업무의 층 ID.")] public string helmFloor = "deck-1";
        [Tooltip("방향타 옆 접근 지점(Cell). 배치가 바뀌면 함께 조정한다.")] public Vector2 helmPosition = new Vector2(2.5f, 1.5f);
        [Tooltip("표시·충돌·상호작용용 Prefab. 시뮬레이션 상태는 별도로 관리한다.")] public CrewView prefab;
        [Tooltip("정식 새 게임은 빈 배열. 선원은 이후 고용 시스템에서 추가한다.")] public CrewState[] initialCrew = new CrewState[0];
        [Tooltip("초기 항해 식량 수량. 무역 상품과 구분한다.")] public int initialFood;
        [Tooltip("초기 수리도구 수량.")] public int initialRepairTools;
        [Tooltip("선원 상태 갱신 간격(실제 초). 진행 중 바꾸지 않는다.")] public float stepSeconds = .25f;
        [Tooltip("초당 이동 Cell 수.")] public float moveSpeed = 2;
        [Tooltip("Nav 경로 재탐색 간격(실제 초). 중간 목적지 전환은 이 타이머를 초기화하지 않는다.")] public float repathInterval = 3;
        [Tooltip("Context steering에서 평가할 방향 수.") , Range(8, 32)] public int steeringDirections = 16;
        [Tooltip("장애물과 선원을 미리 확인할 거리(Cell).") ] public float lookAhead = 1.1f;
        [Tooltip("목표 방향 선호도 대비 위험 회피 가중치.")] public float avoidanceWeight = 2;
        [Tooltip("이전 이동 방향을 유지하는 가중치.")] public float momentumWeight = .15f;
        [Tooltip("초당 속도 변화량(Cell/초²).") ] public float acceleration = 10;
        [Tooltip("Crew Prefab Collider 반지름과 같은 값(Cell).") ] public float bodyRadius = .28f;
        [Tooltip("장애물 회피 시 물리 반지름에 추가할 여유(Cell).") ] public float clearance = .05f;
        [Tooltip("중간 목적지 통과 판정 반경(Cell).") ] public float waypointRadius = .3f;
        [Tooltip("최종 접근점 도착 판정 반경(Cell).") ] public float arrivalRadius = .12f;
        [Tooltip("다른 선원의 위치 예측 시간(실제 초).") ] public float predictionSeconds = .3f;
        [Tooltip("평상시 선원의 물리 질량.")] public float normalMass = 1;
        [Tooltip("작업·취침·식사 중 질량. 평상시보다 커야 한다.")] public float workingMass = 4;
        [Tooltip("밀려난 뒤 작업 시작점으로 돌아가는 속도(Cell/초).")] public float workReturnSpeed = .6f;
        [Tooltip("작업 위치 복귀 모터의 최대 가속도(Cell/초²).")] public float workReturnAcceleration = 3;
        [Tooltip("작업 위치에서 이 거리 이상 벗어나면 진행을 멈춘다(Cell).")] public float workRadius = .18f;
        [Tooltip("할 일이 없을 때 배회하는 최대 거리(Cell).")] public float wanderDistance = 3;
        [Tooltip("실제 초당 허기 증가량.")] public float hungerRate = .5f;
        [Tooltip("식사를 시작하는 허기(0~100). ")] public float mealThreshold = 60;
        [Tooltip("식사 1회의 소요 실제 초.")] public float mealSeconds = 2;
        [Tooltip("식사 1회의 허기 감소량.")] public float mealRecovery = 70;
        [Tooltip("기상 중 실제 초당 기력 감소량.")] public float energyDrain = .3f;
        [Tooltip("취침이 필요한 기력.")] public float sleepThreshold = 25;
        [Tooltip("주간에 기상할 기력.")] public float wakeThreshold = 90;
        [Tooltip("취침 중 실제 초당 기력 회복량.")] public float sleepRecovery = 5;
        [Tooltip("자동 취침 시작 시각(게임 시간, 0~23). 기상 시각보다 커야 한다.")] public int sleepHour = 22;
        [Tooltip("자동 기상 시각(게임 시간, 0~23).")] public int wakeHour = 6;
        [Tooltip("수리도구 하나를 사용하는 작업 주기(실제 초).")] public float repairSeconds = 3;
        [Tooltip("수리도구 하나당 회복 내구도. 완전 파손 시설은 수리하지 않는다.")] public float repairAmount = 10;
        [Tooltip("허기 100에서 실제 초당 체력·행복 감소량. 체력 최솟값은 1.")] public float starvationRate = 1;
    }
}
