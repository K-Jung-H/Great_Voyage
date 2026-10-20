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
        public event Action SettlementConfirmed;
        public void Bind(GameRuntime runtime)
        {
            game = runtime;
            View("MerchantScreen").Action("Right/Buy", () => Command(() => game.Economy.Buy(quantity)));
            View("MerchantScreen").Action("Exit", Close);
            View("BrokerScreen").Action("LeftTwoThirds/Sell", () => Command(() => game.Economy.Sell(quantity)));
            View("BrokerScreen").Action("Exit", Close);
            var hiring = View("HiringScreen");
            hiring.Action("Actions/Cancel", Close);
            hiring.Action("Actions/Hire", () => Command(() => game.Economy.Hire(voyages, payment, candidateId)));
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
                int sale = SettlementQuantity();
                if (sale > 0)
                    game.Economy.Sell(sale);
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
            stack.Open(name);
            Refresh();
        }

        public void OpenHiring(string id)
        {
            candidateId = id;
            voyages = 1;
            payment = ContractPayment.Prepaid;
            stack.Open("HiringScreen");
            RefreshHiring();
        }

        public void OpenSettlement()
        {
            quantity = 0;
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
            view.Connected(game.Economy.Message ?? "대표 거래 · 직물 1종");
            string list = name == "MerchantScreen" ? "Right/ProductScroll" : "LeftTwoThirds/OwnedGoods";
            var row = ReplaceRows(view.Get<ScrollRect>(list), 1)[0];
            row.Text("Item", game.Economy.Settings.productName);
            row.Text("Stock", name == "MerchantScreen" ? game.State.inventory.Get(game.Economy.Settings.productId, "port:" + game.Navigation.DockedPort + ":merchant").ToString() : game.State.inventory.Cargo.ToString());
            row.Text("UnitPrice", (name == "MerchantScreen" ? game.Economy.Port.buyPrice : game.Economy.SalePrice).ToString());
            BindQuantity(row, () =>
            {
                if (name == "MerchantScreen")
                    view.Text("Right/SelectedTotal", ((long)quantity * game.Economy.Port.buyPrice).ToString());
                else
                    view.Text("LeftTwoThirds/SaleTotal", ((long)quantity * game.Economy.SalePrice).ToString());
            });
            if (name == "MerchantScreen")
            {
                view.Text("Right/Wallet", game.State.inventory.Gold.ToString());
                view.Text("Right/SelectedTotal", ((long)quantity * game.Economy.Port.buyPrice).ToString());
            }
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
            view.Connected(game.Economy.Message ?? "대표 계약 · 협상/평가 효과는 후속 단계");
            view.Text("Right/Name", candidate?.displayName ?? "고용 불가");
            view.Text("Right/Health", candidate?.health.ToString("0") ?? "—");
            view.Text("Right/Experience", candidate?.navigationExperience.ToString() ?? "—");
            view.Text("Right/Specialty", candidate?.specialty.ToString() ?? "—");
            view.Text("Right/PerVoyage", game.Economy.Settings.wagePerVoyage.ToString());
            view.Text("Right/Total", ((long)game.Economy.Settings.wagePerVoyage * voyages).ToString("N0"));
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
            row.Text("Rank", "미정");
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
            var cargo = ReplaceRows(view.Get<ScrollRect>("CargoSelection"), 1)[0];
            cargo.Text("Item", game.Economy.Settings.productName);
            cargo.Text("Stock", game.State.inventory.Cargo.ToString());
            cargo.Text("UnitPrice", game.Economy.SalePrice.ToString());
            BindQuantity(cargo, RefreshSettlementPreview);
            RefreshSettlementPreview();
            view.Connected("선택 수량은 확인 전까지 재고를 변경하지 않습니다");
        }

        private int SettlementQuantity() => Math.Min(Math.Min(quantity, game.State.inventory.Cargo), (int.MaxValue - game.State.inventory.Gold) / Math.Max(1, game.Economy.SalePrice));
        private void RefreshSettlementPreview()
        {
            var v = View("SettlementModal");
            long proceeds = (long)SettlementQuantity() * game.Economy.SalePrice;
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
