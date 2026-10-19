using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GreatVoyage
{
    public sealed class PortPanel : MonoBehaviour
    {
        [SerializeField] private EconomySession economy;
        [SerializeField] private GameSession session;
        private GameObject root;
        private Text heading;
        private Text details;
        private Text selection;
        private Button buy, sell, hire, pay, confirm, paymentButton, quantityDown, quantityUp, close;
        private IDisposable pause;
        private Font font;
        private PortService service;
        private int quantity = 1;
        private ContractPayment payment;
        private int candidateIndex;
        private Button nextCandidate;
        private string CandidateId => economy.Candidates[candidateIndex % economy.Candidates.Length].id;

        // Role: 개발용 서비스 화면을 구성하고 행동 요청을 경제 규칙에 연결한다.
        private void Awake()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 20);
            root = new GameObject("PortPanel", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(transform, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(900, 660);
            root.GetComponent<Image>().color = new Color(.06f, .09f, .13f, .98f);
            heading = Label("Heading", 24, 20, 850, 50, 26);
            details = Label("Details", 24, 80, 850, 325, 20);
            selection = Label("Selection", 24, 410, 850, 65, 20);
            quantityDown = ActionButton("−", 24, 485, () => quantity = Mathf.Max(1, quantity - 1));
            quantityUp = ActionButton("＋", 200, 485, () => quantity = Mathf.Min(service == PortService.Tavern ? economy.Settings.maxVoyages : 100000, quantity + 1));
            paymentButton = ActionButton("선불/후불", 400, 485, () => payment = payment == ContractPayment.Prepaid ? ContractPayment.Deferred : ContractPayment.Prepaid);
            buy = ActionButton("구매", 24, 550, () => economy.Buy(quantity));
            sell = ActionButton("선택 수량 판매", 24, 550, () => economy.Sell(quantity));
            hire = ActionButton("고용", 24, 550, () => economy.Hire(quantity, payment, CandidateId));
            nextCandidate = ActionButton("다음 후보", 600, 485, () => candidateIndex = (candidateIndex + 1) % economy.Candidates.Length);
            pay = ActionButton("후불 전액 지급", 200, 550, PayFirstDebt);
            confirm = ActionButton("정산 후 출항", 400, 550, () => { if (economy.State.gameOver) session.SaveAndQuit(); else economy.SettleAndDepart(); });
            close = ActionButton("닫기 / 출항 취소", 660, 550, Close);
            root.SetActive(false);
        }

        // Role: 서비스별 표시와 계약 비용 미리보기를 실제 상태로 갱신한다.
        private void Update()
        {
            if (root == null || !root.activeSelf || economy.State == null) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !economy.State.gameOver) { Close(); return; }
            EconomyState state = economy.State;
            bool tavern = service == PortService.Tavern;
            bool settlement = service == PortService.Settlement;
            bool enabled = economy.CanAct;
            heading.text = state.gameOver ? "게임 오버 · 계속하기 불가" : tavern ? "주점 · 고용과 계약" : settlement ? "미정산 출항 · 강제 매각" : service == PortService.Merchant ? "직물 상인 · 구매" : "거래 대행인 · 판매";
            details.text = $"금화 {state.gold}  |  명성 {state.reputation:0.0}  |  직물 {state.cargo}  |  미지급 {economy.Debt}\n";
            if (economy.Port != null)
                details.text += $"구매가 {economy.Port.buyPrice}  |  판매가 {economy.SalePrice}  |  상인 재고 {state.markets.Find(p => p.portId == economy.Port.portId).stock}\n";
            if (tavern)
            {
                CrewState candidate = economy.Candidate(CandidateId);
                details.text += candidate == null ? "선택한 후보는 현재 고용할 수 없습니다. 다음 후보로 전환하세요.\n" : $"후보: {candidate.displayName} · {(candidate.specialty == CrewSpecialty.Navigation ? "항해 특기 · 경력 " + candidate.navigationExperience : "정비 특기")}\n체력 {candidate.health:0} / 기력 {candidate.energy:0} / 행복 {candidate.happiness:0}\n";
            }
            foreach (EmploymentContract contract in state.contracts.Where(c => c.status == ContractStatus.Active || c.debt > 0 || c == state.contracts.LastOrDefault()).TakeLast(3))
                details.text += $"{contract.id} · {contract.status} · {(contract.payment == ContractPayment.Prepaid ? "선불" : "후불")} 총액 {contract.totalPrice} · 잔여 {contract.remainingVoyages}회 · 미지급 {contract.debt}\n";
            if (settlement) details.text += "매각은 시장가의 60%. 필요한 금액을 넘겨 판매할 수 있습니다.\n확정 시 보유금으로 지급하고 부족액에 비례해 명성이 감소합니다.\n";
            details.text += "\n" + (session.ErrorMessage ?? economy.Message);
            selection.text = tavern ? $"계약 {quantity}회 × {economy.Settings.wagePerVoyage} = {quantity * economy.Settings.wagePerVoyage} 금화\n{(payment == ContractPayment.Prepaid ? "선불: 계약 시 전액" : "후불: 계약 만료 후 전액")}" : $"선택 수량 {quantity} · 합계 {(long)quantity * (service == PortService.Merchant ? economy.Port.buyPrice : economy.SalePrice)} 금화";
            buy.gameObject.SetActive(service == PortService.Merchant);
            sell.gameObject.SetActive(service == PortService.Broker || settlement);
            hire.gameObject.SetActive(tavern); paymentButton.gameObject.SetActive(tavern);
            nextCandidate.gameObject.SetActive(tavern); nextCandidate.interactable = enabled;
            pay.gameObject.SetActive(tavern || settlement); confirm.gameObject.SetActive(settlement);
            foreach (Button button in new[] { buy, sell, hire, pay, confirm, paymentButton, quantityDown, quantityUp }) button.interactable = enabled;
            if (state.gameOver) { confirm.interactable = true; confirm.GetComponentInChildren<Text>().text = "저장 후 종료"; }
            close.interactable = !state.gameOver;
        }

        // Role: 정지 사유를 확보하고 요청한 서비스 화면을 표시한다.
        public void Open(PortService requested)
        {
            service = requested; quantity = 1;
            if (pause == null) pause = session.Pauses.Acquire("PortService");
            root.SetActive(true); root.transform.SetAsLastSibling();
        }

        // Role: 이 화면의 정지만 해제하고 미확정 출항을 취소한다.
        public void Close()
        {
            if (economy.State != null && economy.State.gameOver) return;
            economy.CancelSettlement();
            root.SetActive(false); pause?.Dispose(); pause = null;
        }

        // Role: 현재 대표 후보의 미지급 계약 전액 지급을 요청한다.
        private void PayFirstDebt()
        {
            EmploymentContract contract = economy.State.contracts.Find(value => value.debt > 0);
            if (contract != null) economy.Pay(contract.id);
        }

        // Role: 지정 위치에 개발용 텍스트를 배치한다.
        private Text Label(string name, float x, float y, float width, float height, int size)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.transform.SetParent(root.transform, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
            var text = obj.GetComponent<Text>(); text.font = font; text.fontSize = size; text.color = Color.white; text.raycastTarget = false;
            return text;
        }

        // Role: 지정 위치에 규칙 호출용 버튼을 구성한다.
        private Button ActionButton(string caption, float x, float y, UnityEngine.Events.UnityAction action)
        {
            var obj = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button)); obj.transform.SetParent(root.transform, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(165, 48);
            obj.GetComponent<Image>().color = new Color(.2f, .3f, .4f);
            Text text = Label(caption, 0, 0, 165, 48, 18); text.transform.SetParent(obj.transform, false); text.alignment = TextAnchor.MiddleCenter;
            Button button = obj.GetComponent<Button>(); button.onClick.AddListener(action); return button;
        }

        // Role: 시스템 종료 시 표시 자원과 정지 사유를 해제한다.
        private void OnDestroy() { pause?.Dispose(); if (font != null) Destroy(font); }
    }
}
