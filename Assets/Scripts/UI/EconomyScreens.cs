using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GreatVoyage
{
    public sealed class EconomyScreens : MonoBehaviour
    {
        [SerializeField, Tooltip("U0에서 구성한 공통 화면 스택.")]
        private ScreenStack stack;
        private GameRuntime game;
        private string candidateId, contractId;
        private int quantity = 1, voyages = 1;
        private ContractPayment payment;
        private string merchantId;
        private string selectedRequest;
        private readonly Dictionary<string, int> quantities = new Dictionary<string, int>();
        public event Action SettlementConfirmed;
        public void Bind(GameRuntime runtime)
        {
            game = runtime;
            View("MerchantScreen").Action("Right/Buy", () => Command(() => game.Market.TradeBatch(quantities, true)));
            View("MerchantScreen").Action("Exit", Close);
            View("BrokerScreen").Action("LeftTwoThirds/Sell", () => Command(() => game.Market.TradeBatch(quantities, false)));
            View("BrokerScreen").Action("Exit", Close);
            View("BrokerScreen").Action("ResidentRequests/Accept", () => { game.Market.Fulfill(selectedRequest); RefreshTrade("BrokerScreen"); });
            var hiring = View("HiringScreen");
            hiring.Action("Actions/Cancel", Close);
            hiring.Action("Actions/Hire", () => Command(() => game.Economy.Hire(voyages, payment, candidateId)));
            hiring.Action("Actions/Negotiate", () => { game.Economy.Negotiate(); RefreshHiring(); });
            var evaluation = hiring.Get<Slider>("Right/Evaluation");
            evaluation.interactable = true;
            evaluation.minValue = -50; evaluation.maxValue = 50;
            evaluation.onValueChanged.AddListener(value => { game.Economy.Evaluate(value); RefreshHiring(); });
            hiring.Action("Right/Prepaid", () =>
            {
                payment = ContractPayment.Prepaid;
                RefreshHiring();
            });
            hiring.Action("Right/Deferred", () =>
            {
                payment = ContractPayment.Deferred;
                RefreshHiring();
            });
            var count = hiring.Get<TMP_InputField>("Right/Count");
            count.interactable = true;
            count.contentType = TMP_InputField.ContentType.IntegerNumber;
            count.onValueChanged.AddListener(value =>
            {
                if (int.TryParse(value, out int number))
                    voyages = Mathf.Clamp(number, 1, game.Economy.Settings.maxVoyages);
                RefreshHiring();
            });
            View("ContractsScreen").Action("Exit", Close);
            View("ContractsScreen").Action("Pay", () => Command(() => game.Economy.Pay(contractId)));
            View("ContractsScreen").Action("Renew", () =>
            {
                var contract = game.Economy.State.contracts.Find(item => item.id == contractId);
                if (contract != null && game.Economy.Candidate(contract.crewId) != null)
                    OpenHiring(contract.crewId);
            });
            View("SettlementModal").Action("CancelDeparture", () =>
            {
                game.Economy.CancelSettlement();
                quantity = 0;
                Close();
            });
            View("SettlementModal").Action("ConfirmSettlement", () =>
            {
                if (!game.Market.TradeBatch(quantities, false, true))
                    return;
                game.Economy.SettleAndDepart();
                stack.CloseTop();
                SettlementConfirmed?.Invoke();
            });
            View("TavernServiceScreen").Action("Exit", Close);
            game.Economy.SettlementRequested += OpenSettlement;
            stack.Closed += OnClosed;
        }

        private ScreenView View(string name) => stack.View(name);
        private void Close()
        {
            stack.CloseTop();
            quantity = 1;
        }

        private void Command(Action action)
        {
            action();
            Refresh();
            game.RequestSave();
        }

        public void Open(string name)
        {
            quantity = 1;
            quantities.Clear();
            stack.Open(name);
            Refresh();
        }

        public void OpenMerchant(string id) { merchantId = id; Open("MerchantScreen"); }

        public void OpenHiring(string id)
        {
            candidateId = id;
            game.Economy.BeginQuote(id);
            View("HiringScreen").Get<Slider>("Right/Evaluation").SetValueWithoutNotify(0);
            voyages = 1;
            payment = ContractPayment.Prepaid;
            stack.Open("HiringScreen");
            RefreshHiring();
        }

        public void OpenSettlement()
        {
            quantity = 0;
            quantities.Clear();
            stack.Open("SettlementModal");
            RefreshSettlement();
        }

        public void Refresh()
        {
            string top = stack.Top?.name;
            if (top == "MerchantScreen" || top == "BrokerScreen")
                RefreshTrade(top);
            else if (top == "HiringScreen")
                RefreshHiring();
            else if (top == "ContractsScreen")
                RefreshContracts();
            else if (top == "SettlementModal")
                RefreshSettlement();
            else if (top == "TavernServiceScreen")
                RefreshCandidates();
        }

        private void RefreshTrade(string name)
        {
            var view = View(name);
            view.Connected(game.Market.Message ?? (game.Market.Current?.condition + " · 현지 평판 " + game.Market.Current?.localStanding));
            string list = name == "MerchantScreen" ? "Right/ProductScroll" : "LeftTwoThirds/OwnedGoods";
            bool buy = name == "MerchantScreen";
            var products = buy ? game.Market.ProductsForMerchant(merchantId) : game.Market.Items.Where(item => item.tradeable && game.State.inventory.Get(item.id) > 0).ToArray();
            var rows = ReplaceRows(view.Get<ScrollRect>(list), products.Length);
            for (int i = 0; i < products.Length; i++)
            {
                var item = products[i];
                rows[i].Text("Item", item.displayName);
                rows[i].Text("Stock", game.State.inventory.Get(item.id, buy ? "port:" + game.Navigation.DockedPort + ":merchant" : "player").ToString());
                rows[i].Text("UnitPrice", (buy ? game.Market.Quote(item.id).buyPrice : game.Market.SalePrice(item.id)).ToString());
                BindProductQuantity(rows[i], item.id, () => RefreshTradeTotal(view, buy));
            }
            RefreshTradeTotal(view, buy);
            if (name == "MerchantScreen")
            {
                view.Text("Right/Wallet", game.State.inventory.Gold.ToString());
                view.Text("Right/SelectedTotal", TradeTotal(true).ToString());
            }
            else RefreshRequests(view);
        }

        private long TradeTotal(bool buy, bool liquidation = false) => quantities.Sum(pair => (long)pair.Value * (buy ? game.Market.Quote(pair.Key)?.buyPrice ?? 0 : game.Market.SalePrice(pair.Key, liquidation)));
        private void RefreshTradeTotal(ScreenView view, bool buy) => view.Text(buy ? "Right/SelectedTotal" : "LeftTwoThirds/SaleTotal", TradeTotal(buy).ToString());
        private void BindProductQuantity(ListItemView row, string product, Action changed)
        {
            var input = row.Get<TMP_InputField>("Quantity");
            input.interactable = true;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.SetTextWithoutNotify(quantities.TryGetValue(product, out int value) ? value.ToString() : "0");
            input.onValueChanged.AddListener(text => { quantities[product] = int.TryParse(text, out int number) ? Mathf.Clamp(number, 0, 1000000) : 0; changed(); });
        }
        private void RefreshRequests(ScreenView view)
        {
            var requests = game.Market.Current.requests.Where(value => !value.completed && value.expiresAt > game.ElapsedSeconds).ToArray();
            view.Get<RectTransform>("ResidentRequests/Deferred").gameObject.SetActive(false);
            if (!requests.Any(value => value.id == selectedRequest)) selectedRequest = requests.FirstOrDefault()?.id;
            var rows = ReplaceRows(view.Get<ScrollRect>("ResidentRequests/Requests"), requests.Length);
            for (int i = 0; i < requests.Length; i++)
            {
                var request = requests[i];
                rows[i].Text("Request", (request.id == selectedRequest ? "▶ " : "") + request.needed + " " + game.Market.ProductName(request.neededItem));
                rows[i].Text("Reward", request.reward + " " + game.Market.ProductName(request.rewardItem));
                rows[i].Text("TimeRemaining", Mathf.CeilToInt((float)(request.expiresAt - game.ElapsedSeconds)) + "초");
                rows[i].Text("Limit", "1회");
                rows[i].OnSelect(() => { selectedRequest = request.id; RefreshRequests(view); });
            }
            view.Get<Button>("ResidentRequests/Accept").interactable = selectedRequest != null;
        }

        private void BindQuantity(ListItemView row, Action changed)
        {
            var input = row.Get<TMP_InputField>("Quantity");
            input.interactable = true;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.SetTextWithoutNotify(quantity.ToString());
            input.onValueChanged.AddListener(text =>
            {
                quantity = int.TryParse(text, out int result) ? Mathf.Clamp(result, 0, 1000000) : 0;
                changed();
            });
        }

        private void RefreshCandidates()
        {
            var candidates = game.Economy.Candidates.Where(c => game.Economy.Candidate(c.id) != null).ToArray();
            var rows = ReplaceRows(View("TavernServiceScreen").Get<ScrollRect>("CrewIntroduction/Candidates"), candidates.Length);
            for (int i = 0; i < rows.Count; i++)
            {
                var candidate = candidates[i];
                rows[i].Text("Name", candidate.displayName);
                rows[i].Text("Specialty", candidate.specialty.ToString());
                rows[i].Text("Health", candidate.health.ToString("0"));
                rows[i].OnSelect(() => OpenHiring(candidate.id));
            }

            View("TavernServiceScreen").Connected("후보 선택 → 고용 · 유료 소개/정보는 후속 단계");
        }

        private void RefreshHiring()
        {
            var view = View("HiringScreen");
            var candidate = game.Economy.Candidate(candidateId);
            view.Connected(game.Economy.Message ?? "평가 금액 조정 ±50% · 능력/행복/신뢰 변화는 고용 확정 시 반영");
            view.Text("Right/Name", candidate?.displayName ?? "고용 불가");
            view.Text("Right/Health", candidate?.health.ToString("0") ?? "—");
            view.Text("Right/Experience", candidate?.navigationExperience.ToString() ?? "—");
            view.Text("Right/Specialty", candidate?.specialty.ToString() ?? "—");
            view.Text("Right/FutureStats", candidate == null ? "" : $"힘 {candidate.strength:0} · 손재주 {candidate.dexterity:0} · 지능 {candidate.intelligence:0}\n행복 {candidate.happiness:0}→{game.Economy.PreviewHappiness(candidate):0} · 신뢰 {candidate.trust:0}→{game.Economy.PreviewTrust(candidate):0}\n희망 {candidate.wish?.kind ?? CrewWishKind.None} · {candidate.wish?.targetId}");
            view.Text("Right/PerVoyage", game.Economy.QuoteWage(candidate).ToString());
            view.Text("Right/Total", ((long)game.Economy.QuoteWage(candidate) * voyages).ToString("N0"));
            view.Get<Button>("Actions/Negotiate").interactable = candidate != null && !game.Economy.Negotiated;
            view.Get<Button>("Right/Prepaid").GetComponent<Image>().color = payment == ContractPayment.Prepaid ? new Color(.38f, .31f, .17f) : new Color(.16f, .3f, .35f);
            view.Get<Button>("Right/Deferred").GetComponent<Image>().color = payment == ContractPayment.Deferred ? new Color(.38f, .31f, .17f) : new Color(.16f, .3f, .35f);
            view.Get<TMP_InputField>("Right/Count").SetTextWithoutNotify(voyages.ToString());
            view.Get<Button>("Actions/Hire").interactable = candidate != null;
        }

        private void RefreshContracts()
        {
            var view = View("ContractsScreen");
            var current = game.Economy.State.contracts.Where(c => c.status == ContractStatus.Active || c.debt > 0).ToArray();
            var past = game.Economy.State.contracts.Where(c => c.status == ContractStatus.Expired && c.endedPort == game.Navigation.DockedPort && c.endedVoyage == game.Navigation.CompletedVoyages).ToArray();
            foreach (var pair in new[]
            {
                (key: "CurrentContracts", items: current),
                (key: "PreviousVoyage", items: past)
            }

            )
            {
                var rows = ReplaceRows(view.Get<ScrollRect>(pair.key), pair.items.Length);
                for (int i = 0; i < rows.Count; i++)
                {
                    var contract = pair.items[i];
                    FillContract(rows[i], contract);
                    rows[i].OnSelect(() =>
                    {
                        contractId = contract.id;
                        SelectContract();
                    });
                }
            }

            SelectContract();
            view.Connected(game.Economy.Message ?? "현재 계약 · 현 항구 직전 동행 · 미지급금");
        }

        private void SelectContract()
        {
            var view = View("ContractsScreen");
            var selected = game.Economy.State.contracts.Find(item => item.id == contractId);
            view.Text("SelectedContract", selected?.crewId ?? "계약 선택");
            view.Text("Payment", selected?.payment.ToString() ?? "—");
            view.Text("Remaining", selected?.remainingVoyages.ToString() ?? "—");
            view.Text("Debt", selected?.debt.ToString() ?? "—");
        }

        private void FillContract(ListItemView row, EmploymentContract contract)
        {
            row.Text("Name", game.State.characters.Find(m => m.id == contract.crewId)?.displayName ?? contract.crewId);
            row.Text("Rank", game.State.characters.Find(m => m.id == contract.crewId)?.rank ?? "선원");
            row.Text("Payment", contract.payment.ToString());
            row.Text("Amount", contract.totalPrice.ToString());
            row.Text("Remaining", contract.remainingVoyages.ToString());
            row.Text("Debt", contract.debt.ToString());
        }

        private void RefreshSettlement()
        {
            var view = View("SettlementModal");
            var due = game.Economy.State.contracts.Where(c => c.debt > 0).ToArray();
            var rows = ReplaceRows(view.Get<ScrollRect>("DueContracts"), due.Length);
            for (int i = 0; i < rows.Count; i++)
                FillContract(rows[i], due[i]);
            var products = game.Market.Items.Where(item => item.tradeable && game.State.inventory.Get(item.id) > 0).ToArray();
            var cargoRows = ReplaceRows(view.Get<ScrollRect>("CargoSelection"), products.Length);
            for (int i = 0; i < products.Length; i++)
            {
                var item = products[i];
                cargoRows[i].Text("Item", item.displayName);
                cargoRows[i].Text("Stock", game.State.inventory.Get(item.id).ToString());
                cargoRows[i].Text("UnitPrice", game.Market.SalePrice(item.id, true).ToString());
                BindProductQuantity(cargoRows[i], item.id, RefreshSettlementPreview);
            }
            RefreshSettlementPreview();
            view.Connected("선택 수량은 확인 전까지 재고를 변경하지 않습니다");
        }

        private int SettlementQuantity() => Math.Min(Math.Min(quantity, game.State.inventory.Cargo), (int.MaxValue - game.State.inventory.Gold) / Math.Max(1, game.Economy.SalePrice));
        private void RefreshSettlementPreview()
        {
            var v = View("SettlementModal");
            long proceeds = TradeTotal(false, true);
            long available = game.State.inventory.Gold + proceeds;
            v.Text("SaleProceeds", proceeds.ToString());
            v.Text("Debt", game.Economy.Debt.ToString());
            v.Text("Surplus", Math.Max(0, available - game.Economy.Debt).ToString());
            v.Text("ExpectedReputation", (game.Economy.State.reputation - Math.Max(0, game.Economy.Debt - available) * game.Economy.Settings.debtReputationRate).ToString("0.0"));
        }

        public static List<ListItemView> ReplaceRows(ScrollRect scroll, int count)
        {
            var template = scroll.content.GetComponentsInChildren<ListItemView>(true).First(item => item.name == "ItemTemplate");
            foreach (Transform child in scroll.content)
                if (child != template.transform)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }

            scroll.enabled = true;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            if (count == 0)
            {
                if (scroll.verticalScrollbar) scroll.verticalScrollbar.gameObject.SetActive(false);
                if (scroll.horizontalScrollbar) scroll.horizontalScrollbar.gameObject.SetActive(false);
            }
            var empty = scroll.viewport.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(label => label.name == "EmptyState");
            if (empty)
                empty.gameObject.SetActive(count == 0);
            var rows = new List<ListItemView>();
            for (int i = 0; i < count; i++)
            {
                var row = Instantiate(template, scroll.content);
                row.name = "Row_" + i;
                row.gameObject.SetActive(true);
                rows.Add(row);
            }
            LayoutRebuilder.MarkLayoutForRebuild(scroll.content);
            return rows;
        }

        private void OnClosed(ScreenView view)
        {
            if (view.name == "SettlementModal")
                game.Economy.CancelSettlement();
            Refresh();
        }

        private void OnDestroy()
        {
            if (game != null)
                game.Economy.SettlementRequested -= OpenSettlement;
            if (stack)
                stack.Closed -= OnClosed;
        }
    }
}
