using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GreatVoyage
{
    public sealed class UIRoot : MonoBehaviour
    {
        [SerializeField, Tooltip("Editor에서 제작한 일지 화면 인스턴스. 이 Controller와 별도 객체.")]
        private GameObject journalScreen;
        [SerializeField, Tooltip("일지 우측 상단 닫기 버튼.")]
        private Button journalClose;
        [SerializeField, Tooltip("공통 HUD의 상호작용 안내.")]
        private Text prompt;
        [SerializeField, Tooltip("공통 UI 입력 포커스 소유자.")]
        private EventSystem eventSystem;

        public event Action CloseRequested;
        public bool BlocksWorldInput => journalScreen != null && journalScreen.activeSelf;
        public bool IsConfigured => journalScreen != null && journalScreen != gameObject &&
            journalClose != null && journalClose.transform.IsChildOf(journalScreen.transform) &&
            prompt != null && eventSystem != null;
        private bool initialized;
        private Font displayFont;

        public void Initialize()
        {
            if (!IsConfigured) throw new InvalidOperationException("UIRoot 필수 직렬화 참조가 누락되었습니다.");
            if (!initialized)
            {
                journalClose.onClick.AddListener(RequestClose);
                displayFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 24);
                if (displayFont != null)
                    foreach (Text label in GetComponentsInChildren<Text>(true)) label.font = displayFont;
            }
            initialized = true;
            SetJournalVisible(false);
            ShowPrompt(null);
        }

        public void SetJournalVisible(bool visible)
        {
            if (journalScreen == null) return;
            journalScreen.SetActive(visible);
            if (visible) ShowPrompt(null);
            if (eventSystem != null) eventSystem.SetSelectedGameObject(visible && journalClose != null ? journalClose.gameObject : null);
        }

        public void ShowPrompt(string message)
        {
            if (prompt == null) return;
            prompt.text = message ?? string.Empty;
            prompt.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private void RequestClose() => CloseRequested?.Invoke();

        private void OnDestroy()
        {
            if (initialized && journalClose != null) journalClose.onClick.RemoveListener(RequestClose);
            if (displayFont != null) Destroy(displayFont);
        }
    }
}
