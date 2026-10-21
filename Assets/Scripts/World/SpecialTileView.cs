using System;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class SpecialTileView : MonoBehaviour
    {
        [SerializeField, Tooltip("고정 특수 타일의 층 ID.")]
        private string floorId;
        [SerializeField, Tooltip("고정 특수 타일 ID. 시설 인스턴스이면 비워 둔다.")]
        private string tileId;
        [SerializeField, Tooltip("시설 상태 ID. 고정 특수 타일이면 비워 둔다.")]
        private string facilityId;
        [SerializeField, Tooltip("통행 분류에 따라 제어할 고체 Collider. 상호작용 Trigger는 넣지 않는다.")]
        private Collider2D[] blockers;
        [SerializeField, Tooltip("이 타일이 없는 기존 함선 정의에서는 표시·충돌을 숨긴다.")]
        private bool optionalForLegacy;
        private ShipModel ship;
        private SpriteRenderer[] renderers;
        public void Bind(ShipModel ship)
        {
            this.ship = ship;
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            Refresh();
        }
        public void Refresh()
        {
            if (string.IsNullOrEmpty(tileId) == string.IsNullOrEmpty(facilityId) || blockers == null)
                throw new InvalidOperationException("Special tile identity/binding invalid: " + name);
            TilePassability passability;
            if (!string.IsNullOrEmpty(facilityId))
            {
                var state = ship.GetFacility(facilityId);
                if (state == null)
                    return;
                passability = ship.GetFacilityDefinition(state.definitionId).passability;
            }
            else
            {
                var floor = ship.GetFloorDefinition(floorId);
                var tile = Array.Find(floor?.specialTiles ?? Array.Empty<SpecialTileDefinition>(), t => t.id == tileId);
                if (tile == null && !optionalForLegacy)
                    throw new InvalidOperationException("Special tile binding missing: " + tileId);
                passability = ship.IsTileBlocking(floorId, tileId) ? TilePassability.Impassable : TilePassability.Passable;
                if (tile?.unlockable == true || optionalForLegacy)
                    foreach (var renderer in renderers) renderer.enabled = tile != null && passability == TilePassability.Impassable;
            }

            foreach (var collider in blockers)
            {
                if (!collider || collider.isTrigger)
                    throw new InvalidOperationException("Special tile solid binding invalid: " + name);
                collider.enabled = passability == TilePassability.Impassable;
            }
        }
    }
}
