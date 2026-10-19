using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GreatVoyage
{
    public sealed class JournalPanel : MonoBehaviour
    {
        [SerializeField, Tooltip("열고 닫을 일지 패널. 이 컴포넌트와는 다른 오브젝트여야 한다.")]
        private GameObject panelRoot;
        [SerializeField, Tooltip("책상 근처에서 표시할 E 키 안내.")]
        private GameObject interactionPrompt;
        [SerializeField, Tooltip("일지 닫기 버튼.")]
        private Button closeButton;
        [SerializeField, Tooltip("일지의 정지 사유를 등록할 세션.")]
        private GameSession session;

        private InputAction closeAction;
        private System.IDisposable pause;
        private Font displayFont;
        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        // Role: 최소 일지 화면과 닫기 입력을 준비한다.
        private void Awake()
        {
            closeAction = new InputAction("CloseJournal", InputActionType.Button, "<Keyboard>/escape");
            displayFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 24);
            if (displayFont != null)
                foreach (Text label in GetComponentsInChildren<Text>(true)) label.font = displayFont;
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            Close();
            ShowPrompt(false);
        }

        // Role: UI가 살아 있는 동안 닫기 입력을 받는다.
        private void OnEnable() => closeAction.Enable();

        // Role: Escape 입력으로 열린 일지를 닫는다.
        private void Update()
        {
            if (IsOpen && closeAction.WasPressedThisFrame()) Close();
        }

        // Role: 책상 상호작용으로 최소 일지 화면을 표시한다.
        public void Open()
        {
            if (session != null && pause == null) pause = session.Pauses.Acquire("Journal");
            if (panelRoot != null) panelRoot.SetActive(true);
            ShowPrompt(false);
        }

        // Role: 일지를 닫고 일지가 소유한 정지 사유만 해제한다.
        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            pause?.Dispose();
            pause = null;
        }

        // Role: 책상 사용 가능 여부에 따라 상호작용 안내를 표시한다.
        public void ShowPrompt(bool visible)
        {
            if (interactionPrompt != null) interactionPrompt.SetActive(visible && !IsOpen);
        }

        // Role: 비활성화된 UI의 입력과 표시를 정리한다.
        private void OnDisable()
        {
            closeAction.Disable();
            Close();
            ShowPrompt(false);
        }

        // Role: 버튼 구독과 생성한 입력·폰트 자원을 해제한다.
        private void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            closeAction?.Dispose();
            if (displayFont != null) Destroy(displayFont);
        }
    }
}
