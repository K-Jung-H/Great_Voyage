using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class EconomyRules : IDisposable
    {
        private readonly EconomySettings settings;
        private readonly GameRuntime session;
        private readonly NavigationRules navigation;
        private readonly CrewBrain crew;
        private readonly InventoryState inventory;
        public event Action SettlementRequested;
        public EconomyRules(EconomySettings settings, GameRuntime session, NavigationRules navigation, CrewBrain crew, InventoryState inventory)
        {
            this.settings = settings;
            this.session = session;
            this.navigation = navigation;
            this.crew = crew;
            this.inventory = inventory;
        }

        private EconomyState state;
        private IDisposable gameOverPause;
        public EconomyState State => state;
        public EconomySettings Settings => settings;
        public CrewState[] Candidates => new[]
        {
            settings.candidate
        }.Concat(settings.additionalCandidates ?? Array.Empty<CrewState>()).ToArray();
        public string Message { get; private set; }
        public int Debt => state.contracts.Sum(contract => contract.debt);
        public bool CanAct => state != null && session.IsReady && session.ErrorMessage == null && !state.gameOver && navigation.IsDocked;
        public TradePortDefinition Port => Array.Find(settings.ports, port => port.portId == navigation.DockedPort);
        public int SalePrice => state.settlementPending ? Mathf.FloorToInt(Port.buyPrice * settings.liquidationRate) : Port.sellPrice;

        // Role: 정의·복원 상태를 검증하고 항해 완료와 출항 검사를 연결한다.
        public EconomyState Initialize(EconomyState saved)
        {
            if (settings == null || session == null || navigation == null || crew == null || string.IsNullOrEmpty(settings.id) || string.IsNullOrEmpty(settings.productId) || settings.candidate == null || settings.initialGold < 0 || settings.wagePerVoyage <= 0 || settings.maxVoyages < 1 || settings.maxVoyages > 100 || (long)settings.wagePerVoyage * settings.maxVoyages > 1000000 || !Finite(settings.initialReputation) || !Finite(settings.gameOverThreshold) || settings.initialReputation <= settings.gameOverThreshold || !Finite(settings.debtReputationRate) || settings.debtReputationRate <= 0 || !Finite(settings.liquidationRate) || settings.liquidationRate <= 0 || settings.liquidationRate > 1 || settings.ports == null || settings.ports.Length != navigation.PortCount)
                throw new InvalidDataException("경제 설정 또는 참조가 유효하지 않습니다.");
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (TradePortDefinition port in settings.ports)
                if (port == null || !navigation.HasPort(port.portId) || !ids.Add(port.portId) || port.buyPrice <= 0 || port.sellPrice <= 0 || port.buyPrice > 1000000 || port.sellPrice > 1000000 || port.initialStock < 0)
                    throw new InvalidDataException("거래 항구 정의가 유효하지 않습니다.");
            ids.Clear();
            foreach (CrewState candidate in Candidates)
            {
                crew.CheckCandidate(candidate);
                if (!ids.Add(candidate.id))
                    throw new InvalidDataException("고용 후보 ID가 중복됩니다.");
            }

            state = saved ?? new EconomyState
            {
                settingsId = settings.id,
                gold = settings.initialGold,
                reputation = settings.initialReputation,
                lastVoyage = navigation.CompletedVoyages
            };
            if (saved == null)
                foreach (TradePortDefinition port in settings.ports)
                {
                    state.markets.Add(new PortMarketState { portId = port.portId });
                    inventory.Set(settings.productId, port.initialStock, "port:" + port.portId + ":merchant");
                }

            if (state.settingsId != settings.id || inventory.Gold < 0 || inventory.Cargo < 0 || !Finite(state.reputation) || state.markets == null || state.markets.Count != settings.ports.Length || state.contracts == null || state.contracts.Count > 10000 || state.contractSequence < state.contracts.Count || state.lastVoyage != navigation.CompletedVoyages || (state.settlementPending && !navigation.IsDocked))
                throw new InvalidDataException("경제 저장 상태가 유효하지 않습니다.");
            ids.Clear();
            foreach (PortMarketState market in state.markets)
                if (market == null || !ids.Add(market.portId) || market.stock < 0 || !Array.Exists(settings.ports, p => p.portId == market.portId))
                    throw new InvalidDataException("항구 재고 상태가 유효하지 않습니다.");
            ids.Clear();
            var active = new System.Collections.Generic.HashSet<string>();
            long debts = 0;
            foreach (EmploymentContract contract in state.contracts)
            {
                if (contract == null || string.IsNullOrEmpty(contract.id) || !ids.Add(contract.id) || !session.State.characters.Exists(candidate => candidate.id == contract.crewId) || !Enum.IsDefined(typeof(ContractPayment), contract.payment) || !Enum.IsDefined(typeof(ContractStatus), contract.status) || contract.totalPrice <= 0 || contract.debt < 0 || contract.debt > contract.totalPrice || contract.remainingVoyages < 0 || contract.lastVoyage != state.lastVoyage || (contract.payment == ContractPayment.Prepaid && contract.debt != 0) || (contract.status == ContractStatus.Active && (contract.remainingVoyages == 0 || contract.debt != 0 || crew.FindMember(contract.crewId) == null || !active.Add(contract.crewId))) || (contract.status == ContractStatus.Expired && (contract.remainingVoyages != 0 || !session.State.characters.Exists(member => member.id == contract.crewId) || contract.endedVoyage <= 0 || contract.endedVoyage > state.lastVoyage || !navigation.HasPort(contract.endedPort))) || (contract.status == ContractStatus.Broken && contract.debt != 0))
                    throw new InvalidDataException("계약 저장 상태가 유효하지 않습니다.");
                debts += contract.debt;
            }

            if (debts > int.MaxValue || state.gameOver != (state.reputation <= settings.gameOverThreshold) || (state.gameOver && !navigation.IsDocked))
                throw new InvalidDataException("미지급금 또는 게임 오버 상태가 유효하지 않습니다.");
            navigation.VoyageCompleted += CompleteVoyage;
            if (state.gameOver)
                gameOverPause = session.Pauses.Acquire("GameOver");
            return state;
        }

        // Role: 수량·대금·상인 재고를 검사한 후 구매를 한 번 저장한다.
        public void Buy(int quantity)
        {
            if (!CanAct || navigation.IsAboard || state.settlementPending)
                return;
            PortMarketState market = state.markets.Find(value => value.portId == navigation.DockedPort);
            long price = (long)Port.buyPrice * quantity;
            string owner = "port:" + market.portId + ":merchant";
            if (quantity <= 0 || quantity > inventory.Get(settings.productId, owner) || price > inventory.Gold || (long)inventory.Cargo + quantity > int.MaxValue)
            {
                Message = "금화 또는 상인 재고가 부족합니다.";
                return;
            }

            inventory.Gold -= (int)price;
            inventory.Cargo += quantity;
            inventory.Set(settings.productId, inventory.Get(settings.productId, owner) - quantity, owner);
            Persist("구매했습니다.");
        }

        // Role: 선택한 상품을 대행인 또는 강제 매각 가격으로 판매한다.
        public void Sell(int quantity)
        {
            if (!CanAct || (navigation.IsAboard && !state.settlementPending))
                return;
            long price = (long)SalePrice * quantity;
            if (quantity <= 0 || quantity > inventory.Cargo || price + inventory.Gold > int.MaxValue)
            {
                Message = "판매 수량 또는 금화 한도를 확인하세요.";
                return;
            }

            inventory.Cargo -= quantity;
            inventory.Gold += (int)price;
            Persist("판매했습니다. 정산에 쓰지 않은 금화는 유지됩니다.");
        }

        // Role: 현재 항구에서 다시 계약할 수 있는 대표 후보를 조회한다.
        public CrewState Candidate(string candidateId = null)
        {
            CrewState definition = Candidates.FirstOrDefault(candidate => candidate.id == (candidateId ?? settings.candidate.id));
            if (definition == null || crew.FindMember(definition.id) != null)
                return null;
            EmploymentContract previous = state.contracts.LastOrDefault(contract => contract.crewId == definition.id);
            if (previous == null)
                return definition;
            return previous.status == ContractStatus.Expired && previous.debt == 0 && previous.endedPort == navigation.DockedPort && previous.endedVoyage == navigation.CompletedVoyages ? session.State.characters.Find(member => member.id == definition.id) : null;
        }

        // Role: 선불은 전액 차감하고 후불은 만료 채무로 예약하며 선원을 추가한다.
        public void Hire(int voyages, ContractPayment payment, string candidateId = null)
        {
            if (!CanAct || navigation.IsAboard || state.settlementPending)
                return;
            CrewState candidate = Candidate(candidateId);
            if (candidate == null || voyages < 1 || voyages > settings.maxVoyages || !Enum.IsDefined(typeof(ContractPayment), payment) || state.contractSequence == int.MaxValue || state.contracts.Count >= 10000)
            {
                Message = "고용 가능한 후보 또는 계약 횟수를 확인하세요.";
                return;
            }

            int total = settings.wagePerVoyage * voyages;
            if (payment == ContractPayment.Prepaid && inventory.Gold < total)
            {
                Message = "선불 전액을 지급할 금화가 부족합니다.";
                return;
            }

            CrewState boarding = JsonUtility.FromJson<CrewState>(JsonUtility.ToJson(candidate));
            CrewState definition = Candidates.First(item => item.id == candidate.id);
            boarding.floorId = definition.floorId;
            boarding.position = definition.position;
            crew.AddMember(boarding);
            session.State.Register(crew.FindMember(boarding.id));
            if (boarding.primaryDuty == CrewDuty.Navigation && CrewBrain.QualifiedNavigator(boarding))
                navigation.journal.State.autopilot = true;
            if (payment == ContractPayment.Prepaid)
                inventory.Gold -= total;
            state.contractSequence++;
            state.contracts.Add(new EmploymentContract { id = "contract-" + state.contractSequence, crewId = candidate.id, payment = payment, totalPrice = total, remainingVoyages = voyages, lastVoyage = navigation.CompletedVoyages });
            Persist("계약을 체결했습니다.");
        }

        // Role: 완료 항해 순번을 한 번 반영하고 만료 선원의 선점을 해제한다.
        private void CompleteVoyage(string voyageId, string portId)
        {
            int sequence = navigation.CompletedVoyages;
            if (sequence <= state.lastVoyage)
                return;
            foreach (EmploymentContract contract in state.contracts)
            {
                if (contract.status == ContractStatus.Active)
                {
                    CrewState member = crew.FindMember(contract.crewId);
                    if (member == null || member.availability == CrewAvailability.Dead || member.availability == CrewAvailability.Imprisoned)
                    {
                        contract.status = ContractStatus.Broken;
                        contract.debt = 0;
                    }
                    else if (--contract.remainingVoyages == 0)
                    {
                        contract.status = ContractStatus.Expired;
                        contract.endedPort = portId;
                        contract.endedVoyage = sequence;
                        session.State.Register(crew.RemoveMember(contract.crewId));
                        contract.formerCrew = null;
                        contract.debt = contract.payment == ContractPayment.Deferred ? contract.totalPrice : 0;
                    }
                }

                contract.lastVoyage = sequence;
            }

            state.lastVoyage = sequence;
            Persist("입항 및 계약 횟수를 저장했습니다.");
        }

        // Role: 선택한 만료 계약의 후불 전액을 한 번 지급한다.
        public void Pay(string contractId)
        {
            if (!CanAct)
                return;
            EmploymentContract contract = state.contracts.Find(value => value.id == contractId);
            if (contract == null || contract.debt == 0)
                return;
            if (inventory.Gold < contract.debt)
            {
                Message = "후불 전액을 지급할 금화가 부족합니다.";
                return;
            }

            inventory.Gold -= contract.debt;
            contract.debt = 0;
            Persist("후불 전액을 지급했습니다.");
        }

        // Role: 미정산 출항을 멈추고 사용자 선택 매각 화면을 연다.
        public bool CanDepart()
        {
            if (!CanAct)
                return false;
            if (Debt == 0 && !state.settlementPending)
                return true;
            state.settlementPending = true;
            if (Persist("매각 수량을 선택하고 정산 후 출항을 확정하세요."))
                SettlementRequested?.Invoke();
            return false;
        }

        // Role: 보유금 정산·잔여 채무 소멸·명성 판정을 저장한 뒤 출항을 허용한다.
        public void SettleAndDepart()
        {
            if (!CanAct || !state.settlementPending)
                return;
            int unpaid = 0;
            foreach (EmploymentContract contract in state.contracts)
            {
                int paid = Math.Min(inventory.Gold, contract.debt);
                inventory.Gold -= paid;
                unpaid += contract.debt - paid;
                contract.debt = 0;
            }

            if (unpaid > 0)
                state.reputation -= unpaid * settings.debtReputationRate;
            state.gameOver = state.reputation <= settings.gameOverThreshold;
            state.settlementPending = false;
            if (state.gameOver)
                gameOverPause = session.Pauses.Acquire("GameOver");
            if (!Persist(state.gameOver ? "명성 기준 이하로 게임 오버입니다. 이 저장에서는 계속할 수 없습니다." : "정산을 완료했습니다."))
                return;
        }

        // Role: 출항 정산 취소를 저장하고 현재 항구에 남는다.
        public void CancelSettlement()
        {
            if (state == null || state.gameOver)
                return;
            if (state.settlementPending)
            {
                state.settlementPending = false;
                Persist("출항을 취소했습니다.");
            }
        }

        // Role: 변경 결과와 전체 세션을 함께 저장하고 실패를 표시한다.
        private bool Persist(string message)
        {
            Message = message;
            bool saved = session.RequestSave();
            if (!saved)
                Message = session.ErrorMessage;
            return saved;
        }

        // Role: 저장에 허용할 유한 수치인지 검사한다.
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        // Role: 항해 알림과 이 시스템의 정지 사유를 해제한다.
        public void Dispose()
        {
            if (navigation != null)
                navigation.VoyageCompleted -= CompleteVoyage;
            gameOverPause?.Dispose();
        }
    }
}
