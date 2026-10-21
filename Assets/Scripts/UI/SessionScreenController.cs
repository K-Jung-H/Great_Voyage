using System;
using UnityEngine;
using UnityEngine.UI;

namespace GreatVoyage
{
    public sealed class SessionScreenController : MonoBehaviour
    {
        [SerializeField, Tooltip("저장 오류·계속·종료·게임 오버의 공통 Prefab.")]
        private ScreenView view;
        [SerializeField, Tooltip("공통 화면 스택.")]
        private ScreenStack stack;
        private GameRuntime game;
        private string towPort;
        private int towCost;
        public event Action QuitRequested;
        public void Bind(GameRuntime runtime)
        {
            game = runtime;
            view.Action("ContinueAndExit/Continue", () => stack.CloseTop());
            view.Action("ContinueAndExit/SaveAndExit", () => QuitRequested?.Invoke());
            view.Action("ContinueAndExit/RequestTow", ConfirmTow);
            view.Action("SaveError/RetrySave", () =>
            {
                if (game.FlushSave(true))
                {
                    if (game.Economy.State.gameOver)
                    {
                        ShowGameOver();
                        return;
                    }

                    stack.UnlockAndClose("SessionModals");
                }
            });
            game.Error += ShowError;
        }

        private void Select(string mode)
        {
            foreach (var name in new[]
            {
                "ContinueAndExit",
                "SaveError",
                "GameOver"
            }

            )
                view.Get<RectTransform>(name).gameObject.SetActive(name == mode);
        }

        public void OpenMenu()
        {
            towPort = null;
            Select("ContinueAndExit");
            view.Connected("정상 종료 시 마지막 상태 저장");
            view.Text("ContinueAndExit/Message", "계속하거나 저장 후 종료할 수 있습니다. 구난은 방향타 기능 정지·해상·사건 종료 상태에서 이용할 수 있습니다.");
            var button = view.Get<Button>("ContinueAndExit/RequestTow");
            button.gameObject.SetActive(game.Navigation.CanRequestTow);
            button.interactable = true;
            view.Text("ContinueAndExit/RequestTow/Label", "최근접 항구 구난 요청");
            stack.Open("SessionModals");
        }

        private void ConfirmTow()
        {
            var destination = game.Navigation.NearestTowPort;
            if (destination == null)
            {
                view.Text("ContinueAndExit/Message", "현재 구난을 요청할 수 없습니다. 진행 중인 사건을 먼저 마치세요.");
                return;
            }
            if (towPort != destination.id || towCost != game.Navigation.TowCost)
            {
                towPort = destination.id;
                towCost = game.Navigation.TowCost;
                bool completes = towPort != game.Navigation.DeparturePort || game.Navigation.State.departureCleared;
                view.Text("ContinueAndExit/Message", destination.displayName + " 견인 · " + towCost + " 금화\n" + (completes ? "정상 입항·계약 정산" : "출항지 미이탈 복귀·계약 횟수 유지") + "\n시설 수리 없음 · 확정 버튼을 다시 누르세요.");
                view.Text("ContinueAndExit/RequestTow/Label", "견인 확정");
                return;
            }
            if (game.Navigation.RequestTow(towPort, towCost))
            {
                stack.UnlockAndClose("SessionModals");
                if (game.Economy.State.gameOver)
                    ShowGameOver();
            }
        }

        public void ShowError(string error)
        {
            Select("SaveError");
            string operation = game?.ErrorOperation ?? "초기화";
            view.Text("SaveError/Heading", operation + " 오류");
            view.Text("SaveError/Message", error);
            view.Get<Button>("SaveError/RetrySave").interactable = game != null && operation == "저장";
            view.Connected(operation + " 실패 · 상세 호출 경로는 Console에서 확인");
            if (game != null)
                stack.Open("SessionModals", true);
            else
                view.gameObject.SetActive(true);
        }

        public void ShowGameOver()
        {
            Select("GameOver");
            view.Connected("게임 오버 · 이어하기 불가");
            stack.Open("SessionModals", true);
        }

        private void OnDestroy()
        {
            if (game != null)
                game.Error -= ShowError;
        }
    }
}
