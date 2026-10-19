using System;
using System.Collections.Generic;

namespace GreatVoyage
{
    public enum ContractPayment { Prepaid, Deferred }
    public enum ContractStatus { Active, Expired, Broken }
    public enum PortService { Merchant, Broker, Tavern, Settlement }

    [Serializable]
    public sealed class EmploymentContract
    {
        public string id;
        public string crewId;
        public ContractPayment payment;
        public ContractStatus status;
        public int totalPrice;
        public int remainingVoyages;
        public int lastVoyage;
        public int debt;
        public string endedPort;
        public int endedVoyage;
        public CrewState formerCrew;
    }

    [Serializable]
    public sealed class PortMarketState
    {
        public string portId;
        public int stock;
    }

    [Serializable]
    public sealed class EconomyState
    {
        public string settingsId;
        public int gold;
        public int cargo;
        public float reputation;
        public bool gameOver;
        public bool settlementPending;
        public int contractSequence;
        public int lastVoyage;
        public List<PortMarketState> markets = new List<PortMarketState>();
        public List<EmploymentContract> contracts = new List<EmploymentContract>();
    }
}
