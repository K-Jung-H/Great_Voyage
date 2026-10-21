using System;
using System.Linq;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class ShipyardRules
    {
        private readonly GameRuntime game;
        private readonly ShipDefinition[] definitions;
        public ShipDefinition[] Ships { get; }
        public string Message { get; private set; }
        public ShipyardRules(GameRuntime game, ShipDefinition[] catalog, ShipDefinition[] definitions) { this.game = game; Ships = catalog; this.definitions = definitions; }
        public FacilityDefinition[] Catalog => game.Ship.Definition.facilities;
        private bool AtShipyard => game.Navigation.IsDocked && !game.Navigation.IsAboard && game.State.player.areaId == "town" && !game.Battle.Running;
        public int CargoCapacity => game.Ship.Facilities.Where(value => value.durability > 0 && game.Ship.GetFacilityDefinition(value.definitionId).role == FacilityRole.Cargo).Sum(value => game.Ship.GetFacilityDefinition(value.definitionId).capacity * value.level);

        public bool CanDepart()
        {
            int beds = game.Ship.Facilities.Count(value => value.durability > 0 && game.Ship.GetFacilityDefinition(value.definitionId).usableAsBed);
            if (beds < game.State.crew.members.Count(value => value.availability != CrewAvailability.Dead && value.availability != CrewAvailability.Imprisoned))
                return Reject("선원 수 이상의 작동하는 침대가 필요합니다.");
            foreach (var kind in Catalog.Where(value => value.essential))
                if (!game.Ship.Facilities.Any(value => value.definitionId == kind.id) || game.Ship.Facilities.Any(value => value.definitionId == kind.id && value.durability <= 0))
                    return Reject("필수 시설을 설치·수리해야 합니다: " + kind.name);
            Message = null;
            return true;
        }

        public int RepairCost(FacilityState facility)
        {
            var kind = game.Ship.GetFacilityDefinition(facility.definitionId);
            return Mathf.CeilToInt(Mathf.Max(0, game.Ship.MaximumDurability(facility) - facility.durability) * kind.repairPrice);
        }

        public bool Repair(string id = null)
        {
            if (!AtShipyard) return Reject("현재 항구의 조선소에서만 수리할 수 있습니다.");
            var facilities = game.Ship.Facilities.Where(value => id == null || value.id == id).ToArray();
            long cost = facilities.Sum(value => (long)RepairCost(value));
            if (facilities.Length == 0 || cost > game.State.inventory.Gold) return Reject("수리할 시설 또는 금화를 확인하세요.");
            game.State.inventory.Gold -= (int)cost;
            foreach (var facility in facilities) facility.durability = game.Ship.MaximumDurability(facility);
            return Persist("수리했습니다.");
        }

        public bool Purchase(string definitionId, string floorId, int x, int y)
        {
            if (!AtShipyard) return false;
            var kind = game.Ship.GetFacilityDefinition(definitionId);
            if (kind == null || kind.purchasePrice < 0 || game.State.inventory.Gold < kind.purchasePrice) return Reject("금화 또는 시설 정의를 확인하세요.");
            var facility = new FacilityState { id = Guid.NewGuid().ToString("D"), definitionId = kind.id, floorId = floorId, x = x, y = y, durability = kind.maxDurability };
            if (OverlapsCharacter(facility)) return Reject("선원의 현재 위치를 막는 배치는 할 수 없습니다.");
            if (!game.Ship.TryEdit(facility, null, out string reason)) return Reject(reason);
            game.State.inventory.Gold -= kind.purchasePrice;
            return Persist("시설을 구매·배치했습니다.");
        }

        public bool Move(string id, string floorId, int x, int y)
        {
            if (!AtShipyard) return false;
            var original = game.Ship.GetFacility(id);
            if (original == null) return false;
            var replacement = JsonUtility.FromJson<FacilityState>(JsonUtility.ToJson(original));
            replacement.floorId = floorId; replacement.x = x; replacement.y = y;
            if (OverlapsCharacter(replacement)) return Reject("선원의 현재 위치를 막는 배치는 할 수 없습니다.");
            if (!game.Ship.TryEdit(replacement, id, out string reason)) return Reject(reason);
            return Persist("시설을 재배치했습니다.");
        }

        public bool Remove(string id)
        {
            if (!AtShipyard) return false;
            var facility = game.Ship.GetFacility(id);
            if (facility == null) return false;
            var kind = game.Ship.GetFacilityDefinition(facility.definitionId);
            if (kind.essential && game.Ship.Facilities.Count(value => value.definitionId == kind.id) <= 1) return Reject("마지막 필수 시설은 제거할 수 없습니다.");
            if (kind.role == FacilityRole.Prison && game.Prisoners.Held.Length > game.Prisoners.Capacity - kind.capacity * facility.level) return Reject("포로 수용 공간이 부족해집니다.");
            if (kind.role == FacilityRole.Cargo && game.State.inventory.items.Where(value => value.ownerId == "player").Sum(value => (long)value.quantity) > CargoCapacity - kind.capacity * facility.level) return Reject("화물 수용 공간이 부족해집니다.");
            if (!game.Ship.TryEdit(null, id, out string reason)) return Reject(reason);
            return Persist("시설을 제거했습니다.");
        }

        public bool Upgrade(string id)
        {
            if (!AtShipyard) return false;
            var facility = game.Ship.GetFacility(id);
            if (facility == null) return false;
            var kind = game.Ship.GetFacilityDefinition(facility.definitionId);
            long cost = (long)kind.purchasePrice * facility.level;
            if (facility.level >= kind.maximumLevel || cost > game.State.inventory.Gold) return Reject("최대 단계 또는 금화를 확인하세요.");
            game.State.inventory.Gold -= (int)cost;
            facility.level++;
            return Persist("시설을 업그레이드했습니다.");
        }

        public bool BuyShip(string definitionId = null)
        {
            if (!AtShipyard) return false;
            var definition = Ships.FirstOrDefault(value => value.id == (definitionId ?? Ships.FirstOrDefault()?.id));
            if (!definition) return Reject("구매할 함선을 선택하세요.");
            if (definition.purchasePrice < 0 || game.State.inventory.Gold < definition.purchasePrice) return Reject("함선 구매 금화가 부족합니다.");
            var model = new ShipModel(definition, new PlayerLocation());
            var ship = model.Initialize(null);
            game.State.inactiveShips.Add(new OwnedShipState { id = Guid.NewGuid().ToString("D"), portId = game.Navigation.DockedPort, ship = ship });
            game.State.inventory.Gold -= definition.purchasePrice;
            return Persist("새 함선을 현재 항구에 구입했습니다.");
        }

        public bool SellShip(string id)
        {
            if (!AtShipyard) return false;
            var owned = game.State.inactiveShips.Find(value => value.id == id);
            if (owned == null || owned.portId != game.Navigation.DockedPort) return Reject("현재 항구에 보관한 함선을 선택하세요.");
            var definition = definitions.FirstOrDefault(value => value.id == owned.ship.definitionId);
            if (!definition) return false;
            int price = definition.purchasePrice / 2;
            if (price < 0 || (long)game.State.inventory.Gold + price > int.MaxValue) return false;
            game.State.inactiveShips.Remove(owned);
            game.State.inventory.Gold += price;
            return Persist("보관 함선을 판매했습니다.");
        }

        public bool ReplaceShip(string id)
        {
            if (!AtShipyard) return false;
            var target = game.State.inactiveShips.Find(value => value.id == id);
            if (target == null || target.portId != game.Navigation.DockedPort) return Reject("현재 항구에 보관한 함선을 선택하세요.");
            var definition = definitions.FirstOrDefault(value => value.id == target.ship.definitionId);
            if (!definition) return Reject("함선 공간 정의가 없습니다.");
            var model = new ShipModel(definition, new PlayerLocation());
            model.Initialize(target.ship);
            int beds = target.ship.facilities.Count(value => value.durability > 0 && model.GetFacilityDefinition(value.definitionId).usableAsBed);
            long cargo = target.ship.facilities.Where(value => value.durability > 0 && model.GetFacilityDefinition(value.definitionId).role == FacilityRole.Cargo).Sum(value => (long)model.GetFacilityDefinition(value.definitionId).capacity * value.level);
            long prisons = target.ship.facilities.Where(value => value.durability > 0 && model.GetFacilityDefinition(value.definitionId).role == FacilityRole.Prison).Sum(value => (long)model.GetFacilityDefinition(value.definitionId).capacity * value.level);
            if (beds < game.State.crew.members.Count(value => value.availability != CrewAvailability.Dead) || cargo < game.State.inventory.items.Where(value => value.ownerId == "player").Sum(value => (long)value.quantity) || prisons < game.Prisoners.Held.Length) return Reject("교체할 함선의 선원·화물·포로 수용 공간이 부족합니다.");
            game.Rest.Wake();
            var previous = new OwnedShipState { id = game.State.shipInstanceId, portId = game.Navigation.DockedPort, ship = game.State.ship };
            game.Ship.ChangeDefinition(definition, target.ship);
            game.State.ship = game.Ship.State;
            game.State.shipInstanceId = target.id;
            game.State.inactiveShips.Remove(target);
            game.State.inactiveShips.Add(previous);
            game.State.navigation.boardingPosition = game.Ship.EntryPosition(definition.startingFloor);
            game.Crew.ResetPositionsForShip();
            return Persist("선원·화물·포로를 이전하고 함선을 교체했습니다.");
        }

        public bool Unlock(string floorId, string tileId)
        {
            if (!AtShipyard) return false;
            var tile = game.Ship.GetFloorDefinition(floorId)?.specialTiles.FirstOrDefault(value => value.id == tileId && value.unlockable);
            string key = floorId + ":" + tileId;
            if (tile == null || game.State.ship.unlockedTiles.Contains(key) || tile.unlockPrice < 0 || game.State.inventory.Gold < tile.unlockPrice) return Reject("해금 대상·금화를 확인하세요.");
            game.State.inventory.Gold -= tile.unlockPrice;
            game.State.ship.unlockedTiles.Add(key);
            return Persist("선내 잠금 타일을 해금했습니다.");
        }

        private bool OverlapsCharacter(FacilityState facility)
        {
            var floor = game.Ship.GetFloorDefinition(facility.floorId);
            var kind = game.Ship.GetFacilityDefinition(facility.definitionId);
            if (floor == null || kind == null) return true;
            var bounds = new Rect((floor.origin + new Vector2(facility.x, facility.y)) * game.Ship.CellSize, (Vector2)kind.size * game.Ship.CellSize);
            return game.State.crew.members.Any(value => value.floorId == facility.floorId && value.availability != CrewAvailability.Dead && bounds.Overlaps(new Rect(value.position - Vector2.one * .3f * game.Ship.CellSize, Vector2.one * .6f * game.Ship.CellSize)));
        }

        private bool Reject(string reason) { Message = reason; return false; }
        private bool Persist(string message) { Message = message; game.RequestSave(); game.NotifyChanged(); return true; }
    }
}
