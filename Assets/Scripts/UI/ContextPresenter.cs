using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GreatVoyage
{
    public sealed class ContextPresenter : MonoBehaviour
    {
        [SerializeField, Tooltip("조타·취침/불침번 표시 루트.")]
        private GameObject root;
        [SerializeField, Tooltip("조타 표시.")]
        private GameObject helm;
        [SerializeField, Tooltip("취침 상태 표시.")]
        private GameObject rest;
        [SerializeField, Tooltip("방향·속도 표시.")]
        private TMP_Text heading;
        [SerializeField, Tooltip("취침/불침번 모드.")]
        private TMP_Text sleepStatus;
        [SerializeField, Tooltip("불침번 남은 거리.")]
        private TMP_Text watch;
        [SerializeField, Tooltip("기상/불침번 시간.")]
        private TMP_Text wake;
        [SerializeField, Tooltip("후속 단계의 망원경 표시.")]
        private GameObject telescope;
        [SerializeField, Tooltip("층 이동 삽화 Frame.")]
        private GameObject transition;
        [SerializeField, Tooltip("현재 임시 삽화의 층 식별 표시.")]
        private TMP_Text transitionLabel;
        [SerializeField, Tooltip("삽화 표시 시간. 게임 규칙의 대기 시간이 아닌 표현 조정값.")]
        private float transitionSeconds = .35f;
        private GameRuntime game;
        private WorldView world;
        private float remaining;
        public bool IsTransitioning => remaining > 0;

        public void Bind(GameRuntime runtime, WorldView world)
        {
            game = runtime;
            this.world = world;
            world.FloorChanged += ShowFloor;
            telescope.SetActive(false);
            transition.SetActive(false);
        }

        private void ShowFloor(string floor)
        {
            remaining = transitionSeconds;
            transitionLabel.text = floor + " · 층 이동 삽화";
            transition.SetActive(true);
        }

        public void Refresh()
        {
            remaining = Mathf.Max(0, remaining - Time.unscaledDeltaTime);
            transition.SetActive(remaining > 0);
            bool steering = game.Navigation.IsSteering, sleeping = game.Rest.State.mode != PlayerRest.Awake;
            root.SetActive(steering || sleeping);
            helm.SetActive(steering);
            rest.SetActive(sleeping);
            if (steering)
                heading.text = $"방향 {game.Navigation.Heading:0}° · 속도 {game.Navigation.State.speed:0.0}";
            if (sleeping)
            {
                sleepStatus.text = game.Rest.IsSleeping ? "취침 중" : "불침번";
                watch.text = $"순찰 {game.Rest.State.patrolRemaining:0.0} Cell";
                wake.text = game.Rest.IsSleeping ? $"기상 {game.Rest.State.wakeAt / 600:0.0}일" : $"남은 시간 {game.Rest.State.watchRemaining:0}초";
            }
        }

        private void OnDestroy()
        {
            if (world)
                world.FloorChanged -= ShowFloor;
        }
    }
}
