using System;
using System.Collections.Generic;

namespace GreatVoyage
{
    [Serializable]
    public sealed class InventoryEntry
    {
        public string ownerId = "player";
        public string productId;
        public int quantity;
    }

    [Serializable]
    public sealed class InventoryState
    {
        public int gold;
        public string cargoProductId = "cloth";
        public List<InventoryEntry> items = new List<InventoryEntry>();
        public int Gold
        {
            get => gold;
            set
            {
                if (value < 0)
                    throw new InvalidOperationException("Negative gold");
                gold = value;
            }
        }

        public int Cargo { get => Get(cargoProductId); set => Set(cargoProductId, value); }
        public int Food { get => Get("food"); set => Set("food", value); }
        public int RepairTools { get => Get("repair-tools"); set => Set("repair-tools", value); }

        public int Get(string product, string owner = "player") => items.Find(item => item.productId == product && item.ownerId == owner)?.quantity ?? 0;
        public void Set(string product, int quantity, string owner = "player")
        {
            if (string.IsNullOrEmpty(product) || quantity < 0)
                throw new InvalidOperationException("Invalid inventory entry");
            var item = items.Find(value => value.productId == product && value.ownerId == owner);
            if (item == null)
            {
                item = new InventoryEntry
                {
                    productId = product,
                    ownerId = owner
                };
                items.Add(item);
            }

            item.quantity = quantity;
        }
    }
}
