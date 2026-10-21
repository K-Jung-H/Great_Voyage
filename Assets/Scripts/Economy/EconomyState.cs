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
        public int day;
        public float localStanding;
        public string condition;
        public List<MarketQuote> quotes = new List<MarketQuote>();
        public List<ResidentRequest> requests = new List<ResidentRequest>();
        public List<PortQuest> quests = new List<PortQuest>();
    }

    [Serializable]
    public sealed class MarketQuote
    {
        public string productId;
        public int buyPrice, sellPrice;
    }

    [Serializable]
    public sealed class ResidentRequest
    {
        public string id, neededItem, rewardItem;
        public int needed, reward;
        public double expiresAt;
        public bool completed;
    }

    [Serializable]
    public sealed class PortQuest
    {
        public string id, productId;
        public int quantity, reward;
        public double expiresAt;
        public bool accepted, completed, failed;
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
