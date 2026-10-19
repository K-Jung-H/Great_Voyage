using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Linq;

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
        [SerializeField] private JournalSession journal;
        [SerializeField] private NavigationSession navigation;
        [SerializeField] private JournalMap map;
        [SerializeField] private Text infoLabel, messageLabel;
        [SerializeField] private RectTransform historyContent;
        [SerializeField] private InputField noteInput;
        [SerializeField] private Button recordButton, clearPlanButton, autoButton, selectButton, planButton, noteButton, saveNoteButton, removeEventButton;
        public int Tool { get; private set; }
        private int historyCount = -1;
        private Vector2 selectedPosition;
        private JournalVoyage selectedVoyage;
        private string selectedEvent;

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
            if (journal != null)
            {
                recordButton.onClick.AddListener(journal.Record); clearPlanButton.onClick.AddListener(journal.ClearPlan);
                autoButton.onClick.AddListener(journal.ToggleAutopilot);
                selectButton.onClick.AddListener(() => Tool = 0); planButton.onClick.AddListener(() => Tool = 1); noteButton.onClick.AddListener(() => Tool = 2);
                saveNoteButton.onClick.AddListener(() => { journal.AddNote(selectedPosition, noteInput.text); noteInput.text = ""; });
                removeEventButton.onClick.AddListener(() => { journal.RemoveEvent(selectedVoyage, selectedEvent); selectedEvent = null; });
            }
            Close();
            ShowPrompt(false);
        }

        // Role: UI가 살아 있는 동안 닫기 입력을 받는다.
        private void OnEnable() => closeAction.Enable();

        // Role: Escape 입력으로 열린 일지를 닫는다.
        private void Update()
        {
            if (IsOpen && closeAction.WasPressedThisFrame()) Close();
            if (!IsOpen || journal == null || journal.State == null) return;
            messageLabel.text = $"도구: {(Tool == 1 ? "계획선 펜" : Tool == 2 ? "메모 위치" : "선택")} · 휠 확대/축소 · 오른쪽 드래그 이동\n" + journal.Message;
            autoButton.GetComponentInChildren<Text>().text = journal.State.autopilot ? "자동 조타 요청 해제" : "자동 조타 요청";
            removeEventButton.interactable = selectedEvent != null;
            if (historyCount != journal.State.voyages.Count) RefreshHistory();
        }

        // Role: 책상 상호작용으로 최소 일지 화면을 표시한다.
        public void Open()
        {
            if (session != null && (!session.IsReady || session.IsSleeping)) return;
            if (session != null && pause == null) pause = session.Pauses.Acquire("Journal");
            if (panelRoot != null) panelRoot.SetActive(true);
            ShowPrompt(false);
            if (journal != null && journal.State != null) { RefreshHistory(); SelectMapPoint(navigation.WorldPosition, 1); }
        }

        // Role: 일지를 닫고 일지가 소유한 정지 사유만 해제한다.
        public void Close()
        {
            if (IsOpen && journal != null && session != null && session.IsReady) session.SaveCurrent();
            if (panelRoot != null) panelRoot.SetActive(false);
            pause?.Dispose();
            pause = null;
        }

        // Role: 선택한 지도 위치의 항구·기록 사건·메모 정보를 오른쪽에 표시한다.
        public void SelectMapPoint(Vector2 point, float radius)
        {
            selectedPosition = point; selectedEvent = null; selectedVoyage = null;
            string details = $"해역 ({point.x:0.0}, {point.y:0.0})\n흰 표식: 현재 배\n금색: 항구 / 실제 기록\n파란색: 계획선\n초록색: 메모 / 붉은색: 사건";
            foreach (PortDefinition port in navigation.Ports)
                if (Vector2.Distance(point, port.position) <= radius)
                { selectedPosition = port.position; details = port.displayName + $"\n세계 ({port.position.x:0}, {port.position.y:0})\n입항 반경 {port.arrivalRadius:0.0}\n이 항구의 거래·고용은 하선 후 이용합니다."; }
            foreach (JournalVoyage voyage in journal.State.voyages.Where(v => v == journal.Current || map.Layers.Contains(v.sequence)))
            {
                foreach (JournalEvent item in voyage.events)
                    if (Vector2.Distance(point, item.position) <= radius)
                    { selectedPosition = item.position; selectedEvent = item.id; selectedVoyage = voyage; details = $"{item.day}일 · {item.title}\n실제 발생 위치 ({item.position.x:0.0}, {item.position.y:0.0})"; }
                foreach (JournalNote note in voyage.notes)
                    if (Vector2.Distance(point, note.position) <= radius) details += "\n메모: " + note.text;
            }
            infoLabel.text = details;
        }

        // Role: 과거 항해 목록을 스크롤과 지도 레이어 토글로 구성한다.
        private void RefreshHistory()
        {
            foreach (Transform child in historyContent) Destroy(child.gameObject);
            int index = 0;
            foreach (JournalVoyage voyage in journal.State.voyages.AsEnumerable().Reverse())
            {
                JournalVoyage item = voyage;
                Button button = MakeButton(historyContent, "항해 " + item.sequence, 0, index * 70, 200, 62);
                button.GetComponentInChildren<Text>().text = $"항해 {item.sequence}\n{item.departurePort} → {item.arrivalPort ?? "진행 중"}";
                foreach (Text text in button.GetComponentsInChildren<Text>()) text.font = displayFont;
                button.onClick.AddListener(() =>
                {
                    if (!map.Layers.Add(item.sequence)) map.Layers.Remove(item.sequence);
                    button.GetComponent<Image>().color = map.Layers.Contains(item.sequence) ? new Color(.45f, .35f, .16f) : new Color(.14f, .2f, .23f);
                });
                button.GetComponent<Image>().color = map.Layers.Contains(item.sequence) ? new Color(.45f, .35f, .16f) : new Color(.14f, .2f, .23f);
                index++;
            }
            historyContent.sizeDelta = new Vector2(200, Mathf.Max(540, index * 70)); historyCount = journal.State.voyages.Count;
        }

        // Role: 승인된 개발 씬 편집에서 도안의 세 영역 일지 UI를 생성한다.
        public void BuildLayout()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("일지 UI 생성은 편집 중에만 가능합니다.");
            if (panelRoot != null) DestroyImmediate(panelRoot);
            panelRoot = new GameObject("JournalPanel", typeof(RectTransform), typeof(Image)); panelRoot.transform.SetParent(transform, false);
            RectTransform panel = panelRoot.GetComponent<RectTransform>(); panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f); panel.sizeDelta = new Vector2(1480, 800);
            panelRoot.GetComponent<Image>().color = new Color(.07f, .1f, .12f, .99f);
            MakeLabel(panel, "항해 일지", 20, 15, 400, 45, 30);
            MakeLabel(panel, "이전 항해 기록", 20, 65, 220, 30, 20);
            var scrollRoot = MakeRect(panel, "Records", 20, 100, 220, 530); scrollRoot.gameObject.AddComponent<RectMask2D>();
            historyContent = MakeRect(scrollRoot, "Content", 0, 0, 200, 540);
            ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>(); scroll.content = historyContent; scroll.viewport = scrollRoot; scroll.horizontal = false;
            selectButton = MakeButton(panel, "선택", 20, 650, 100, 44);
            planButton = MakeButton(panel, "계획선 펜", 130, 650, 110, 44);
            noteButton = MakeButton(panel, "메모 위치", 20, 705, 220, 44);
            var viewport = MakeRect(panel, "MapViewport", 260, 75, 860, 615); viewport.gameObject.AddComponent<Image>().color = new Color(.13f, .25f, .29f); viewport.gameObject.AddComponent<RectMask2D>();
            var mapRect = MakeRect(viewport, "Map", 0, 0, 860, 615); mapRect.pivot = new Vector2(.5f, .5f); mapRect.anchoredPosition = new Vector2(430, -307.5f);
            map = mapRect.gameObject.AddComponent<JournalMap>();
#if UNITY_EDITOR
            var serializedMap = new UnityEditor.SerializedObject(map);
            serializedMap.FindProperty("journal").objectReferenceValue = journal; serializedMap.FindProperty("navigation").objectReferenceValue = navigation; serializedMap.FindProperty("panel").objectReferenceValue = this;
            serializedMap.ApplyModifiedPropertiesWithoutUndo();
#endif
            MakeLabel(panel, "항구 · 사건 · 메모\n지도에서 표식을 선택하세요", 1140, 85, 320, 125, 26).alignment = TextAnchor.MiddleCenter;
            infoLabel = MakeLabel(panel, "현재 위치", 1140, 235, 320, 245, 21);
            var input = MakeRect(panel, "NoteInput", 1140, 500, 320, 105); input.gameObject.AddComponent<Image>().color = new Color(.18f, .23f, .25f);
            Text noteText = MakeLabel(input, "", 10, 8, 300, 85, 20); noteInput = input.gameObject.AddComponent<InputField>(); noteInput.textComponent = noteText; noteInput.lineType = InputField.LineType.MultiLineNewline; noteInput.characterLimit = 500;
            saveNoteButton = MakeButton(panel, "선택 위치에 메모 저장", 1140, 620, 320, 44);
            removeEventButton = MakeButton(panel, "선택 사건 아이콘 제거", 1140, 675, 320, 44);
            closeButton = MakeButton(panel, "나가기", 1320, 15, 140, 44);
            recordButton = MakeButton(panel, "현재 경로 기록", 260, 710, 220, 44);
            clearPlanButton = MakeButton(panel, "계획선 지우기", 490, 710, 220, 44);
            autoButton = MakeButton(panel, "자동 조타 요청", 720, 710, 250, 44);
            messageLabel = MakeLabel(panel, "", 260, 755, 1200, 42, 16);
            panelRoot.SetActive(false);
        }

        // Role: UI 자식을 좌상단 기준의 사각형으로 배치한다.
        private static RectTransform MakeRect(Transform parent, string name, float x, float y, float width, float height)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
        // Role: 일지 표시 텍스트를 구성한다.
        private static Text MakeLabel(Transform parent, string text, float x, float y, float width, float height, int size)
        {
            Text label = MakeRect(parent, "Label", x, y, width, height).gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.text = text; label.fontSize = size; label.color = new Color(.92f, .87f, .73f); label.raycastTarget = false; return label;
        }
        // Role: 일지 행동 버튼의 시각 요소를 구성한다.
        private static Button MakeButton(Transform parent, string text, float x, float y, float width, float height)
        {
            var rect = MakeRect(parent, text, x, y, width, height); rect.gameObject.AddComponent<Image>().color = new Color(.25f, .29f, .28f);
            Button button = rect.gameObject.AddComponent<Button>(); MakeLabel(rect, text, 0, 0, width, height, 18).alignment = TextAnchor.MiddleCenter; return button;
        }

        // Role: 책상 사용 가능 여부에 따라 상호작용 안내를 표시한다.
        public void ShowPrompt(bool visible, string message = "[E] 항해 일지 열기")
        {
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(visible && !IsOpen);
                if (visible)
                {
                    Text label = interactionPrompt.GetComponentInChildren<Text>();
                    if (label != null) label.text = message;
                }
            }
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
