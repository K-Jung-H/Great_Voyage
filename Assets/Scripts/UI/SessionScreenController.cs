using System;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class SessionScreenController : MonoBehaviour
    {
        [SerializeField, Tooltip("저장 오류·계속·종료·게임 오버의 공통 Prefab.")]
        private ScreenView view;
        [SerializeField, Tooltip("공통 화면 스택.")]
        private ScreenStack stack;
        private GameRuntime game;
        public event Action QuitRequested;
        public void Bind(GameRuntime runtime)
        {
            game = runtime;
            view.Action("ContinueAndExit/Continue", () => stack.CloseTop());
            view.Action("ContinueAndExit/SaveAndExit", () => QuitRequested?.Invoke());
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
            Select("ContinueAndExit");
            view.Connected("정상 종료 시 마지막 상태 저장");
            stack.Open("SessionModals");
        }

        public void ShowError(string error)
        {
            Select("SaveError");
            view.Text("SaveError/Message", error);
            view.Connected("저장 실패 · 현재 상태와 원본 파일 보존");
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
