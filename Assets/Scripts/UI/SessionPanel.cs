using System;
using UnityEngine;
using UnityEngine.UI;

namespace GreatVoyage
{
    public sealed class SessionPanel : MonoBehaviour
    {
        [SerializeField, Tooltip("시간과 저장을 관리하는 세션.")]
        private GameSession session;
        [SerializeField, Tooltip("우측 상단 날짜·시간 표시.")]
        private Text clockLabel;
        [SerializeField, Tooltip("정지 메뉴 루트.")]
        private GameObject menuRoot;
        [SerializeField, Tooltip("정지 메뉴 열기 버튼.")]
        private Button openButton;
        [SerializeField, Tooltip("정지 메뉴 닫기 버튼.")]
        private Button resumeButton;
        [SerializeField, Tooltip("저장 후 종료 버튼.")]
        private Button quitButton;
        [SerializeField, Tooltip("저장 오류 메시지.")]
        private Text errorLabel;
        [SerializeField, Tooltip("개발 씬 전용 선원·침대·자원 재준비 버튼.")]
        private Button crewResetButton;
        private IDisposable pause;

        // Role: 정지 메뉴의 버튼을 연결한다.
        private void Awake()
        {
            openButton.onClick.AddListener(Open);
            resumeButton.onClick.AddListener(Close);
            quitButton.onClick.AddListener(session.SaveAndQuit);
            if (crewResetButton != null) crewResetButton.onClick.AddListener(session.RestoreCrewPrototype);
            menuRoot.SetActive(false);
        }

        // Role: 시간·정지 상태와 저장 오류를 표시한다.
        private void Update()
        {
            double days = session.ElapsedSeconds / GameSession.SecondsPerDay;
            int day = (int)Math.Floor(days) + 1;
            int minute = (int)((days - Math.Floor(days)) * 1440d);
            clockLabel.text = $"{day}일  {minute / 60:00}:{minute % 60:00}" + (session.IsPaused ? "  · 정지" : "");
            errorLabel.text = session.ErrorMessage ?? "";
            errorLabel.gameObject.SetActive(!string.IsNullOrEmpty(session.ErrorMessage));
        }

        // Role: 일지와 독립적인 메뉴 정지 사유를 등록한다.
        public void Open()
        {
            if (pause == null) pause = session.Pauses.Acquire("SessionMenu");
            menuRoot.SetActive(true);
        }

        // Role: 메뉴 사유만 해제해 일지 등 다른 정지는 유지한다.
        public void Close()
        {
            menuRoot.SetActive(false);
            pause?.Dispose();
            pause = null;
        }

        // Role: UI 비활성화 시 메뉴가 소유한 정지만 해제한다.
        private void OnDisable() => Close();

        // Role: 생성한 버튼 구독을 해제한다.
        private void OnDestroy()
        {
            openButton.onClick.RemoveListener(Open);
            resumeButton.onClick.RemoveListener(Close);
            if (session != null) quitButton.onClick.RemoveListener(session.SaveAndQuit);
            if (session != null && crewResetButton != null) crewResetButton.onClick.RemoveListener(session.RestoreCrewPrototype);
        }
    }
}
