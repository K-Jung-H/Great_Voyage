using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GreatVoyage
{
    [Serializable]
    public sealed class FloorFacilityRoot
    {
        public string floorId;
        public Transform root;
    }

    public sealed class FacilityPresentation : MonoBehaviour
    {
        [SerializeField, Tooltip("층별로 사전 배치한 동적 시설 부모.")]
        private FloorFacilityRoot[] floors;
        [SerializeField, Tooltip("공통 표시/상호작용 바인딩.")]
        private WorldView world;
        private GameRuntime game;
        private readonly Dictionary<string, GameObject> instances = new Dictionary<string, GameObject>();
        public void Bind(GameRuntime runtime) => game = runtime;

        public void Refresh()
        {
            foreach (var facility in game.Ship.Facilities)
            {
                if (world.HasStaticFacility(facility.id) || !string.IsNullOrEmpty(facility.fixedTileId))
                    continue;
                var kind = game.Ship.GetFacilityDefinition(facility.definitionId);
                var floor = floors.First(value => value.floorId == facility.floorId);
                if (!instances.TryGetValue(facility.id, out var instance))
                {
                    instance = Instantiate(kind.prefab, floor.root);
                    instance.name = "Facility_" + facility.id;
                    instances.Add(facility.id, instance);
                }
                instance.transform.SetParent(floor.root, false);
                var floorDefinition = game.Ship.GetFloorDefinition(facility.floorId);
                instance.transform.localPosition = floorDefinition.origin + new Vector2(facility.x, facility.y) + (Vector2)kind.size * .5f;
                instance.transform.localScale = new Vector3(kind.size.x, kind.size.y, 1);
                var point = instance.GetComponentInChildren<WorldInteraction>(true);
                if (point)
                {
                    point.ConfigureFacility(facility.id, kind);
                    if (game.Ship.TryGetAccessCell(facility.id, out var access)) point.SetAccessPosition(access);
                    world.BindPoint(point);
                }
                foreach (var collider in instance.GetComponentsInChildren<Collider2D>(true))
                    if (!collider.isTrigger) collider.enabled = kind.passability == TilePassability.Impassable;
                foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
                    renderer.color = Color.Lerp(new Color(.35f,.1f,.1f), Color.white, facility.durability / game.Ship.MaximumDurability(facility));
            }
            foreach (var id in instances.Keys.Where(id => game.Ship.GetFacility(id) == null).ToArray())
            {
                Destroy(instances[id]);
                instances.Remove(id);
            }
        }
    }
}
