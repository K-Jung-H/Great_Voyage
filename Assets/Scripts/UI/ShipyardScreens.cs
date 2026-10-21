using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace GreatVoyage
{
    public sealed class ShipyardScreens : MonoBehaviour
    {
        [SerializeField, Tooltip("기획 D07의 인물·세 가지 메뉴·세부 화면.")]
        private ScreenView service;
        [SerializeField, Tooltip("사전 제작한 시설 편집 Overlay.")]
        private ScreenView editor;
        [SerializeField, Tooltip("공통 정지/입력 스택.")]
        private ScreenStack stack;
        [SerializeField, Tooltip("공통 월드 표현.")]
        private WorldView world;
        [SerializeField, Tooltip("화면 좌표를 함선 Cell로 변환할 카메라.")]
        private Camera worldCamera;
        [SerializeField, Tooltip("공통 카메라 추적.")]
        private CameraFollow follow;
        [SerializeField, Tooltip("Editor에서 배치한 편집 관전 목표.")]
        private Transform focus;
        [SerializeField, Tooltip("편집 종료 후 복귀할 플레이어.")]
        private Transform player;
        [SerializeField, Tooltip("Editor에서 배치한 시설 배치 미리보기.")]
        private Transform preview;
        [SerializeField, Tooltip("사전 제작 WorldViewport의 포인터 입력.")]
        private ShipEditPointer pointer;
        private GameRuntime game;
        private string selected, definition;
        private int floorIndex, cellX, cellY;
        private string mode = "RepairDetails";

        public void Bind(GameRuntime runtime)
        {
            game = runtime;
            service.Action("Exit", () => stack.CloseTop());
            service.Action("Menu/RepairMenu", () => Show("RepairDetails"));
            service.Action("Menu/TilePurchaseMenu", () => Show("TilePurchaseDetails"));
            service.Action("Menu/ShipPurchaseReplaceMenu", () => Show("ShipPurchaseReplaceDetails"));
            foreach (var name in new[] { "RepairDetails", "TilePurchaseDetails", "ShipPurchaseReplaceDetails" })
                service.Action(name + "/Back", () => Show(null));
            service.Action("RepairDetails/RepairSelected", () => { game.Shipyard.Repair(selected); Refresh(); });
            service.Action("RepairDetails/RepairAll", () => { game.Shipyard.Repair(); Refresh(); });
            service.Action("TilePurchaseDetails/Purchase", BeginEdit);
            service.Action("ShipPurchaseReplaceDetails/Buy", () => { game.Shipyard.BuyShip(selected != null && selected.StartsWith("model:") ? selected.Substring(6) : null); Refresh(); });
            service.Action("ShipPurchaseReplaceDetails/Sell", () => { game.Shipyard.SellShip(selected); Refresh(); });
            service.Action("ShipPurchaseReplaceDetails/Replace", () => { game.Shipyard.ReplaceShip(selected); Refresh(); });
            editor.Action("Back", () => stack.CloseTop());
            editor.Action("ConfirmLayout", () => stack.CloseTop());
            editor.Action("Left/FloorPrevious", () => { floorIndex = (floorIndex + game.Ship.Definition.floors.Length - 1) % game.Ship.Definition.floors.Length; RefreshEdit(); });
            editor.Action("Left/FloorNext", () => { floorIndex = (floorIndex + 1) % game.Ship.Definition.floors.Length; RefreshEdit(); });
            editor.Action("Right/Place", () => { game.Shipyard.Purchase(definition, Floor, cellX, cellY); RefreshEdit(); });
            editor.Action("Right/Reposition", () => { game.Shipyard.Move(selected, Floor, cellX, cellY); RefreshEdit(); });
            editor.Action("Right/Remove", () => { game.Shipyard.Remove(selected); RefreshEdit(); });
            editor.Action("Right/Upgrade", () => { game.Shipyard.Upgrade(selected); RefreshEdit(); });
            editor.Action("Right/Unlock", () => { var tile = game.Ship.GetFloorDefinition(Floor).specialTiles.FirstOrDefault(value => value.unlockable && value.cells.Contains(new Vector2Int(cellX, cellY))); if (tile != null) game.Shipyard.Unlock(Floor, tile.id); RefreshEdit(); });
            pointer.Selected += SelectCell;
            stack.Closed += Closed;
            preview.gameObject.SetActive(false);
        }

        private string Floor => game.Ship.Definition.floors[floorIndex].id;
        public void Open() { Show(null); stack.Open(service.name); }
        private void Show(string panel)
        {
            mode = panel;
            foreach (var name in new[] { "RepairDetails", "TilePurchaseDetails", "ShipPurchaseReplaceDetails" })
                service.Get<RectTransform>(name).gameObject.SetActive(name == panel);
            if (panel == "TilePurchaseDetails") definition = game.Shipyard.Catalog.FirstOrDefault(value => value.purchasePrice >= 0)?.id;
            Refresh();
        }

        private void Refresh()
        {
            service.Connected(game.Shipyard.Message ?? "현재 항구 조선소");
            if (mode == null) return;
            var scroll = service.Get<ScrollRect>(mode + "/List");
            if (mode == "RepairDetails")
            {
                var facilities = game.Ship.Facilities.ToArray();
                var rows = EconomyScreens.ReplaceRows(scroll, facilities.Length);
                for (int i = 0; i < facilities.Length; i++)
                {
                    var item = facilities[i];
                    rows[i].Text("Facility", game.Ship.GetFacilityDefinition(item.definitionId).name);
                    rows[i].Text("Durability", item.durability.ToString("0"));
                    rows[i].Text("Price", game.Shipyard.RepairCost(item).ToString());
                    rows[i].OnSelect(() => { selected = item.id; Refresh(); });
                }
            }
            else if (mode == "TilePurchaseDetails")
            {
                var catalog = game.Shipyard.Catalog.Where(value => value.purchasePrice >= 0).ToArray();
                var rows = EconomyScreens.ReplaceRows(scroll, catalog.Length);
                for (int i = 0; i < catalog.Length; i++)
                {
                    var kind = catalog[i];
                    rows[i].Text("Facility", (definition == kind.id ? "▶ " : "") + kind.name);
                    rows[i].Text("Size", kind.size.ToString());
                    rows[i].Text("Price", kind.purchasePrice.ToString());
                    rows[i].OnSelect(() => { definition = kind.id; Refresh(); });
                }
            }
            else
            {
                var ships = game.State.inactiveShips.ToArray();
                var models = game.Shipyard.Ships;
                var rows = EconomyScreens.ReplaceRows(scroll, ships.Length + models.Length);
                for (int i = 0; i < models.Length; i++)
                {
                    var model = models[i];
                    rows[i].Text("Ship", model.name + " · 신조선");
                    rows[i].Text("Price", model.purchasePrice.ToString());
                    rows[i].Text("Location", game.Navigation.DockedPort);
                    rows[i].OnSelect(() => { selected = "model:" + model.id; Refresh(); });
                }
                for (int i = 0; i < ships.Length; i++)
                {
                    var ship = ships[i];
                    var row = rows[i + models.Length];
                    row.Text("Ship", (ship.id == selected ? "▶ " : "") + ship.ship.definitionId);
                    row.Text("Location", ship.portId);
                    row.OnSelect(() => { selected = ship.id; Refresh(); });
                }
                service.Text(mode + "/Price", "구매할 모델 또는 보관 함선 선택");
            }
            service.Text(mode + "/Selection", selected ?? definition ?? "선택 없음");
            service.Text(mode + "/Cargo", "적재 용량 " + game.Shipyard.CargoCapacity);
            service.Text(mode + "/Crew", "선원 " + game.State.crew.members.Count);
            service.Text(mode + "/Prisoners", "포로 " + game.Prisoners.Held.Length + "/" + game.Prisoners.Capacity);
        }

        private void BeginEdit()
        {
            floorIndex = 0;
            selected = null;
            stack.Open(editor.name);
            follow.BindTarget(focus);
            RefreshEdit();
        }

        private void SelectCell(Vector2 screen)
        {
            var position = worldCamera.ScreenToWorldPoint(screen);
            var floor = game.Ship.GetFloorDefinition(Floor);
            var cell = Vector2Int.FloorToInt((Vector2)position / game.Ship.CellSize - floor.origin);
            cellX = cell.x; cellY = cell.y;
            RefreshEdit();
        }

        private void RefreshEdit()
        {
            world.InspectFloor(Floor);
            var floor = game.Ship.GetFloorDefinition(Floor);
            focus.position = (floor.origin + (Vector2)floor.size * .5f) * game.Ship.CellSize;
            var facilities = game.Ship.Facilities.Where(value => value.floorId == Floor).ToArray();
            var rows = EconomyScreens.ReplaceRows(editor.Get<ScrollRect>("Left/FacilityPalette"), facilities.Length);
            for (int i = 0; i < facilities.Length; i++)
            {
                var item = facilities[i];
                rows[i].Text("Facility", (item.id == selected ? "▶ " : "") + game.Ship.GetFacilityDefinition(item.definitionId).name);
                rows[i].Text("Durability", item.durability.ToString("0"));
                rows[i].OnSelect(() => { selected = item.id; definition = item.definitionId; RefreshEdit(); });
            }
            var kind = game.Ship.GetFacilityDefinition(definition);
            var existing = game.Ship.GetFacility(selected);
            var locked = floor.specialTiles.FirstOrDefault(value => value.unlockable && value.cells.Contains(new Vector2Int(cellX, cellY)) && !game.State.ship.unlockedTiles.Contains(Floor + ":" + value.id));
            editor.Get<Button>("Right/Unlock").interactable = locked != null;
            editor.Get<Button>("Right/Place").interactable = kind && existing == null && kind.purchasePrice >= 0;
            editor.Get<Button>("Right/Reposition").interactable = existing != null && string.IsNullOrEmpty(existing.fixedTileId);
            editor.Get<Button>("Right/Remove").interactable = existing != null && string.IsNullOrEmpty(existing.fixedTileId);
            editor.Get<Button>("Right/Upgrade").interactable = existing != null;
            editor.Text("Right/SelectedFacility", kind ? kind.name : "시설 선택");
            editor.Text("Right/Footprint", Floor + " · (" + cellX + ", " + cellY + ") · " + (kind ? kind.size.ToString() : ""));
            editor.Text("Right/PlacementResult", game.Shipyard.Message ?? "중앙 함선의 칸을 선택하세요.");
            editor.Text("Right/Price", kind ? kind.purchasePrice.ToString() : "");
            preview.gameObject.SetActive(kind);
            if (kind)
            {
                preview.position = (floor.origin + new Vector2(cellX, cellY) + (Vector2)kind.size * .5f) * game.Ship.CellSize;
                preview.localScale = new Vector3(kind.size.x, kind.size.y, 1) * game.Ship.CellSize;
            }
        }

        private void Closed(ScreenView view)
        {
            if (view != editor) return;
            world.InspectFloor(null);
            follow.BindTarget(player);
            preview.gameObject.SetActive(false);
            Refresh();
        }

        private void OnDestroy()
        {
            if (pointer) pointer.Selected -= SelectCell;
            if (stack) stack.Closed -= Closed;
        }
    }
}
