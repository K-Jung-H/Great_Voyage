using System;
using System.Linq;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class MarketRules
    {
        private readonly GameRuntime game;
        private readonly ItemDefinition[] items;
        private readonly MarketSettings settings;
        public string Message { get; private set; }
        public ItemDefinition[] Items => items;
        public PortMarketState Current => game.State.economy.markets.Find(value => value.portId == game.Navigation.DockedPort);
        public MarketRules(GameRuntime game, ItemDefinition[] items, MarketSettings settings)
        {
            this.game = game; this.items = items; this.settings = settings;
            if (!settings || items.Any(item => item.basePrice <= 0 || item.dailyStock < 0) || settings.maximumRequests < 0 || settings.maximumRequests > 20 || settings.requestLifetime <= 0 || settings.questLifetime <= 0)
                throw new System.IO.InvalidDataException("Invalid market definition");
            foreach (var market in game.State.economy.markets)
            {
                market.quotes ??= new System.Collections.Generic.List<MarketQuote>();
                market.requests ??= new System.Collections.Generic.List<ResidentRequest>();
                market.quests ??= new System.Collections.Generic.List<PortQuest>();
            }
            Advance();
        }

        public ItemDefinition[] ProductsForMerchant(string id)
        {
            var shops = game.Economy.Port?.shops;
            int index = id != null && id.StartsWith("merchant-") && int.TryParse(id.Substring(9), out int parsed) ? parsed : -1;
            var category = shops != null && index >= 0 && index < shops.Length ? shops[index] : ProductCategory.Clothing;
            return items.Where(item => item.tradeable && item.category == category).ToArray();
        }

        public MarketQuote Quote(string product) => Current?.quotes.Find(value => value.productId == product);
        public int SalePrice(string product, bool liquidation = false)
        {
            var quote = Quote(product);
            return quote == null ? 0 : liquidation ? Mathf.Max(1, Mathf.FloorToInt(quote.buyPrice * game.Economy.Settings.liquidationRate)) : quote.sellPrice;
        }

        public bool Buy(string product, int quantity)
        {
            var quote = Quote(product);
            if (!CanTrade || quote == null || quantity <= 0) return false;
            var inventory = game.State.inventory;
            string owner = "port:" + Current.portId + ":merchant";
            long price = (long)quote.buyPrice * quantity;
            long held = inventory.items.Where(value => value.ownerId == "player").Sum(value => (long)value.quantity);
            if (quantity > inventory.Get(product, owner) || price > inventory.Gold || held + quantity > game.Shipyard.CargoCapacity || (long)inventory.Get(product) + quantity > int.MaxValue) return Reject("금화·재고·적재 용량을 확인하세요.");
            inventory.Gold -= (int)price;
            inventory.Set(product, inventory.Get(product) + quantity);
            inventory.Set(product, inventory.Get(product, owner) - quantity, owner);
            return Persist("구매했습니다.");
        }

        public bool Sell(string product, int quantity, bool liquidation = false)
        {
            if (Current == null || game.State.economy.gameOver || !game.Navigation.IsDocked || quantity <= 0 || (!liquidation && !CanTrade) || (liquidation && !game.State.economy.settlementPending)) return false;
            var item = items.FirstOrDefault(value => value.id == product && value.tradeable);
            long price = (long)SalePrice(product, liquidation) * quantity;
            var inventory = game.State.inventory;
            if (item == null || quantity > inventory.Get(product) || price <= 0 || price + inventory.Gold > int.MaxValue) return Reject("판매 수량·금화 한도를 확인하세요.");
            inventory.Set(product, inventory.Get(product) - quantity);
            inventory.Gold += (int)price;
            return Persist("판매했습니다.");
        }

        private bool CanTrade => Current != null && game.Navigation.IsDocked && !game.Navigation.IsAboard && !game.State.economy.gameOver && !game.State.economy.settlementPending;
        public bool TradeBatch(System.Collections.Generic.IReadOnlyDictionary<string, int> selection, bool buy, bool liquidation = false)
        {
            if (Current == null || game.State.economy.gameOver || (!liquidation && !CanTrade) || (liquidation && (!game.State.economy.settlementPending || buy))) return false;
            var chosen = selection.Where(value => value.Value > 0).ToArray();
            long total = 0, count = 0;
            string owner = "port:" + Current.portId + ":merchant";
            foreach (var pair in chosen)
            {
                var item = items.FirstOrDefault(value => value.id == pair.Key && value.tradeable);
                var quote = Quote(pair.Key);
                if (item == null || quote == null || pair.Value > game.State.inventory.Get(pair.Key, buy ? owner : "player")) return Reject("선택 수량·상품·재고를 확인하세요.");
                total += (long)(buy ? quote.buyPrice : SalePrice(pair.Key, liquidation)) * pair.Value;
                count += pair.Value;
            }
            if (chosen.Length == 0 && !liquidation) return Reject("수량을 선택하세요.");
            if (buy && (total > game.State.inventory.Gold || count + game.State.inventory.items.Where(value => value.ownerId == "player").Sum(value => (long)value.quantity) > game.Shipyard.CargoCapacity)) return Reject("금화 또는 적재 용량이 부족합니다.");
            if (!buy && total + game.State.inventory.Gold > int.MaxValue) return Reject("금화 한도를 초과합니다.");
            foreach (var pair in chosen)
                if (buy) Buy(pair.Key, pair.Value); else Sell(pair.Key, pair.Value, liquidation);
            return true;
        }
        public void Advance()
        {
            int day = (int)(game.ElapsedSeconds / GameRuntime.SecondsPerDay) + 1;
            foreach (var market in game.State.economy.markets)
            {
                if (market.day != day) RefreshDay(market, day);
                foreach (var quest in market.quests.Where(value => value.accepted && !value.completed && !value.failed && value.expiresAt <= game.ElapsedSeconds))
                {
                    quest.failed = true;
                    market.localStanding = Mathf.Max(-100, market.localStanding - settings.questStanding);
                    game.RequestSave();
                }
            }
        }

        private void RefreshDay(PortMarketState market, int day)
        {
            bool first = market.day == 0;
            market.day = day;
            uint seed = 2166136261u;
            foreach (char character in market.portId + ":" + day) seed = (seed ^ character) * 16777619u;
            if (seed == 0) seed = 1;
            var conditions = new[] { "맑음", "비", "전쟁", "가뭄", "역병" };
            market.condition = conditions[(int)(EventRules.Next(ref seed) * conditions.Length)];
            market.quotes.Clear();
            foreach (var item in items.Where(value => value.tradeable))
            {
                float demand = market.condition == "전쟁" && item.category == ProductCategory.Weapons ? 1.3f : market.condition == "가뭄" && item.category == ProductCategory.Vegetables ? 1.4f : market.condition == "역병" && item.category == ProductCategory.Medicine ? 1.5f : market.condition == "비" && item.category == ProductCategory.Food ? 1.2f : 1;
                float seasonal = item.category == ProductCategory.Spices && day / 30 % 4 == 3 ? 1.15f : 1;
                float trend = EventRules.Next(ref seed) > .8f ? 1.15f : 1;
                int buy = Mathf.Max(1, Mathf.RoundToInt(item.basePrice * Mathf.Lerp(1 - settings.dailyVariation, 1 + settings.dailyVariation, EventRules.Next(ref seed)) * demand * seasonal * trend * (1 - market.localStanding / 1000)));
                market.quotes.Add(new MarketQuote { productId = item.id, buyPrice = buy, sellPrice = Mathf.Max(1, Mathf.RoundToInt(buy * settings.saleRatio)) });
                string owner = "port:" + market.portId + ":merchant";
                if (!first || !game.State.inventory.items.Any(value => value.ownerId == owner && value.productId == item.id)) game.State.inventory.Set(item.id, item.dailyStock, owner);
            }
            market.requests.Clear();
            var goods = items.Where(value => value.tradeable && value.use == ItemUse.TradeGoods).ToArray();
            for (int i = 0; i < settings.maximumRequests && goods.Length > 0; i++)
            {
                var item = goods[(int)(EventRules.Next(ref seed) * goods.Length)];
                bool wantsGold = i % 2 == 1;
                int quantity = 1 + (int)(EventRules.Next(ref seed) * 3);
                int value = Mathf.Max(1, Mathf.RoundToInt(item.basePrice * quantity * Mathf.Lerp(.7f, 1.3f, EventRules.Next(ref seed))));
                market.requests.Add(new ResidentRequest { id = market.portId + ":" + day + ":request:" + i, neededItem = wantsGold ? null : item.id, needed = wantsGold ? value : quantity, rewardItem = wantsGold ? item.id : null, reward = wantsGold ? quantity : value, expiresAt = (day - 1) * GameRuntime.SecondsPerDay + settings.requestLifetime });
            }
            market.quests.RemoveAll(value => !value.accepted || value.completed || value.failed);
            if (goods.Length > 0)
            {
                var item = goods[(int)(EventRules.Next(ref seed) * goods.Length)];
                market.quests.Add(new PortQuest { id = market.portId + ":" + day + ":quest", productId = item.id, quantity = 3, reward = item.basePrice * 4 });
            }
            game.RequestSave();
        }

        public bool Fulfill(string id)
        {
            if (!CanTrade) return false;
            var request = Current.requests.Find(value => value.id == id && !value.completed && value.expiresAt > game.ElapsedSeconds);
            if (request == null) return false;
            var inventory = game.State.inventory;
            int have = string.IsNullOrEmpty(request.neededItem) ? inventory.Gold : inventory.Get(request.neededItem);
            long after = (string.IsNullOrEmpty(request.rewardItem) ? inventory.Gold : inventory.Get(request.rewardItem)) + (long)request.reward;
            if (have < request.needed || after > int.MaxValue) return Reject("요청 재산이 부족하거나 보상 한도를 초과합니다.");
            if (!string.IsNullOrEmpty(request.rewardItem) && inventory.items.Where(value => value.ownerId == "player").Sum(value => (long)value.quantity) - (string.IsNullOrEmpty(request.neededItem) ? 0 : request.needed) + request.reward > game.Shipyard.CargoCapacity) return Reject("보상 적재 공간이 부족합니다.");
            if (string.IsNullOrEmpty(request.neededItem)) inventory.Gold -= request.needed; else inventory.Set(request.neededItem, have - request.needed);
            if (string.IsNullOrEmpty(request.rewardItem)) inventory.Gold += request.reward; else inventory.Set(request.rewardItem, inventory.Get(request.rewardItem) + request.reward);
            request.completed = true;
            return Persist("주민 요청을 완료했습니다.");
        }

        public bool Quest(string id)
        {
            if (!CanTrade || game.State.player.areaId != "town") return false;
            var quest = Current.quests.Find(value => value.id == id && !value.failed && !value.completed);
            if (quest == null) return false;
            if (!quest.accepted)
            {
                quest.accepted = true; quest.expiresAt = game.ElapsedSeconds + settings.questLifetime;
                return Persist("퀘스트를 수주했습니다.");
            }
            if (quest.expiresAt <= game.ElapsedSeconds || game.State.inventory.Get(quest.productId) < quest.quantity || (long)game.State.inventory.Gold + quest.reward > int.MaxValue) return Reject("기한·납품 수량·보상 한도를 확인하세요.");
            game.State.inventory.Set(quest.productId, game.State.inventory.Get(quest.productId) - quest.quantity);
            game.State.inventory.Gold += quest.reward;
            quest.completed = true;
            Current.localStanding = Mathf.Min(100, Current.localStanding + settings.questStanding);
            return Persist("퀘스트를 완료했습니다. 현지 평판이 올랐습니다.");
        }
        public string ProductName(string id) => string.IsNullOrEmpty(id) ? "금화" : items.FirstOrDefault(value => value.id == id)?.displayName ?? id;
        private bool Reject(string message) { Message = message; return false; }
        private bool Persist(string message) { Message = message; game.RequestSave(); return true; }
    }
}
